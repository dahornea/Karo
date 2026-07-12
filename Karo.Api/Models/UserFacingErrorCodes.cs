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
            ["Only the room host can perform that action."] = "NotRoomHost",
            ["All connected players must be ready before the match can start."] = "PlayersNotReady",
            ["This room does not have enough players to start a match."] = "NotEnoughPlayers",
            ["This room no longer exists."] = "RoomNotFound",
            ["You are not connected to this room."] = "SessionNotConnected",
            ["This session is no longer valid. Please join the room again."] = "SessionExpired",
            ["This session is already active in another tab."] = "SessionReplaced",
            ["Leaving an active match requires forfeiting the match."] = "ForfeitRequired",
            ["That player has not timed out."] = "PlayerNotTimedOut",
            ["The match is paused while a player reconnects."] = "MatchPausedForReconnect",
            ["The match must be finished before returning to the lobby."] = "MatchNotFinished",
            ["It is not your turn."] = "NotYourTurn",
            ["It is not your setup placement turn."] = "NotYourSetupTurn",
            ["Resolve the Warden action first."] = "WardenActionRequired",
            ["Roll the dice before buying a Development Card."] = "DevelopmentCardBuyRequiresRoll",
            ["Development Cards cannot be bought during setup."] = "DevelopmentCardBuyBlockedDuringSetup",
            ["Development Cards cannot be used during setup."] = "DevelopmentCardPlayBlockedDuringSetup",
            ["You cannot play a Development Card bought this turn."] = "DevelopmentCardBoughtThisTurn",
            ["You can play only one Development Card per turn."] = "DevelopmentCardAlreadyPlayedThisTurn",
            ["Resolve the current Development Card action first."] = "DevelopmentCardActionPending",
            ["Not enough supplies."] = "NotEnoughSupplies",
            ["You do not have enough supplies for this trade."] = "NotEnoughSuppliesForTrade",
            ["You cannot trade a resource for itself."] = "TradeSameResource",
            ["You must roll before trading."] = "TradeRequiresRoll",
            ["Trading is not available during setup."] = "TradeBlockedDuringSetup",
            ["Only the current player can create trade offers."] = "PlayerTradeOnlyCurrentCanOffer",
            ["You cannot trade with yourself."] = "PlayerTradeSelfBlocked",
            ["Trade offers must include Supplies from both players."] = "PlayerTradeRequiresBothSides",
            ["Not enough Supplies for this offer."] = "PlayerTradeNotEnoughOfferedSupplies",
            ["Trading is not available right now."] = "PlayerTradeUnavailable",
            ["This trade offer is no longer available."] = "PlayerTradeUnavailableOffer",
            ["The other player no longer has the requested Supplies."] = "PlayerTradeTargetSuppliesChanged",
            ["The proposing player no longer has the offered Supplies."] = "PlayerTradeProposerSuppliesChanged",
            ["Only the target player can accept this trade offer."] = "PlayerTradeOnlyTargetCanAccept",
            ["Only the target player can reject this trade offer."] = "PlayerTradeOnlyTargetCanReject",
            ["Only the proposing player can cancel this trade offer."] = "PlayerTradeOnlyProposerCanCancel",
            ["The Development Card deck is empty."] = "DevelopmentDeckEmpty",
            ["You have no Trail pieces remaining."] = "NoTrailPiecesRemaining",
            ["No legal Trail placement is available."] = "NoLegalTrailPlacement",
            ["You have no Camp pieces remaining."] = "NoCampPiecesRemaining",
            ["You have no Stronghold pieces remaining."] = "NoStrongholdPiecesRemaining",
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
