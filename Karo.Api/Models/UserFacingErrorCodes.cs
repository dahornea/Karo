namespace Karo.Api.Models;

public static class UserFacingErrorCodes
{
    public const string ValidationFailed = "ValidationFailed";

    private static readonly IReadOnlyDictionary<string, string> ExactMessages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Player name is required."] = "PlayerNameRequired",
            ["Invalid room code."] = "InvalidRoomCode",
            ["This room is already in game."] = "RoomAlreadyInGame",
            ["This room is full."] = "RoomFull",
            ["Only the host can start the game."] = "OnlyHostCanStart",
            ["It is not your turn."] = "NotYourTurn",
            ["It is not your setup placement turn."] = "NotYourSetupTurn",
            ["Resolve the Warden action first."] = "WardenActionRequired",
            ["You must roll before buying a Development Card."] = "DevelopmentCardBuyRequiresRoll",
            ["You must roll before playing a Development Card."] = "DevelopmentCardPlayRequiresRoll",
            ["You cannot buy Development Cards during setup."] = "DevelopmentCardBuyBlockedDuringSetup",
            ["You cannot play Development Cards during setup."] = "DevelopmentCardPlayBlockedDuringSetup",
            ["You cannot play a Development Card bought this turn."] = "DevelopmentCardBoughtThisTurn",
            ["You already played a Development Card this turn."] = "DevelopmentCardAlreadyPlayedThisTurn",
            ["Not enough supplies."] = "NotEnoughSupplies",
            ["You do not have enough supplies for this trade."] = "NotEnoughSuppliesForTrade",
            ["You cannot trade a resource for itself."] = "TradeSameResource",
            ["You must roll before trading."] = "TradeRequiresRoll",
            ["Trading is not available during setup."] = "TradeBlockedDuringSetup",
            ["The Development Card deck is empty."] = "DevelopmentDeckEmpty",
            ["Choose a valid build node."] = "InvalidPlacement",
            ["Choose a valid Trail edge."] = "InvalidPlacement",
            ["That build node is already occupied."] = "BuildNodeOccupied",
            ["That Trail edge is already occupied."] = "TrailEdgeOccupied",
            ["Camps must leave at least one empty node between settlements."] = "CampSpacingRequired",
            ["Your setup Trail must connect to the Camp you just placed."] = "SetupTrailMustConnect",
            ["The Warden must move to a different tile."] = "WardenMustMove",
            ["Choose a valid tile for the Warden."] = "InvalidWardenTile",
            ["Invalid Warden victim."] = "InvalidWardenVictim"
        };

    public static string FromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return ValidationFailed;
        }

        if (ExactMessages.TryGetValue(message, out var code))
        {
            return code;
        }

        if (message.StartsWith("You must discard exactly ", StringComparison.OrdinalIgnoreCase))
        {
            return "WardenDiscardAmountMismatch";
        }

        if (message.StartsWith("Choose exactly ", StringComparison.OrdinalIgnoreCase))
        {
            return "InvalidSelectionCount";
        }

        if (message.StartsWith("You need ", StringComparison.OrdinalIgnoreCase))
        {
            return "NotEnoughSupplies";
        }

        if (message.Contains("not your turn", StringComparison.OrdinalIgnoreCase))
        {
            return "NotYourTurn";
        }

        if (message.Contains("Warden", StringComparison.OrdinalIgnoreCase))
        {
            return "WardenValidationFailed";
        }

        if (message.Contains("development card", StringComparison.OrdinalIgnoreCase))
        {
            return "DevelopmentCardValidationFailed";
        }

        if (message.Contains("trade", StringComparison.OrdinalIgnoreCase))
        {
            return "TradeValidationFailed";
        }

        if (message.Contains("setup", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Camp", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Trail", StringComparison.OrdinalIgnoreCase)
            || message.Contains("node", StringComparison.OrdinalIgnoreCase)
            || message.Contains("edge", StringComparison.OrdinalIgnoreCase))
        {
            return "PlacementValidationFailed";
        }

        return ValidationFailed;
    }
}
