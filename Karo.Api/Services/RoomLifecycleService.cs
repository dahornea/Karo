using Karo.Api.Models;
using Microsoft.Extensions.Logging;

namespace Karo.Api.Services;

public sealed class RoomLifecycleService
{
    private readonly LobbyService _lobbyService;
    private readonly GameService _gameService;
    private readonly RoomLifecycleOptions _options;
    private readonly bool _isDevelopment;
    private readonly ILogger<RoomLifecycleService>? _logger;

    public RoomLifecycleService(
        LobbyService lobbyService,
        GameService gameService,
        RoomLifecycleOptions? options = null,
        Microsoft.Extensions.Hosting.IHostEnvironment? environment = null,
        ILogger<RoomLifecycleService>? logger = null)
    {
        _lobbyService = lobbyService;
        _gameService = gameService;
        _options = options ?? new RoomLifecycleOptions();
        _isDevelopment = environment?.IsDevelopment() ?? true;
        _logger = logger;
    }

    public PlayerSessionResult CreateRoom(string connectionId, string playerName)
    {
        var result = _lobbyService.CreateRoomSession(connectionId, playerName);
        _logger?.LogInformation("Created room {RoomCode} for player {PlayerId}.", result.Room.RoomCode, result.Player.PlayerId);
        return result;
    }

    public PlayerSessionResult JoinRoom(string connectionId, string roomCode, string playerName)
    {
        var result = _lobbyService.JoinRoomSession(connectionId, roomCode, playerName);
        _logger?.LogInformation("Player {PlayerId} joined room {RoomCode}.", result.Player.PlayerId, result.Room.RoomCode);
        return result;
    }

    public PlayerSessionResult ResumeRoomSession(string connectionId, string roomCode, string playerId, string reconnectToken)
    {
        var result = _lobbyService.ResumeRoomSession(connectionId, roomCode, playerId, reconnectToken);
        var game = _gameService.SetPlayerConnectionStatus(result.Room.RoomCode, result.Player.PlayerId, PlayerConnectionStatus.Connected);
        if (game is not null)
        {
            _gameService.SyncRoomPlayerMetadata(result.Room);
            _gameService.ResumeFromReconnect(result.Room.RoomCode, result.Player.PlayerId);
            EnsureRequiredPlayerIsAvailable(result.Room, game);
        }

        _logger?.LogInformation("Player {PlayerId} resumed room {RoomCode}.", result.Player.PlayerId, result.Room.RoomCode);

        return result;
    }

    public PlayerSessionResult RecoverCurrentSession(string connectionId)
    {
        var result = _lobbyService.RecoverCurrentSession(connectionId);
        var game = _gameService.SetPlayerConnectionStatus(result.Room.RoomCode, result.Player.PlayerId, PlayerConnectionStatus.Connected);
        if (game is not null)
        {
            _gameService.SyncRoomPlayerMetadata(result.Room);
            _gameService.ResumeFromReconnect(result.Room.RoomCode, result.Player.PlayerId);
        }

        _logger?.LogInformation("Recovered an active browser session for player {PlayerId} in room {RoomCode}.", result.Player.PlayerId, result.Room.RoomCode);
        return result;
    }

    public LifecycleDisconnectResult Disconnect(string connectionId)
    {
        var result = _lobbyService.Disconnect(connectionId);
        if (result.Room is null || string.IsNullOrWhiteSpace(result.PlayerId))
        {
            return result;
        }

        var game = _gameService.SetPlayerConnectionStatus(result.Room.RoomCode, result.PlayerId, PlayerConnectionStatus.Reconnecting);
        if (game is not null)
        {
            _gameService.InvalidateTradeOffersForPlayer(result.Room.RoomCode, result.PlayerId);
            _gameService.SyncRoomPlayerMetadata(result.Room);
            EnsureRequiredPlayerIsAvailable(result.Room, game);
        }

        _logger?.LogInformation("Player {PlayerId} disconnected from room {RoomCode} and entered reconnect grace.", result.PlayerId, result.Room.RoomCode);

        return result;
    }

    public LifecycleDisconnectResult LeaveLobby(string connectionId, string roomCode)
    {
        var result = _lobbyService.LeaveLobby(connectionId, roomCode);
        _logger?.LogInformation("Player {PlayerId} left waiting room {RoomCode}.", result.PlayerId, result.RoomCode);
        return result;
    }

    public GameState ForfeitMatch(string connectionId, string roomCode)
    {
        var playerId = _lobbyService.GetPlayerForConnection(connectionId)?.PlayerId
            ?? throw new LobbyException("You are not connected to this room.");
        var room = _lobbyService.MarkForfeited(connectionId, roomCode);
        var game = _gameService.ForfeitPlayer(room.RoomCode, playerId, "Their placed pieces remain as neutral abandoned pieces.");
        _gameService.SyncRoomPlayerMetadata(room);
        FinishIfBelowMinimum(room, game, "The match ended because too few active players remain.");
        _logger?.LogInformation("Player {PlayerId} forfeited match in room {RoomCode}.", playerId, room.RoomCode);
        return game;
    }

    public GameState ContinueWithoutPlayer(string connectionId, string roomCode, string timedOutPlayerId)
    {
        var room = _lobbyService.ContinueWithoutPlayer(connectionId, roomCode, timedOutPlayerId);
        var game = _gameService.ForfeitPlayer(room.RoomCode, timedOutPlayerId, "Their reconnect window expired. Their placed pieces remain as neutral abandoned pieces.");
        _gameService.SyncRoomPlayerMetadata(room);
        FinishIfBelowMinimum(room, game, "The match ended because too few active players remain.");
        _logger?.LogInformation("Timed-out player {PlayerId} was continued without in room {RoomCode}.", timedOutPlayerId, room.RoomCode);
        return game;
    }

    public GameState EndPausedMatch(string connectionId, string roomCode)
    {
        var room = _lobbyService.GetRoomForConnection(connectionId)
            ?? throw new LobbyException("You are not connected to this room.");
        if (!string.Equals(room.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(room.HostPlayerId, _lobbyService.GetPlayerForConnection(connectionId)?.PlayerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new LobbyException("Only the room host can perform that action.", "NotRoomHost");
        }

        var game = _gameService.GetGame(room.RoomCode)
            ?? throw new GameRuleException("There is no active match to end.");
        if (game.Pause?.IsPaused != true)
        {
            throw new GameRuleException("The match is not waiting for a reconnect.");
        }

        if (game.Pause.ReconnectDeadline > DateTimeOffset.UtcNow)
        {
            throw new GameRuleException("The reconnect grace period is still active.");
        }

        var endedGame = _gameService.FinishAbandonedMatch(room.RoomCode, "The host ended the paused match.", _lobbyService.GetPlayerForConnection(connectionId)?.PlayerId);
        _logger?.LogInformation("Host ended paused match in room {RoomCode}.", room.RoomCode);
        return endedGame;
    }

    public LifecycleDisconnectResult ReturnToLobby(string connectionId, string roomCode)
    {
        var result = _lobbyService.ReturnToLobby(connectionId, roomCode);
        _gameService.RemoveGame(roomCode);
        _logger?.LogInformation("Room {RoomCode} returned to the waiting lobby.", roomCode);
        return result;
    }

    public Room MarkPostGame(GameState game)
    {
        return _lobbyService.MarkPostGame(game.RoomCode);
    }

    public GameState? AfterGameMutation(Room room, GameState game)
    {
        if (game.Status == GameStatus.Finished)
        {
            _lobbyService.MarkPostGame(room.RoomCode);
            return game;
        }

        EnsureRequiredPlayerIsAvailable(room, game);
        return _gameService.GetGame(room.RoomCode);
    }

    public IReadOnlyList<RoomCleanupResult> ProcessLifecycle()
    {
        var results = _lobbyService.ProcessLifecycle();
        foreach (var result in results)
        {
            if (result.RoomRemoved)
            {
                _gameService.RemoveGame(result.RoomCode);
                continue;
            }

            if (result.Room is null)
            {
                continue;
            }

            foreach (var playerId in result.TimedOutPlayerIds)
            {
                _logger?.LogWarning("Player {PlayerId} timed out in room {RoomCode}.", playerId, result.Room.RoomCode);
                var game = _gameService.SetPlayerConnectionStatus(result.Room.RoomCode, playerId, PlayerConnectionStatus.TimedOut);
                if (game is not null)
                {
                    EnsureRequiredPlayerIsAvailable(result.Room, game);
                }
            }

            _gameService.SyncRoomPlayerMetadata(result.Room);
        }

        return results;
    }

    private void EnsureRequiredPlayerIsAvailable(Room room, GameState game)
    {
        var requiredPlayerId = GetRequiredPlayerId(game);
        if (string.IsNullOrWhiteSpace(requiredPlayerId))
        {
            return;
        }

        var roomPlayer = room.Players.FirstOrDefault(player => string.Equals(player.PlayerId, requiredPlayerId, StringComparison.OrdinalIgnoreCase));
        if (roomPlayer?.ConnectionStatus == PlayerConnectionStatus.Connected)
        {
            return;
        }

        var deadline = roomPlayer?.DisconnectedAt is { } disconnectedAt
            ? disconnectedAt + _options.ReconnectGracePeriod
            : DateTimeOffset.UtcNow + _options.ReconnectGracePeriod;
        _gameService.PauseForReconnect(room.RoomCode, requiredPlayerId, deadline, "Required player is reconnecting");
    }

    private static string? GetRequiredPlayerId(GameState game)
    {
        if (game.Status == GameStatus.Finished)
        {
            return null;
        }

        if (game.PendingWardenAction == WardenAction.Discarding)
        {
            return game.PendingWardenDiscards.FirstOrDefault()?.PlayerId;
        }

        if (game.PendingWardenAction is WardenAction.MoveWarden or WardenAction.ChooseVictim)
        {
            return game.CurrentWardenPlayerId;
        }

        if (game.Phase == GamePhase.Setup)
        {
            return game.CurrentSetupPlayerId;
        }

        return game.CurrentPlayer.PlayerId;
    }

    private void FinishIfBelowMinimum(Room room, GameState game, string reason)
    {
        var minimumPlayers = _isDevelopment ? 1 : _options.MinimumPlayers;
        if (game.Players.Count(player => !player.HasForfeited) < minimumPlayers)
        {
            _gameService.FinishAbandonedMatch(room.RoomCode, reason);
            _lobbyService.MarkPostGame(room.RoomCode);
        }
    }
}
