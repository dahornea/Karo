using Karo.Api.Models;
using Karo.Api.Services;

namespace Karo.Api.DTOs;

public sealed record JoinRoomResultDto(
    string PlayerId,
    RoomDto Room,
    PlayerSessionDto Session);

public sealed record PlayerSessionDto(
    string RoomCode,
    string PlayerId,
    string ReconnectToken);

public sealed record ResumeRoomSessionResultDto(
    RoomDto Room,
    GameStateDto? Game,
    PlayerSessionDto Session);

public sealed record RoomDto(
    string RoomCode,
    string HostPlayerId,
    string Status,
    long RoomStateVersion,
    IReadOnlyList<PlayerDto> Players);

public sealed record PlayerDto(
    string PlayerId,
    string PlayerName,
    string ConnectionStatus,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? DisconnectedAt,
    bool IsHost,
    bool IsReady,
    bool HasForfeited,
    string PlayerColor);

public sealed record GameStartedDto(
    string RoomCode,
    string Status,
    string MatchId,
    long GameStateVersion,
    GamePauseDto? Pause,
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
    int LargestArmyKnightCount,
    int? LargestArmyAwardedAtTurn,
    string? LongestTrailPlayerId,
    int LongestTrailLength,
    string? WinnerPlayerId,
    ActiveDevelopmentCardEffectDto? ActiveDevelopmentCardEffect,
    IReadOnlyList<PlayerTradeOfferDto> TradeOffers,
    IReadOnlyList<GameLogEntryDto> Log,
    DateTimeOffset StartedAt);

public sealed record GameStateDto(
    string RoomCode,
    string Status,
    string MatchId,
    long GameStateVersion,
    GamePauseDto? Pause,
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
    int LargestArmyKnightCount,
    int? LargestArmyAwardedAtTurn,
    string? LongestTrailPlayerId,
    int LongestTrailLength,
    string? WinnerPlayerId,
    ActiveDevelopmentCardEffectDto? ActiveDevelopmentCardEffect,
    IReadOnlyList<PlayerTradeOfferDto> TradeOffers,
    IReadOnlyList<GameLogEntryDto> Log,
    DateTimeOffset StartedAt);

public sealed record BoardStateDto(
    int BoardSeed,
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
    bool IsBlocked,
    IReadOnlyList<string> AdjacentTileIds);

public sealed record BoardVertexDto(
    string VertexId,
    double X,
    double Y,
    bool IsCoastal,
    IReadOnlyList<string> AdjacentTileIds,
    IReadOnlyList<string> AdjacentVertexIds,
    IReadOnlyList<string> AdjacentEdgeIds,
    string? OwnerPlayerId,
    string? StructureType);

public sealed record BoardEdgeDto(
    string EdgeId,
    string StartVertexId,
    string EndVertexId,
    IReadOnlyList<string> AdjacentTileIds,
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

public sealed record BoardIntegrityResultDto(
    int BoardSeed,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record PlayerGameStateDto(
    string PlayerId,
    string PlayerName,
    bool IsHost,
    string ConnectionStatus,
    bool HasForfeited,
    string PlayerColor,
    int SupplyCount,
    IReadOnlyDictionary<string, int> Supplies,
    int CampsBuilt,
    int StrongholdsBuilt,
    int TrailsBuilt,
    int TotalTrails,
    int RemainingTrails,
    int TotalCamps,
    int RemainingCamps,
    int TotalStrongholds,
    int RemainingStrongholds,
    int VisibleVictoryPoints,
    int TotalVictoryPoints,
    bool HasLargestArmy,
    bool HasLongestTrail,
    int LongestTrailLength,
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

public sealed record PlayerTradeOfferDto(
    string TradeOfferId,
    string RoomCode,
    int TurnNumber,
    string ProposerPlayerId,
    string ProposerName,
    string TargetPlayerId,
    string TargetName,
    IReadOnlyDictionary<string, int> OfferedResources,
    IReadOnlyDictionary<string, int> RequestedResources,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    bool CanAccept,
    bool CanReject,
    bool CanCancel);

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

public sealed record GamePauseDto(
    bool IsPaused,
    string Reason,
    string DisconnectedPlayerId,
    DateTimeOffset PausedAt,
    DateTimeOffset ReconnectDeadline);

public static class LobbyDtoMapper
{
    public static BoardIntegrityResultDto ToDto(this BoardValidationResult result)
    {
        return new BoardIntegrityResultDto(
            result.BoardSeed,
            result.IsValid,
            result.Errors,
            result.Warnings);
    }

    public static RoomDto ToDto(this Room room)
    {
        return new RoomDto(
            room.RoomCode,
            room.HostPlayerId,
            room.Status.ToString(),
            room.RoomStateVersion,
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
            game.MatchId,
            game.GameStateVersion,
            game.Pause?.ToDto(),
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
            game.LargestArmyKnightCount,
            game.LargestArmyAwardedAtTurn,
            game.LongestTrailPlayerId,
            game.LongestTrailLength,
            game.WinnerPlayerId,
            game.CurrentPlayer.ActiveDevelopmentCardEffect?.ToDto(),
            game.TradeOffers.Select(offer => offer.ToDto(game, viewerPlayerId)).ToList(),
            game.Log.Select(entry => entry.ToDto()).ToList(),
            game.StartedAt);
    }

    public static GameStateDto ToDto(this GameState game, string? viewerPlayerId)
    {
        return new GameStateDto(
            game.RoomCode,
            game.Status.ToString(),
            game.MatchId,
            game.GameStateVersion,
            game.Pause?.ToDto(),
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
            game.LargestArmyKnightCount,
            game.LargestArmyAwardedAtTurn,
            game.LongestTrailPlayerId,
            game.LongestTrailLength,
            game.WinnerPlayerId,
            game.CurrentPlayer.ActiveDevelopmentCardEffect?.ToDto(),
            game.TradeOffers.Select(offer => offer.ToDto(game, viewerPlayerId)).ToList(),
            game.Log.Select(entry => entry.ToDto()).ToList(),
            game.StartedAt);
    }

    private static PlayerDto ToDto(this Player player)
    {
        return new PlayerDto(
            player.PlayerId,
            player.PlayerName,
            player.ConnectionStatus.ToString(),
            player.JoinedAt,
            player.LastSeenAt,
            player.DisconnectedAt,
            player.IsHost,
            player.IsReady,
            player.HasForfeited,
            player.PlayerColor);
    }

    private static BoardStateDto ToDto(this BoardState board)
    {
        return new BoardStateDto(
            board.BoardSeed,
            board.Tiles
                .OrderBy(tile => tile.R)
                .ThenBy(tile => tile.Q)
                .Select(tile => new HexTileDto(
                    tile.TileId,
                    tile.Q,
                    tile.R,
                    tile.ResourceType.ToString(),
                    tile.NumberToken,
                    tile.IsBlocked,
                    tile.AdjacentTileIds.ToList()))
                .ToList(),
            board.Vertices
                .OrderBy(vertex => vertex.VertexId)
                .Select(vertex => new BoardVertexDto(
                    vertex.VertexId,
                    vertex.X,
                    vertex.Y,
                    vertex.IsCoastal,
                    vertex.AdjacentTileIds.ToList(),
                    vertex.AdjacentVertexIds.ToList(),
                    vertex.AdjacentEdgeIds.ToList(),
                    vertex.OwnerPlayerId,
                    vertex.StructureType?.ToString()))
                .ToList(),
            board.Edges
                .OrderBy(edge => edge.EdgeId)
                .Select(edge => new BoardEdgeDto(
                    edge.EdgeId,
                    edge.StartVertexId,
                    edge.EndVertexId,
                    edge.AdjacentTileIds.ToList(),
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
        var pieceSupply = PlayerPieceSupplyService.GetSupply(game, player.PlayerId);

        return new PlayerGameStateDto(
            player.PlayerId,
            player.PlayerName,
            player.IsHost,
            player.ConnectionStatus.ToString(),
            player.HasForfeited,
            player.PlayerColor,
            player.Supplies.Values.Sum(),
            supplies,
            player.CampsBuilt,
            player.StrongholdsBuilt,
            player.TrailsBuilt,
            pieceSupply.TotalTrails,
            pieceSupply.RemainingTrails,
            pieceSupply.TotalCamps,
            pieceSupply.RemainingCamps,
            pieceSupply.TotalStrongholds,
            pieceSupply.RemainingStrongholds,
            GameService.CalculateVictoryPoints(game, player, revealHidden: false),
            GameService.CalculateVictoryPoints(game, player, revealHidden),
            game.LargestArmyPlayerId == player.PlayerId,
            game.LongestTrailPlayerId == player.PlayerId,
            player.LongestTrailLength,
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

    private static PlayerTradeOfferDto ToDto(this PlayerTradeOffer offer, GameState game, string? viewerPlayerId)
    {
        var proposer = game.Players.FirstOrDefault(player =>
            string.Equals(player.PlayerId, offer.ProposerPlayerId, StringComparison.OrdinalIgnoreCase));
        var target = game.Players.FirstOrDefault(player =>
            string.Equals(player.PlayerId, offer.TargetPlayerId, StringComparison.OrdinalIgnoreCase));
        var isPending = offer.Status == PlayerTradeOfferStatus.Pending;
        var isStillActionable = isPending
            && game.Status == GameStatus.InProgress
            && game.Phase == GamePhase.NormalTurn
            && game.PendingWardenAction == WardenAction.None
            && game.CurrentPlayer.ActiveDevelopmentCardEffect is null
            && game.HasRolledThisTurn
            && offer.TurnNumber == game.TurnNumber
            && string.Equals(game.CurrentPlayer.PlayerId, offer.ProposerPlayerId, StringComparison.OrdinalIgnoreCase);
        var proposerCanPay = proposer is not null && HasSupplies(proposer, offer.OfferedResources);
        var targetCanPay = target is not null && HasSupplies(target, offer.RequestedResources);

        return new PlayerTradeOfferDto(
            offer.TradeOfferId,
            offer.RoomCode,
            offer.TurnNumber,
            offer.ProposerPlayerId,
            proposer?.PlayerName ?? "Player",
            offer.TargetPlayerId,
            target?.PlayerName ?? "Player",
            ToResourceDictionaryDto(offer.OfferedResources),
            ToResourceDictionaryDto(offer.RequestedResources),
            offer.Status.ToString(),
            offer.CreatedAt,
            offer.ResolvedAt,
            isStillActionable
                && proposerCanPay
                && targetCanPay
                && string.Equals(viewerPlayerId, offer.TargetPlayerId, StringComparison.OrdinalIgnoreCase),
            isStillActionable && string.Equals(viewerPlayerId, offer.TargetPlayerId, StringComparison.OrdinalIgnoreCase),
            isStillActionable && string.Equals(viewerPlayerId, offer.ProposerPlayerId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSupplies(PlayerGameState player, IReadOnlyDictionary<ResourceType, int> required)
    {
        return required.All(item => player.Supplies[item.Key] >= item.Value);
    }

    private static IReadOnlyDictionary<string, int> ToResourceDictionaryDto(IReadOnlyDictionary<ResourceType, int> resources)
    {
        return ResourceTypes.All
            .Where(resource => resources.TryGetValue(resource, out var amount) && amount > 0)
            .ToDictionary(resource => resource.ToString(), resource => resources[resource]);
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

    private static GamePauseDto ToDto(this GamePauseState pause)
    {
        return new GamePauseDto(
            pause.IsPaused,
            pause.Reason,
            pause.DisconnectedPlayerId,
            pause.PausedAt,
            pause.ReconnectDeadline);
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
