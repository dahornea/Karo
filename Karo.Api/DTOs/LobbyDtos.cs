using Karo.Api.Models;
using Karo.Api.Services;

namespace Karo.Api.DTOs;

public sealed record JoinRoomResultDto(
    string PlayerId,
    RoomDto Room);

public sealed record RoomDto(
    string RoomCode,
    string HostConnectionId,
    string Status,
    IReadOnlyList<PlayerDto> Players);

public sealed record PlayerDto(
    string PlayerId,
    string PlayerName,
    string ConnectionId,
    DateTimeOffset JoinedAt,
    bool IsHost);

public sealed record GameStartedDto(
    string RoomCode,
    string Status,
    string Phase,
    BoardStateDto Board,
    IReadOnlyList<PlayerGameStateDto> Players,
    string CurrentPlayerId,
    string? CurrentSetupPlayerId,
    IReadOnlyList<string> PlayerOrder,
    string? SetupRound,
    string? SetupStep,
    string? SetupDirection,
    string? LastSetupCampVertexId,
    int TurnNumber,
    int? LastDiceRoll,
    bool HasRolledThisTurn,
    int WinningVictoryPoints,
    int DevelopmentDeckCount,
    string RobberTileId,
    string WardenTileId,
    string PendingWardenAction,
    string? CurrentWardenPlayerId,
    IReadOnlyList<WardenDiscardRequirementDto> PendingWardenDiscards,
    IReadOnlyList<string> WardenVictimOptions,
    string? LargestArmyPlayerId,
    string? WinnerPlayerId,
    ActiveDevelopmentCardEffectDto? ActiveDevelopmentCardEffect,
    IReadOnlyList<GameLogEntryDto> Log,
    DateTimeOffset StartedAt);

public sealed record GameStateDto(
    string RoomCode,
    string Status,
    string Phase,
    BoardStateDto Board,
    IReadOnlyList<PlayerGameStateDto> Players,
    string CurrentPlayerId,
    string? CurrentSetupPlayerId,
    IReadOnlyList<string> PlayerOrder,
    string? SetupRound,
    string? SetupStep,
    string? SetupDirection,
    string? LastSetupCampVertexId,
    int TurnNumber,
    int? LastDiceRoll,
    bool HasRolledThisTurn,
    int WinningVictoryPoints,
    int DevelopmentDeckCount,
    string RobberTileId,
    string WardenTileId,
    string PendingWardenAction,
    string? CurrentWardenPlayerId,
    IReadOnlyList<WardenDiscardRequirementDto> PendingWardenDiscards,
    IReadOnlyList<string> WardenVictimOptions,
    string? LargestArmyPlayerId,
    string? WinnerPlayerId,
    ActiveDevelopmentCardEffectDto? ActiveDevelopmentCardEffect,
    IReadOnlyList<GameLogEntryDto> Log,
    DateTimeOffset StartedAt);

public sealed record BoardStateDto(
    IReadOnlyList<HexTileDto> Tiles,
    IReadOnlyList<BoardVertexDto> Vertices,
    IReadOnlyList<BoardEdgeDto> Edges,
    IReadOnlyList<HarborSlotDto> HarborSlots,
    IReadOnlyList<PortDto> Ports);

public sealed record HexTileDto(
    string TileId,
    int Q,
    int R,
    string ResourceType,
    int? NumberToken,
    bool IsBlocked);

public sealed record BoardVertexDto(
    string VertexId,
    double X,
    double Y,
    bool IsCoastal,
    IReadOnlyList<string> AdjacentTileIds,
    string? OwnerPlayerId,
    string? StructureType);

public sealed record BoardEdgeDto(
    string EdgeId,
    string StartVertexId,
    string EndVertexId,
    string? OwnerPlayerId);

public sealed record HarborSlotDto(
    string HarborSlotId,
    IReadOnlyList<string> AdjacentVertexIds,
    string AdjacentEdgeId,
    int TileQ,
    int TileR,
    int EdgeIndex,
    double RenderX,
    double RenderY,
    double OrientationDegrees,
    string HarborType,
    int TradeRate);

public sealed record PortDto(
    string Id,
    string Type,
    string? ResourceType,
    int TileQ,
    int TileR,
    int EdgeIndex,
    IReadOnlyList<string> AdjacentVertexIds,
    string DisplayLabel);

public sealed record PlayerGameStateDto(
    string PlayerId,
    string PlayerName,
    bool IsHost,
    int SupplyCount,
    IReadOnlyDictionary<string, int> Supplies,
    int CampsBuilt,
    int StrongholdsBuilt,
    int TrailsBuilt,
    int VisibleVictoryPoints,
    int TotalVictoryPoints,
    bool HasLargestArmy,
    int PlayedKnightCount,
    bool HasPlayedDevelopmentCardThisTurn,
    int DevelopmentCardCount,
    IReadOnlyList<PlayerDevelopmentCardDto> DevelopmentCards,
    IReadOnlyList<string> AccessiblePortIds,
    IReadOnlyList<string> AccessibleHarborSlotIds,
    IReadOnlyList<BankTradeRateDto> TradeRates);

public sealed record BankTradeRateDto(
    string Resource,
    int Rate,
    string Source,
    string? PortId);

public sealed record PlayerDevelopmentCardDto(
    string CardId,
    string? Type,
    int PurchasedTurn,
    bool IsPlayed,
    string Status);

public sealed record ActiveDevelopmentCardEffectDto(
    string Type,
    string CardId,
    int FreeTrailsPlaced,
    int MaxFreeTrails);

public sealed record WardenDiscardRequirementDto(
    string PlayerId,
    int RequiredAmount);

public sealed record GameLogEntryDto(
    int Sequence,
    DateTimeOffset CreatedAt,
    string Message,
    string? PlayerId);

public static class LobbyDtoMapper
{
    public static RoomDto ToDto(this Room room)
    {
        return new RoomDto(
            room.RoomCode,
            room.HostConnectionId,
            room.Status.ToString(),
            room.Players
                .OrderBy(player => player.JoinedAt)
                .Select(player => player.ToDto())
                .ToList());
    }

    public static GameStartedDto ToGameStartedDto(this GameState game, string? viewerPlayerId)
    {
        return new GameStartedDto(
            game.RoomCode,
            game.Status.ToString(),
            game.Phase.ToString(),
            game.Board.ToDto(),
            game.Players.Select(player => player.ToDto(game, viewerPlayerId)).ToList(),
            game.CurrentPlayer.PlayerId,
            game.CurrentSetupPlayerId,
            game.PlayerOrder.ToList(),
            game.SetupRound?.ToString(),
            game.SetupStep?.ToString(),
            game.SetupDirection?.ToString(),
            game.LastSetupCampVertexId,
            game.TurnNumber,
            game.LastDiceRoll,
            game.HasRolledThisTurn,
            game.WinningVictoryPoints,
            game.DevelopmentDeck.Count,
            game.RobberTileId,
            game.WardenTileId,
            game.PendingWardenAction.ToString(),
            game.CurrentWardenPlayerId,
            game.PendingWardenDiscards.Select(discard => discard.ToDto()).ToList(),
            game.WardenVictimOptions.ToList(),
            game.LargestArmyPlayerId,
            game.WinnerPlayerId,
            game.CurrentPlayer.ActiveDevelopmentCardEffect?.ToDto(),
            game.Log.Select(entry => entry.ToDto()).ToList(),
            game.StartedAt);
    }

    public static GameStateDto ToDto(this GameState game, string? viewerPlayerId)
    {
        return new GameStateDto(
            game.RoomCode,
            game.Status.ToString(),
            game.Phase.ToString(),
            game.Board.ToDto(),
            game.Players.Select(player => player.ToDto(game, viewerPlayerId)).ToList(),
            game.CurrentPlayer.PlayerId,
            game.CurrentSetupPlayerId,
            game.PlayerOrder.ToList(),
            game.SetupRound?.ToString(),
            game.SetupStep?.ToString(),
            game.SetupDirection?.ToString(),
            game.LastSetupCampVertexId,
            game.TurnNumber,
            game.LastDiceRoll,
            game.HasRolledThisTurn,
            game.WinningVictoryPoints,
            game.DevelopmentDeck.Count,
            game.RobberTileId,
            game.WardenTileId,
            game.PendingWardenAction.ToString(),
            game.CurrentWardenPlayerId,
            game.PendingWardenDiscards.Select(discard => discard.ToDto()).ToList(),
            game.WardenVictimOptions.ToList(),
            game.LargestArmyPlayerId,
            game.WinnerPlayerId,
            game.CurrentPlayer.ActiveDevelopmentCardEffect?.ToDto(),
            game.Log.Select(entry => entry.ToDto()).ToList(),
            game.StartedAt);
    }

    private static PlayerDto ToDto(this Player player)
    {
        return new PlayerDto(
            player.PlayerId,
            player.PlayerName,
            player.ConnectionId,
            player.JoinedAt,
            player.IsHost);
    }

    private static BoardStateDto ToDto(this BoardState board)
    {
        return new BoardStateDto(
            board.Tiles
                .OrderBy(tile => tile.R)
                .ThenBy(tile => tile.Q)
                .Select(tile => new HexTileDto(
                    tile.TileId,
                    tile.Q,
                    tile.R,
                    tile.ResourceType.ToString(),
                    tile.NumberToken,
                    tile.IsBlocked))
                .ToList(),
            board.Vertices
                .OrderBy(vertex => vertex.VertexId)
                .Select(vertex => new BoardVertexDto(
                    vertex.VertexId,
                    vertex.X,
                    vertex.Y,
                    vertex.IsCoastal,
                    vertex.AdjacentTileIds.ToList(),
                    vertex.OwnerPlayerId,
                    vertex.StructureType?.ToString()))
                .ToList(),
            board.Edges
                .OrderBy(edge => edge.EdgeId)
                .Select(edge => new BoardEdgeDto(
                    edge.EdgeId,
                    edge.StartVertexId,
                    edge.EndVertexId,
                    edge.OwnerPlayerId))
                .ToList(),
            board.HarborSlots
                .OrderBy(slot => slot.HarborSlotId)
                .Select(slot => new HarborSlotDto(
                    slot.HarborSlotId,
                    slot.AdjacentVertexIds.ToList(),
                    slot.AdjacentEdgeId,
                    slot.TileQ,
                    slot.TileR,
                    slot.EdgeIndex,
                    slot.RenderX,
                    slot.RenderY,
                    slot.OrientationDegrees,
                    slot.HarborType?.ToString() ?? throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} has no assigned type."),
                    slot.TradeRate ?? throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} has no assigned trade rate.")))
                .ToList(),
            board.Ports
                .OrderBy(port => port.Id)
                .Select(port => new PortDto(
                    port.Id,
                    port.Type.ToString(),
                    port.ResourceType?.ToString(),
                    port.TileQ,
                    port.TileR,
                    port.EdgeIndex,
                    port.AdjacentVertexIds.ToList(),
                    port.DisplayLabel))
                .ToList());
    }

    private static PlayerGameStateDto ToDto(this PlayerGameState player, GameState game, string? viewerPlayerId)
    {
        var isOwner = player.PlayerId == viewerPlayerId;
        var revealHidden = isOwner || game.Status == GameStatus.Finished;
        var supplies = isOwner || game.Status == GameStatus.Finished
            ? player.Supplies.ToDictionary(item => item.Key.ToString(), item => item.Value)
            : ResourceTypes.All.ToDictionary(resource => resource.ToString(), _ => 0);
        var accessiblePortIds = GameService.GetPlayerPorts(game, player.PlayerId)
            .Select(port => port.Id)
            .ToList();
        var accessibleHarborSlotIds = GameService.GetPlayerHarborSlots(game, player.PlayerId)
            .Select(slot => slot.HarborSlotId)
            .ToList();
        var tradeRates = GameService.GetTradeRates(game, player.PlayerId)
            .Select(rate => rate.ToDto())
            .ToList();

        return new PlayerGameStateDto(
            player.PlayerId,
            player.PlayerName,
            player.IsHost,
            player.Supplies.Values.Sum(),
            supplies,
            player.CampsBuilt,
            player.StrongholdsBuilt,
            player.TrailsBuilt,
            GameService.CalculateVictoryPoints(game, player, revealHidden: false),
            GameService.CalculateVictoryPoints(game, player, revealHidden),
            game.LargestArmyPlayerId == player.PlayerId,
            player.PlayedKnightCount,
            player.HasPlayedDevelopmentCardThisTurn,
            player.DevelopmentCards.Count,
            isOwner || game.Status == GameStatus.Finished
                ? player.DevelopmentCards.Select(card => card.ToDto(game, isOwner)).ToList()
                : [],
            accessiblePortIds,
            accessibleHarborSlotIds,
            tradeRates);
    }

    private static BankTradeRateDto ToDto(this BankTradeRate rate)
    {
        return new BankTradeRateDto(
            rate.Resource.ToString(),
            rate.Rate,
            rate.Source.ToString(),
            rate.PortId);
    }

    private static PlayerDevelopmentCardDto ToDto(this PlayerDevelopmentCard card, GameState game, bool isOwner)
    {
        return new PlayerDevelopmentCardDto(
            card.CardId,
            isOwner || game.Status == GameStatus.Finished ? card.Type.ToString() : null,
            card.PurchasedTurn,
            card.IsPlayed,
            GetCardStatus(card, game));
    }

    private static ActiveDevelopmentCardEffectDto ToDto(this ActiveDevelopmentCardEffect effect)
    {
        return new ActiveDevelopmentCardEffectDto(
            effect.Type.ToString(),
            effect.CardId,
            effect.FreeTrailsPlaced,
            effect.MaxFreeTrails);
    }

    private static GameLogEntryDto ToDto(this GameLogEntry entry)
    {
        return new GameLogEntryDto(
            entry.Sequence,
            entry.CreatedAt,
            entry.Message,
            entry.PlayerId);
    }

    private static WardenDiscardRequirementDto ToDto(this WardenDiscardRequirement discard)
    {
        return new WardenDiscardRequirementDto(
            discard.PlayerId,
            discard.RequiredAmount);
    }

    private static string GetCardStatus(PlayerDevelopmentCard card, GameState game)
    {
        if (card.Type == DevelopmentCardType.VictoryPoint)
        {
            return "HiddenVictoryPoint";
        }

        if (card.IsPlayed)
        {
            return "AlreadyPlayed";
        }

        if (card.PurchasedTurn == game.TurnNumber)
        {
            return "BoughtThisTurn";
        }

        return "Playable";
    }
}
