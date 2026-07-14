using Karo.Api.DTOs;
using Karo.Api.Models;
using Karo.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Karo.Api.Hubs;

public sealed class GameLobbyHub : Hub
{
    private readonly LobbyService _lobbyService;
    private readonly GameService _gameService;
    private readonly RoomLifecycleService _roomLifecycleService;
    private readonly DebugGameService _debugGameService;
    private readonly ILogger<GameLobbyHub> _logger;

    public GameLobbyHub(
        LobbyService lobbyService,
        GameService gameService,
        RoomLifecycleService roomLifecycleService,
        DebugGameService debugGameService,
        ILogger<GameLobbyHub> logger)
    {
        _lobbyService = lobbyService;
        _gameService = gameService;
        _roomLifecycleService = roomLifecycleService;
        _debugGameService = debugGameService;
        _logger = logger;
    }

    public async Task<JoinRoomResultDto> CreateRoom(string playerName)
    {
        try
        {
            var result = _roomLifecycleService.CreateRoom(Context.ConnectionId, playerName);
            var room = result.Room;
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            var roomDto = room.ToDto();
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", roomDto);
            return new JoinRoomResultDto(
                result.Player.PlayerId,
                roomDto,
                new PlayerSessionDto(room.RoomCode, result.Player.PlayerId, result.ReconnectToken));
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task<JoinRoomResultDto> JoinRoom(string roomCode, string playerName)
    {
        try
        {
            var result = _roomLifecycleService.JoinRoom(Context.ConnectionId, roomCode, playerName);
            var room = result.Room;
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            var roomDto = room.ToDto();
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", roomDto);
            return new JoinRoomResultDto(
                result.Player.PlayerId,
                roomDto,
                new PlayerSessionDto(room.RoomCode, result.Player.PlayerId, result.ReconnectToken));
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task StartGame(string roomCode)
    {
        Room? startedRoom = null;
        try
        {
            var room = _lobbyService.StartGame(Context.ConnectionId, roomCode);
            startedRoom = room;
            var game = _gameService.StartGame(room);
            _logger.LogInformation("Started match {MatchId} in room {RoomCode} with host {HostPlayerId}.", game.MatchId, room.RoomCode, room.HostPlayerId);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
            await BroadcastGameState(room, game, "GameStarted");
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            if (startedRoom is not null)
            {
                var rolledBackRoom = _lobbyService.RollbackGameStart(Context.ConnectionId, startedRoom.RoomCode);
                await Clients.Group(rolledBackRoom.RoomCode).SendAsync("RoomUpdated", rolledBackRoom.ToDto());
            }

            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task SetReady(string roomCode, bool isReady)
    {
        try
        {
            var room = _lobbyService.SetReady(Context.ConnectionId, roomCode, isReady);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task LeaveRoom(string roomCode)
    {
        try
        {
            var result = _roomLifecycleService.LeaveLobby(Context.ConnectionId, roomCode);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            if (result.RoomRemoved)
            {
                await Clients.Group(roomCode).SendAsync("RoomClosed", roomCode);
            }
            else if (result.Room is not null)
            {
                await Clients.Group(result.Room.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
            }
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task ForfeitMatch(string roomCode)
    {
        try
        {
            var game = _roomLifecycleService.ForfeitMatch(Context.ConnectionId, roomCode);
            var room = _lobbyService.GetRoom(roomCode)
                ?? throw new LobbyException("This room no longer exists.", "RoomNotFound");
            game = _roomLifecycleService.AfterGameMutation(room, game) ?? game;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
            await BroadcastGameState(room, game, "GameUpdated");
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task ContinueWithoutPlayer(string roomCode, string timedOutPlayerId)
    {
        try
        {
            var game = _roomLifecycleService.ContinueWithoutPlayer(Context.ConnectionId, roomCode, timedOutPlayerId);
            var room = _lobbyService.GetRoom(roomCode)
                ?? throw new LobbyException("This room no longer exists.", "RoomNotFound");
            game = _roomLifecycleService.AfterGameMutation(room, game) ?? game;
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
            await BroadcastGameState(room, game, "GameUpdated");
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task EndPausedMatch(string roomCode)
    {
        try
        {
            var game = _roomLifecycleService.EndPausedMatch(Context.ConnectionId, roomCode);
            var room = _roomLifecycleService.MarkPostGame(game);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
            await BroadcastGameState(room, game, "GameUpdated");
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task ReturnRoomToLobby(string roomCode)
    {
        try
        {
            var result = _roomLifecycleService.ReturnToLobby(Context.ConnectionId, roomCode);
            if (result.RoomRemoved)
            {
                await Clients.Group(roomCode).SendAsync("RoomClosed", roomCode);
                return;
            }

            if (result.Room is not null)
            {
                await Clients.Group(result.Room.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
                await Clients.Group(result.Room.RoomCode).SendAsync("ReturnedToLobby", result.Room.ToDto());
            }
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task<ResumeRoomSessionResultDto> ResumeRoomSession(string roomCode, string playerId, string reconnectToken)
    {
        try
        {
            var result = _roomLifecycleService.ResumeRoomSession(Context.ConnectionId, roomCode, playerId, reconnectToken);
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Room.RoomCode);
            if (!string.IsNullOrWhiteSpace(result.ReplacedConnectionId))
            {
                await Clients.Client(result.ReplacedConnectionId).SendAsync("SessionReplaced");
                await Groups.RemoveFromGroupAsync(result.ReplacedConnectionId, result.Room.RoomCode);
            }

            await Clients.Group(result.Room.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
            var game = _gameService.GetGame(result.Room.RoomCode);
            if (game is not null)
            {
                await BroadcastGameState(result.Room, game, "GameUpdated");
            }

            return new ResumeRoomSessionResultDto(
                result.Room.ToDto(),
                game?.ToDto(result.Player.PlayerId),
                new PlayerSessionDto(result.Room.RoomCode, result.Player.PlayerId, result.ReconnectToken));
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task<ResumeRoomSessionResultDto> RecoverCurrentSession()
    {
        try
        {
            var result = _roomLifecycleService.RecoverCurrentSession(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Room.RoomCode);
            await Clients.Group(result.Room.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
            var game = _gameService.GetGame(result.Room.RoomCode);
            if (game is not null)
            {
                await BroadcastGameState(result.Room, game, "GameUpdated");
            }

            return new ResumeRoomSessionResultDto(
                result.Room.ToDto(),
                game?.ToDto(result.Player.PlayerId),
                new PlayerSessionDto(result.Room.RoomCode, result.Player.PlayerId, result.ReconnectToken));
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public GameStateDto? GetGameState(string roomCode)
    {
        var room = _lobbyService.GetRoomForConnection(Context.ConnectionId);
        var player = _lobbyService.GetPlayerForConnection(Context.ConnectionId);
        return room is not null && string.Equals(room.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase)
            ? _gameService.GetGame(roomCode)?.ToDto(player?.PlayerId)
            : null;
    }

    public async Task BuyDevelopmentCard(string roomCode)
    {
        await RunGameAction(roomCode, (game, player) => _gameService.BuyDevelopmentCard(roomCode, player.PlayerId));
    }

    public async Task TradeWithBank(string roomCode, string offeredResource, string requestedResource)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.TradeWithBank(roomCode, player.PlayerId, ParseTradeResource(offeredResource), ParseTradeResource(requestedResource)));
    }

    public async Task MaritimeTrade(string roomCode, string giveResource, string receiveResource)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.MaritimeTrade(roomCode, player.PlayerId, ParseTradeResource(giveResource), ParseTradeResource(receiveResource)));
    }

    public async Task CreateTradeOffer(
        string roomCode,
        string targetPlayerId,
        IReadOnlyDictionary<string, int> offeredResources,
        IReadOnlyDictionary<string, int> requestedResources)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.CreateTradeOffer(
                roomCode,
                player.PlayerId,
                targetPlayerId,
                ParseResourceMap(offeredResources),
                ParseResourceMap(requestedResources)));
    }

    public async Task AcceptTradeOffer(string roomCode, string tradeOfferId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.AcceptTradeOffer(roomCode, player.PlayerId, tradeOfferId));
    }

    public async Task RejectTradeOffer(string roomCode, string tradeOfferId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.RejectTradeOffer(roomCode, player.PlayerId, tradeOfferId));
    }

    public async Task CancelTradeOffer(string roomCode, string tradeOfferId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.CancelTradeOffer(roomCode, player.PlayerId, tradeOfferId));
    }

    public async Task EndTurn(string roomCode)
    {
        await RunGameAction(roomCode, (game, player) => _gameService.EndTurn(roomCode, player.PlayerId));
    }

    public async Task PlaceSetupCamp(string roomCode, string vertexId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlaceSetupCamp(roomCode, player.PlayerId, vertexId));
    }

    public async Task PlaceSetupTrail(string roomCode, string edgeId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlaceSetupTrail(roomCode, player.PlayerId, edgeId));
    }

    public async Task BuildTrail(string roomCode, string edgeId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.BuildTrail(roomCode, player.PlayerId, edgeId));
    }

    public async Task BuildCamp(string roomCode, string vertexId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.BuildCamp(roomCode, player.PlayerId, vertexId));
    }

    public async Task BuildStronghold(string roomCode, string vertexId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.BuildStronghold(roomCode, player.PlayerId, vertexId));
    }

    public async Task RollDice(string roomCode)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.RollDice(roomCode, player.PlayerId));
    }

    public async Task DiscardForWarden(string roomCode, IReadOnlyDictionary<string, int> discardedResources)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.DiscardForWarden(roomCode, player.PlayerId, ParseResourceMap(discardedResources)));
    }

    public async Task MoveWarden(string roomCode, string targetTileId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.MoveWarden(roomCode, player.PlayerId, targetTileId));
    }

    public async Task StealFromWardenVictim(string roomCode, string victimPlayerId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.StealFromWardenVictim(roomCode, player.PlayerId, victimPlayerId));
    }

    public async Task PlayYearOfPlenty(string roomCode, string cardId, IReadOnlyList<string> selectedResources)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlayYearOfPlenty(roomCode, player.PlayerId, cardId, ParseResources(selectedResources, expectedCount: 2)));
    }

    public async Task PlayMonopoly(string roomCode, string cardId, string selectedResource)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlayMonopoly(roomCode, player.PlayerId, cardId, ParseResource(selectedResource)));
    }

    public async Task PlayKnight(string roomCode, string cardId, string targetTileId, string? victimPlayerId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlayKnight(roomCode, player.PlayerId, cardId, targetTileId, victimPlayerId));
    }

    public async Task StartRoadBuilding(string roomCode, string cardId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.StartRoadBuilding(roomCode, player.PlayerId, cardId));
    }

    public async Task PlaceFreeTrail(string roomCode, string edgeId)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.PlaceFreeTrail(roomCode, player.PlayerId, edgeId));
    }

    public async Task CancelActiveDevelopmentCard(string roomCode)
    {
        await RunGameAction(roomCode, (_, player) =>
            _gameService.CancelActiveDevelopmentCard(roomCode, player.PlayerId));
    }

    public async Task DebugAddResource(string roomCode, string playerId, string resourceType, int amount)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.AddResource(room, actor, playerId, ParseResource(resourceType), amount));
    }

    public async Task DebugSetResources(string roomCode, string playerId, IReadOnlyDictionary<string, int> resources)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.SetResources(room, actor, playerId, ParseResourceMap(resources)));
    }

    public async Task DebugSetTestingResources(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.SetTestingResources(room, actor, playerId));
    }

    public async Task DebugClearResources(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ClearResources(room, actor, playerId));
    }

    public async Task DebugSetCurrentPlayer(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.SetCurrentPlayer(room, actor, playerId));
    }

    public async Task DebugForceDiceRoll(string roomCode, int diceValue)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ForceDiceRoll(room, actor, diceValue));
    }

    public async Task DebugResetRollState(string roomCode)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ResetRollState(room, actor));
    }

    public async Task DebugMoveWarden(string roomCode, string targetTileId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.MoveWarden(room, actor, targetTileId));
    }

    public async Task DebugClearWardenState(string roomCode)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ClearWardenState(room, actor));
    }

    public async Task DebugSkipSetup(string roomCode)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.SkipSetup(room, actor));
    }

    public async Task DebugForceSetupStep(string roomCode, string playerId, string setupStep)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ForceSetupStep(room, actor, playerId, ParseSetupStep(setupStep)));
    }

    public async Task DebugSetVictoryPoints(string roomCode, string playerId, int points)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.SetVictoryPoints(room, actor, playerId, points));
    }

    public async Task DebugTriggerWinCheck(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.TriggerWinCheck(room, actor, playerId));
    }

    public async Task DebugRecalculateLongestTrail(string roomCode)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.RecalculateLongestTrail(room, actor));
    }

    public async Task DebugGiveDevelopmentCard(string roomCode, string playerId, string cardType)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.GiveDevelopmentCard(room, actor, playerId, ParseDebugDevelopmentCardType(cardType)));
    }

    public async Task DebugClearDevelopmentCards(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ClearDevelopmentCards(room, actor, playerId));
    }

    public async Task DebugResetDevelopmentCardPlayLimit(string roomCode, string playerId)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.ResetDevelopmentCardPlayLimit(room, actor, playerId));
    }

    public IReadOnlyDictionary<string, int> DebugGetDevelopmentDeckComposition(string roomCode)
    {
        try
        {
            var (room, player) = GetRoomPlayerOrThrow(roomCode);
            return _debugGameService.GetDevelopmentDeckComposition(room, player);
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task DebugRestartMatch(string roomCode)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.RestartMatch(room, actor));
    }

    public async Task DebugRegenerateBoard(string roomCode, int boardSeed)
    {
        await RunDebugAction(roomCode, (room, actor) =>
            _debugGameService.RegenerateBoard(room, actor, boardSeed));
    }

    public BoardIntegrityResultDto DebugValidateBoard(string roomCode)
    {
        try
        {
            var (room, player) = GetRoomPlayerOrThrow(roomCode);
            return _debugGameService.ValidateBoard(room, player).ToDto();
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var result = _roomLifecycleService.Disconnect(Context.ConnectionId);

        if (!string.IsNullOrWhiteSpace(result.RoomCode))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, result.RoomCode);

            if (result.Room is not null)
            {
                await Clients.Group(result.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
                var game = _gameService.GetGame(result.RoomCode);
                if (game is not null)
                {
                    await BroadcastGameState(result.Room, game, "GameUpdated");
                }
            }
            else if (result.RoomRemoved)
            {
                _gameService.RemoveGame(result.RoomCode);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RunGameAction(string roomCode, Func<GameState, Player, GameState> action)
    {
        try
        {
            var (room, player) = GetRoomPlayerOrThrow(roomCode);
            var currentGame = _gameService.GetGame(roomCode)
                ?? throw new GameRuleException("No active match exists for this room.");

            var updatedGame = action(currentGame, player);
            updatedGame = _roomLifecycleService.AfterGameMutation(room, updatedGame) ?? updatedGame;
            GameService.MarkStateChanged(updatedGame);
            var updatedRoom = _lobbyService.GetRoom(roomCode) ?? room;
            await Clients.Group(updatedRoom.RoomCode).SendAsync("RoomUpdated", updatedRoom.ToDto());
            await BroadcastGameState(updatedRoom, updatedGame, "GameUpdated");
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    private async Task RunDebugAction(string roomCode, Func<Room, Player, GameState> action)
    {
        try
        {
            var (room, player) = GetRoomPlayerOrThrow(roomCode);
            var updatedGame = action(room, player);
            updatedGame = _roomLifecycleService.AfterGameMutation(room, updatedGame) ?? updatedGame;
            GameService.MarkStateChanged(updatedGame);
            var updatedRoom = _lobbyService.GetRoom(roomCode) ?? room;
            await Clients.Group(updatedRoom.RoomCode).SendAsync("RoomUpdated", updatedRoom.ToDto());
            await BroadcastGameState(updatedRoom, updatedGame, "GameUpdated");
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    private (Room Room, Player Player) GetRoomPlayerOrThrow(string roomCode)
    {
        var room = _lobbyService.GetRoomForConnection(Context.ConnectionId)
            ?? throw new GameRuleException(_lobbyService.IsStaleConnection(Context.ConnectionId)
                ? "This player session is active in another browser tab or window."
                : "You are not connected to a room.");

        if (!string.Equals(room.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("You are not connected to that room.");
        }

        var player = _lobbyService.GetPlayerForConnection(Context.ConnectionId)
            ?? throw new GameRuleException("You are not in this room.");

        return (room, player);
    }

    private async Task BroadcastGameState(Room room, GameState game, string eventName)
    {
        foreach (var player in room.Players)
        {
            if (string.IsNullOrWhiteSpace(player.ConnectionId)
                || player.ConnectionStatus != PlayerConnectionStatus.Connected)
            {
                continue;
            }

            await Clients.Client(player.ConnectionId).SendAsync(eventName, eventName == "GameStarted"
                ? game.ToGameStartedDto(player.PlayerId)
                : game.ToDto(player.PlayerId));
        }
    }

    private static IReadOnlyList<ResourceType> ParseResources(IReadOnlyList<string> resources, int expectedCount)
    {
        if (resources.Count != expectedCount)
        {
            throw new GameRuleException($"Choose exactly {expectedCount} resources.");
        }

        return resources.Select(ParseResource).ToList();
    }

    private static ResourceType ParseResource(string resource)
    {
        return Enum.TryParse<ResourceType>(resource, ignoreCase: true, out var parsed)
            ? parsed
            : throw new GameRuleException("Choose a valid resource.");
    }

    private static ResourceType ParseTradeResource(string resource)
    {
        return Enum.TryParse<ResourceType>(resource, ignoreCase: true, out var parsed)
            ? parsed
            : throw new GameRuleException("Invalid trade resources.");
    }

    private static IReadOnlyDictionary<ResourceType, int> ParseResourceMap(IReadOnlyDictionary<string, int> resources)
    {
        return resources.ToDictionary(item => ParseResource(item.Key), item => item.Value);
    }

    private static DevelopmentCardType? ParseDebugDevelopmentCardType(string cardType)
    {
        if (string.Equals(cardType, "Random", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<DevelopmentCardType>(cardType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new GameRuleException("Choose a valid development card type.");
    }

    private static SetupStep ParseSetupStep(string setupStep)
    {
        return Enum.TryParse<SetupStep>(setupStep, ignoreCase: true, out var parsed)
            ? parsed
            : throw new GameRuleException("Choose a valid setup step.");
    }
}
