using Karo.Api.DTOs;
using Karo.Api.Models;
using Karo.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Karo.Api.Hubs;

public sealed class GameLobbyHub : Hub
{
    private readonly LobbyService _lobbyService;
    private readonly GameService _gameService;
    private readonly DebugGameService _debugGameService;

    public GameLobbyHub(LobbyService lobbyService, GameService gameService, DebugGameService debugGameService)
    {
        _lobbyService = lobbyService;
        _gameService = gameService;
        _debugGameService = debugGameService;
    }

    public async Task<JoinRoomResultDto> CreateRoom(string playerName)
    {
        try
        {
            var room = _lobbyService.CreateRoom(Context.ConnectionId, playerName);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            var roomDto = room.ToDto();
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", roomDto);

            var currentPlayer = room.Players.Single(player => player.ConnectionId == Context.ConnectionId);
            return new JoinRoomResultDto(currentPlayer.PlayerId, roomDto);
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
            var room = _lobbyService.JoinRoom(Context.ConnectionId, roomCode, playerName);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            var roomDto = room.ToDto();
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", roomDto);

            var currentPlayer = room.Players.Single(player => player.ConnectionId == Context.ConnectionId);
            return new JoinRoomResultDto(currentPlayer.PlayerId, roomDto);
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public async Task StartGame(string roomCode)
    {
        try
        {
            var room = _lobbyService.StartGame(Context.ConnectionId, roomCode);
            var game = _gameService.StartGame(room);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room.ToDto());
            await BroadcastGameState(room, game, "GameStarted");
        }
        catch (LobbyException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    public GameStateDto? GetGameState(string roomCode)
    {
        var room = _lobbyService.GetRoomForConnection(Context.ConnectionId);
        var player = room?.Players.FirstOrDefault(player => player.ConnectionId == Context.ConnectionId);
        return _gameService.GetGame(roomCode)?.ToDto(player?.PlayerId);
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var result = _lobbyService.Disconnect(Context.ConnectionId);

        if (!string.IsNullOrWhiteSpace(result.RoomCode))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, result.RoomCode);

            if (result.Room is not null)
            {
                await Clients.Group(result.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto());
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
            await BroadcastGameState(room, updatedGame, "GameUpdated");
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
            await BroadcastGameState(room, updatedGame, "GameUpdated");
        }
        catch (GameRuleException ex)
        {
            throw HubErrorSerializer.ToHubException(ex);
        }
    }

    private (Room Room, Player Player) GetRoomPlayerOrThrow(string roomCode)
    {
        var room = _lobbyService.GetRoomForConnection(Context.ConnectionId)
            ?? throw new GameRuleException("You are not connected to a room.");

        if (!string.Equals(room.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("You are not connected to that room.");
        }

        var player = room.Players.FirstOrDefault(player => player.ConnectionId == Context.ConnectionId)
            ?? throw new GameRuleException("You are not in this room.");

        return (room, player);
    }

    private async Task BroadcastGameState(Room room, GameState game, string eventName)
    {
        foreach (var player in room.Players)
        {
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
