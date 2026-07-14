using Karo.Api.Models;

namespace Karo.Api.Services;

public sealed record PlayerPieceSupply(
    int TotalTrails,
    int TotalCamps,
    int TotalStrongholds,
    int RemainingTrails,
    int RemainingCamps,
    int RemainingStrongholds);

public static class PlayerPieceSupplyService
{
    public const int TotalTrails = 15;
    public const int TotalCamps = 5;
    public const int TotalStrongholds = 4;

    public static PlayerPieceSupply GetSupply(GameState game, string playerId)
    {
        var placedTrails = game.Board.Edges.Count(edge =>
            string.Equals(edge.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        var placedCamps = game.Board.Vertices.Count(vertex =>
            string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && vertex.StructureType == BoardStructureType.Camp);
        var placedStrongholds = game.Board.Vertices.Count(vertex =>
            string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && vertex.StructureType == BoardStructureType.Stronghold);

        return new PlayerPieceSupply(
            TotalTrails,
            TotalCamps,
            TotalStrongholds,
            TotalTrails - placedTrails,
            TotalCamps - placedCamps,
            TotalStrongholds - placedStrongholds);
    }

    public static void EnsureTrailAvailable(GameState game, string playerId)
    {
        if (GetSupply(game, playerId).RemainingTrails <= 0)
        {
            throw new GameRuleException("You have no Trail pieces remaining.");
        }
    }

    public static void EnsureCampAvailable(GameState game, string playerId)
    {
        if (GetSupply(game, playerId).RemainingCamps <= 0)
        {
            throw new GameRuleException("You have no Camp pieces remaining.");
        }
    }

    public static void EnsureStrongholdAvailable(GameState game, string playerId)
    {
        if (GetSupply(game, playerId).RemainingStrongholds <= 0)
        {
            throw new GameRuleException("You have no Stronghold pieces remaining.");
        }
    }

    public static bool HasValidInvariants(GameState game, string playerId)
    {
        var supply = GetSupply(game, playerId);
        return supply.RemainingTrails is >= 0 and <= TotalTrails
            && supply.RemainingCamps is >= 0 and <= TotalCamps
            && supply.RemainingStrongholds is >= 0 and <= TotalStrongholds;
    }
}
