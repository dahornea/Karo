using Karo.Api.Models;
using Microsoft.Extensions.Logging;

namespace Karo.Api.Services;

public sealed class GameService
{
    public static readonly IReadOnlyDictionary<ResourceType, int> TrailCost =
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 1,
            [ResourceType.Clay] = 1
        };

    public static readonly IReadOnlyDictionary<ResourceType, int> CampCost =
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 1,
            [ResourceType.Clay] = 1,
            [ResourceType.Wool] = 1,
            [ResourceType.Grain] = 1
        };

    public static readonly IReadOnlyDictionary<ResourceType, int> StrongholdCost =
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Grain] = 2,
            [ResourceType.Stone] = 3
        };

    public static readonly IReadOnlyDictionary<ResourceType, int> DevelopmentCardCost =
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wool] = 1,
            [ResourceType.Grain] = 1,
            [ResourceType.Stone] = 1
        };

    private readonly object _gate = new();
    private readonly BoardGenerator _boardGenerator;
    private readonly BoardIntegrityValidator _boardIntegrityValidator;
    private readonly Dictionary<string, GameState> _games = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GameService>? _logger;

    public GameService(
        BoardGenerator boardGenerator,
        ILogger<GameService>? logger = null,
        BoardIntegrityValidator? boardIntegrityValidator = null)
    {
        _boardGenerator = boardGenerator;
        _logger = logger;
        _boardIntegrityValidator = boardIntegrityValidator ?? new BoardIntegrityValidator();
    }

    public GameState StartGame(Room room)
    {
        lock (_gate)
        {
            if (_games.TryGetValue(room.RoomCode, out var existingGame))
            {
                return existingGame;
            }

            var game = CreateGame(room);
            _games[room.RoomCode] = game;
            return game;
        }
    }

    public GameState RestartGame(Room room, int? boardSeed = null)
    {
        lock (_gate)
        {
            var game = CreateGame(room, boardSeed);
            _games[room.RoomCode] = game;
            return game;
        }
    }

    public GameState? GetGame(string roomCode)
    {
        lock (_gate)
        {
            return _games.TryGetValue(roomCode, out var game)
                ? game
                : null;
        }
    }

    public GameState UpdateGame(string roomCode, Func<GameState, GameState> update)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            return update(game);
        }
    }

    public GameState PauseForReconnect(string roomCode, string playerId, DateTimeOffset reconnectDeadline, string reason)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            if (game.Status == GameStatus.Finished || game.Pause?.IsPaused == true)
            {
                return game;
            }

            game.Pause = new GamePauseState
            {
                IsPaused = true,
                Reason = reason,
                DisconnectedPlayerId = playerId,
                PausedAt = DateTimeOffset.UtcNow,
                ReconnectDeadline = reconnectDeadline
            };
            AddLog(game, $"Match paused while {GetGamePlayer(game, playerId).PlayerName} reconnects.", playerId);
            MarkStateChanged(game);
            return game;
        }
    }

    public GameState ResumeFromReconnect(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            if (game.Pause?.IsPaused == true
                && string.Equals(game.Pause.DisconnectedPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                game.Pause = null;
                AddLog(game, $"{GetGamePlayer(game, playerId).PlayerName} reconnected. Match resumed.", playerId);
                MarkStateChanged(game);
            }

            return game;
        }
    }

    public GameState? SetPlayerConnectionStatus(string roomCode, string playerId, PlayerConnectionStatus status)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(roomCode, out var game))
            {
                return null;
            }

            var player = game.Players.FirstOrDefault(candidate => string.Equals(candidate.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
            if (player is not null)
            {
                player.ConnectionStatus = status;
                MarkStateChanged(game);
            }

            return game;
        }
    }

    public GameState? SyncRoomPlayerMetadata(Room room)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(room.RoomCode, out var game))
            {
                return null;
            }

            var changed = false;
            foreach (var gamePlayer in game.Players)
            {
                var roomPlayer = room.Players.FirstOrDefault(player =>
                    string.Equals(player.PlayerId, gamePlayer.PlayerId, StringComparison.OrdinalIgnoreCase));
                if (roomPlayer is null)
                {
                    continue;
                }

                if (gamePlayer.ConnectionStatus != roomPlayer.ConnectionStatus)
                {
                    gamePlayer.ConnectionStatus = roomPlayer.ConnectionStatus;
                    changed = true;
                }

                if (gamePlayer.IsHost != roomPlayer.IsHost)
                {
                    gamePlayer.IsHost = roomPlayer.IsHost;
                    changed = true;
                }
            }

            if (changed)
            {
                MarkStateChanged(game);
            }

            return game;
        }
    }

    public GameState? InvalidateTradeOffersForPlayer(string roomCode, string playerId)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(roomCode, out var game))
            {
                return null;
            }

            var pendingOffers = game.TradeOffers.Where(offer => offer.Status == PlayerTradeOfferStatus.Pending
                && (string.Equals(offer.ProposerPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(offer.TargetPlayerId, playerId, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var offer in pendingOffers)
            {
                ExpireTradeOffer(offer);
            }

            if (pendingOffers.Count > 0)
            {
                AddLog(game, "A pending trade offer expired because a player disconnected.", playerId);
                MarkStateChanged(game);
            }

            return game;
        }
    }

    public static void MarkStateChanged(GameState game)
    {
        game.GameStateVersion++;
    }

    public GameState ForfeitPlayer(string roomCode, string playerId, string reason)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            var player = GetGamePlayer(game, playerId);
            if (player.HasForfeited)
            {
                return game;
            }

            var wasCurrentPlayer = string.Equals(game.CurrentPlayer.PlayerId, playerId, StringComparison.OrdinalIgnoreCase);
            player.HasForfeited = true;
            player.ConnectionStatus = PlayerConnectionStatus.Forfeited;
            player.ActiveDevelopmentCardEffect = null;
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.DevelopmentCards.Clear();
            foreach (var resource in ResourceTypes.All)
            {
                player.Supplies[resource] = 0;
            }

            foreach (var offer in game.TradeOffers.Where(offer => offer.Status == PlayerTradeOfferStatus.Pending
                && (string.Equals(offer.ProposerPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(offer.TargetPlayerId, playerId, StringComparison.OrdinalIgnoreCase))))
            {
                ExpireTradeOffer(offer);
            }

            game.PendingWardenDiscards.RemoveAll(discard => string.Equals(discard.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
            game.WardenVictimOptions.RemoveAll(candidate => string.Equals(candidate, playerId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(game.CurrentWardenPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                CompleteWardenFlow(game, checkWinner: false);
            }

            var orderIndex = game.PlayerOrder.FindIndex(candidate => string.Equals(candidate, playerId, StringComparison.OrdinalIgnoreCase));
            if (orderIndex >= 0)
            {
                game.PlayerOrder.RemoveAt(orderIndex);
                if (game.SetupPlayerIndex > orderIndex)
                {
                    game.SetupPlayerIndex--;
                }
                else if (game.SetupPlayerIndex >= game.PlayerOrder.Count)
                {
                    game.SetupPlayerIndex = Math.Max(0, game.PlayerOrder.Count - 1);
                }
            }

            RecalculateLargestArmyAfterForfeit(game, playerId);
            RecalculateLongestTrail(game, playerId, checkWinner: false);
            if (wasCurrentPlayer && ActivePlayers(game).Count > 0)
            {
                game.CurrentPlayerIndex = FindNextActivePlayerIndex(game, game.CurrentPlayerIndex);
            }

            game.Pause = null;
            AddLog(game, $"{player.PlayerName} forfeited the match. {reason}", playerId);
            MarkStateChanged(game);
            return game;
        }
    }

    public GameState FinishAbandonedMatch(string roomCode, string reason, string? actorPlayerId = null)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            if (game.Status != GameStatus.Finished)
            {
                ExpirePendingTradeOffers(game, actorPlayerId);
                game.Status = GameStatus.Finished;
                game.Phase = GamePhase.Finished;
                game.WinnerPlayerId = null;
                game.FinishedAt = DateTimeOffset.UtcNow;
                game.Pause = null;
                AddLog(game, reason, actorPlayerId);
                MarkStateChanged(game);
            }

            return game;
        }
    }

    public GameState EndTurn(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);

            ExpirePendingTradeOffers(game, player.PlayerId);
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
            game.LastDiceRoll = null;
            game.HasRolledThisTurn = false;
            game.CurrentPlayerIndex = FindNextActivePlayerIndex(game, game.CurrentPlayerIndex);
            game.CurrentPlayer.HasPlayedDevelopmentCardThisTurn = false;
            game.CurrentPlayer.ActiveDevelopmentCardEffect = null;
            game.TurnNumber++;
            AddLog(game, $"{player.PlayerName} ended their turn.");
            AddLog(game, $"{game.CurrentPlayer.PlayerName}'s turn begins.");
            return game;
        }
    }

    public GameState BuyDevelopmentCard(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(
                roomCode,
                playerId,
                setupMessage: "Development Cards cannot be bought during setup.",
                rollMessage: "Roll the dice before buying a Development Card.");

            if (game.DevelopmentDeck.Count == 0)
            {
                throw new GameRuleException("The Development Card deck is empty.");
            }

            Spend(player, DevelopmentCardCost, "Not enough supplies.");

            var card = game.DevelopmentDeck[0];
            game.DevelopmentDeck.RemoveAt(0);
            player.DevelopmentCards.Add(new PlayerDevelopmentCard
            {
                CardId = card.CardId,
                Type = card.Type,
                PurchasedTurn = game.TurnNumber
            });

            AddLog(game, $"{player.PlayerName} bought a development card.", player.PlayerId);
            CheckWinner(game, player);
            return game;
        }
    }

    public GameState TradeWithBank(string roomCode, string playerId, ResourceType offeredResource, ResourceType requestedResource)
    {
        return MaritimeTrade(roomCode, playerId, offeredResource, requestedResource);
    }

    public GameState MaritimeTrade(string roomCode, string playerId, ResourceType offeredResource, ResourceType requestedResource)
    {
        lock (_gate)
        {
            if (offeredResource == requestedResource)
            {
                throw new GameRuleException("You cannot trade a resource for itself.");
            }

            var (game, player) = EnsureNormalTurnCurrentPlayer(
                roomCode,
                playerId,
                setupMessage: "Trading is not available during setup.",
                rollMessage: "You must roll before trading.");
            var tradeRate = GetBestTradeRateInfo(game, player.PlayerId, offeredResource);

            if (player.Supplies[offeredResource] < tradeRate.Rate)
            {
                throw new GameRuleException("You do not have enough supplies for this trade.");
            }

            player.Supplies[offeredResource] -= tradeRate.Rate;
            player.Supplies[requestedResource]++;

            AddLog(
                game,
                $"{player.PlayerName} traded {tradeRate.Rate} {offeredResource} for 1 {requestedResource}.",
                player.PlayerId);
            return game;
        }
    }

    public GameState CreateTradeOffer(
        string roomCode,
        string playerId,
        string targetPlayerId,
        IReadOnlyDictionary<ResourceType, int> offeredResources,
        IReadOnlyDictionary<ResourceType, int> requestedResources)
    {
        lock (_gate)
        {
            var (game, proposer) = EnsurePlayerTradeCreationAllowed(roomCode, playerId);
            var target = game.Players.FirstOrDefault(candidate =>
                    string.Equals(candidate.PlayerId, targetPlayerId, StringComparison.OrdinalIgnoreCase))
                ?? throw new GameRuleException("Choose a valid player.");

            if (string.Equals(proposer.PlayerId, target.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameRuleException("You cannot trade with yourself.");
            }

            var offered = NormalizeTradeResources(offeredResources);
            var requested = NormalizeTradeResources(requestedResources);

            if (offered.Values.Sum() == 0 || requested.Values.Sum() == 0)
            {
                throw new GameRuleException("Trade offers must include Supplies from both players.");
            }

            if (!HasSupplies(proposer, offered))
            {
                throw new GameRuleException("Not enough Supplies for this offer.");
            }

            var offer = new PlayerTradeOffer
            {
                TradeOfferId = Guid.NewGuid().ToString("N"),
                RoomCode = game.RoomCode,
                TurnNumber = game.TurnNumber,
                ProposerPlayerId = proposer.PlayerId,
                TargetPlayerId = target.PlayerId,
                OfferedResources = offered,
                RequestedResources = requested,
                CreatedAt = DateTimeOffset.UtcNow
            };

            game.TradeOffers.Add(offer);
            AddLog(game, $"{proposer.PlayerName} offered a trade to {target.PlayerName}.", proposer.PlayerId);
            return game;
        }
    }

    public GameState AcceptTradeOffer(string roomCode, string playerId, string tradeOfferId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            var accepter = GetGamePlayer(game, playerId);
            var offer = GetPendingTradeOffer(game, tradeOfferId);

            EnsurePlayerTradeResolutionAllowed(game, offer);

            if (!string.Equals(offer.TargetPlayerId, accepter.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameRuleException("Only the target player can accept this trade offer.");
            }

            var proposer = GetGamePlayer(game, offer.ProposerPlayerId);

            if (!HasSupplies(proposer, offer.OfferedResources))
            {
                throw new GameRuleException("The proposing player no longer has the offered Supplies.");
            }

            if (!HasSupplies(accepter, offer.RequestedResources))
            {
                throw new GameRuleException("The other player no longer has the requested Supplies.");
            }

            TransferSupplies(proposer, accepter, offer.OfferedResources);
            TransferSupplies(accepter, proposer, offer.RequestedResources);

            offer.Status = PlayerTradeOfferStatus.Accepted;
            offer.ResolvedAt = DateTimeOffset.UtcNow;

            AddLog(game, $"{accepter.PlayerName} accepted {proposer.PlayerName}'s trade.", accepter.PlayerId);
            CheckWinner(game, proposer);
            if (game.Status != GameStatus.Finished)
            {
                CheckWinner(game, accepter);
            }

            return game;
        }
    }

    public GameState RejectTradeOffer(string roomCode, string playerId, string tradeOfferId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            var target = GetGamePlayer(game, playerId);
            var offer = GetPendingTradeOffer(game, tradeOfferId);

            EnsurePlayerTradeResolutionAllowed(game, offer);

            if (!string.Equals(offer.TargetPlayerId, target.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameRuleException("Only the target player can reject this trade offer.");
            }

            var proposer = GetGamePlayer(game, offer.ProposerPlayerId);
            offer.Status = PlayerTradeOfferStatus.Rejected;
            offer.ResolvedAt = DateTimeOffset.UtcNow;
            AddLog(game, $"{target.PlayerName} rejected {proposer.PlayerName}'s trade.", target.PlayerId);
            return game;
        }
    }

    public GameState CancelTradeOffer(string roomCode, string playerId, string tradeOfferId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            var proposer = GetGamePlayer(game, playerId);
            var offer = GetPendingTradeOffer(game, tradeOfferId);

            EnsurePlayerTradeResolutionAllowed(game, offer);

            if (!string.Equals(offer.ProposerPlayerId, proposer.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameRuleException("Only the proposing player can cancel this trade offer.");
            }

            if (!string.Equals(game.CurrentPlayer.PlayerId, proposer.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameRuleException("Only the current player can create trade offers.");
            }

            offer.Status = PlayerTradeOfferStatus.Cancelled;
            offer.ResolvedAt = DateTimeOffset.UtcNow;
            AddLog(game, $"{proposer.PlayerName} cancelled the trade offer.", proposer.PlayerId);
            return game;
        }
    }

    public GameState PlayYearOfPlenty(string roomCode, string playerId, string cardId, IReadOnlyList<ResourceType> selectedResources)
    {
        lock (_gate)
        {
            if (selectedResources.Count != 2)
            {
                throw new GameRuleException("Year of Plenty requires exactly 2 selected resources.");
            }

            var (game, player, card) = EnsurePlayableActionCard(roomCode, playerId, cardId, DevelopmentCardType.YearOfPlenty);

            foreach (var resource in selectedResources)
            {
                player.Supplies[resource]++;
            }

            MarkActionCardPlayed(game, player, card);
            AddLog(game, $"{player.PlayerName} played Year of Plenty and gathered 2 supplies.", player.PlayerId);
            CheckWinner(game, player);
            return game;
        }
    }

    public GameState PlayMonopoly(string roomCode, string playerId, string cardId, ResourceType selectedResource)
    {
        lock (_gate)
        {
            var (game, player, card) = EnsurePlayableActionCard(roomCode, playerId, cardId, DevelopmentCardType.Monopoly);
            var gained = 0;

            foreach (var opponent in game.Players.Where(candidate => candidate.PlayerId != player.PlayerId))
            {
                var amount = opponent.Supplies[selectedResource];
                if (amount == 0)
                {
                    continue;
                }

                opponent.Supplies[selectedResource] = 0;
                player.Supplies[selectedResource] += amount;
                gained += amount;
            }

            MarkActionCardPlayed(game, player, card);
            AddLog(game, $"{player.PlayerName} played Monopoly and collected {gained} {selectedResource}.", player.PlayerId);
            CheckWinner(game, player);
            return game;
        }
    }

    public GameState PlayKnight(string roomCode, string playerId, string cardId, string targetTileId, string? victimPlayerId)
    {
        lock (_gate)
        {
            var (game, player, card) = EnsurePlayableActionCard(roomCode, playerId, cardId, DevelopmentCardType.Knight);
            player.PlayedKnightCount++;
            MarkActionCardPlayed(game, player, card);
            AddLog(game, $"{player.PlayerName} played a Knight.", player.PlayerId);
            UpdateLargestArmy(game, player);
            CheckWinner(game, player);
            if (game.Status == GameStatus.Finished)
            {
                return game;
            }

            StartWardenMove(game, player.PlayerId);
            AddLog(game, "Move the Warden.", player.PlayerId);
            return game;
        }
    }

    public GameState DiscardForWarden(string roomCode, string playerId, IReadOnlyDictionary<ResourceType, int> discardedResources)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            EnsureMatchInProgress(game);

            if (game.PendingWardenAction != WardenAction.Discarding)
            {
                throw new GameRuleException("The Warden is not waiting for discards.");
            }

            var player = game.Players.FirstOrDefault(candidate => candidate.PlayerId == playerId)
                ?? throw new GameRuleException("Choose a valid player.");
            var requirement = game.PendingWardenDiscards.FirstOrDefault(candidate => candidate.PlayerId == player.PlayerId)
                ?? throw new GameRuleException("You do not need to discard for the Warden.");
            var discardTotal = ResourceTypes.All.Sum(resource =>
                Math.Max(0, discardedResources.TryGetValue(resource, out var amount) ? amount : 0));

            if (discardTotal != requirement.RequiredAmount)
            {
                throw new GameRuleException($"You must discard exactly {requirement.RequiredAmount} supplies.");
            }

            foreach (var resource in ResourceTypes.All)
            {
                var amount = Math.Max(0, discardedResources.TryGetValue(resource, out var value) ? value : 0);
                if (player.Supplies[resource] < amount)
                {
                    throw new GameRuleException("You cannot discard supplies you do not have.");
                }
            }

            foreach (var resource in ResourceTypes.All)
            {
                var amount = Math.Max(0, discardedResources.TryGetValue(resource, out var value) ? value : 0);
                player.Supplies[resource] -= amount;
            }

            game.PendingWardenDiscards.Remove(requirement);
            AddLog(game, $"{player.PlayerName} discarded {requirement.RequiredAmount} supplies.", player.PlayerId);

            if (game.PendingWardenDiscards.Count == 0)
            {
                var wardenPlayerId = game.CurrentWardenPlayerId ?? game.CurrentPlayer.PlayerId;
                StartWardenMove(game, wardenPlayerId);
            }

            return game;
        }
    }

    public GameState MoveWarden(string roomCode, string playerId, string targetTileId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            EnsureMatchInProgress(game);
            EnsureCurrentWardenPlayer(game, playerId, "Only the current player can move the Warden.");

            if (game.PendingWardenAction != WardenAction.MoveWarden)
            {
                throw new GameRuleException(game.PendingWardenAction == WardenAction.Discarding
                    ? "You must discard before the Warden can move."
                    : "Resolve the Warden action first.");
            }

            var player = game.CurrentPlayer;
            var targetTile = game.Board.Tiles.FirstOrDefault(tile => tile.TileId == targetTileId)
                ?? throw new GameRuleException("Choose a valid tile for the Warden.");

            MoveWardenToTile(game, targetTile.TileId);
            AddLog(game, $"{player.PlayerName} moved the Warden.", player.PlayerId);
            AddLog(game, "The Warden blocks this region.", player.PlayerId);

            var victimOptions = GetEligibleWardenVictims(game, player.PlayerId, targetTile.TileId)
                .Select(victim => victim.PlayerId)
                .ToList();
            game.WardenVictimOptions.Clear();
            game.WardenVictimOptions.AddRange(victimOptions);

            if (victimOptions.Count == 0)
            {
                AddLog(game, "No eligible victim was available.", player.PlayerId);
                CompleteWardenFlow(game);
                return game;
            }

            game.PendingWardenAction = WardenAction.ChooseVictim;
            return game;
        }
    }

    public GameState StealFromWardenVictim(string roomCode, string playerId, string victimPlayerId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            EnsureMatchInProgress(game);
            EnsureCurrentWardenPlayer(game, playerId, "Only the current player can choose a Warden victim.");

            if (game.PendingWardenAction != WardenAction.ChooseVictim)
            {
                throw new GameRuleException("The Warden is not waiting for a victim.");
            }

            var player = game.CurrentPlayer;
            if (!game.WardenVictimOptions.Contains(victimPlayerId, StringComparer.OrdinalIgnoreCase))
            {
                throw new GameRuleException("Invalid Warden victim.");
            }

            var victim = game.Players.FirstOrDefault(candidate =>
                    string.Equals(candidate.PlayerId, victimPlayerId, StringComparison.OrdinalIgnoreCase))
                ?? throw new GameRuleException("Invalid Warden victim.");

            if (victim.Supplies.Values.Sum() == 0)
            {
                throw new GameRuleException("This player has no supplies to steal.");
            }

            TryStealRandomResource(game, player, victim.PlayerId);
            AddLog(game, $"{player.PlayerName} stole 1 supply from {victim.PlayerName}.", player.PlayerId);
            CompleteWardenFlow(game);
            return game;
        }
    }

    public GameState StartRoadBuilding(string roomCode, string playerId, string cardId)
    {
        lock (_gate)
        {
            var (game, player, card) = EnsurePlayableActionCard(roomCode, playerId, cardId, DevelopmentCardType.RoadBuilding);
            PlayerPieceSupplyService.EnsureTrailAvailable(game, player.PlayerId);

            if (!HasLegalTrailPlacement(game, player))
            {
                throw new GameRuleException("No legal Trail placement is available.");
            }

            MarkActionCardPlayed(game, player, card);
            player.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
            {
                Type = ActiveDevelopmentCardType.RoadBuilding,
                CardId = card.CardId
            };
            AddLog(game, $"{player.PlayerName} played Road Building.", player.PlayerId);
            return game;
        }
    }

    public GameState PlaceFreeTrail(string roomCode, string playerId, string edgeId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId, requireRolled: false, allowActiveDevelopmentEffect: true);

            if (player.ActiveDevelopmentCardEffect?.Type != ActiveDevelopmentCardType.RoadBuilding)
            {
                throw new GameRuleException("Road Building is not active.");
            }

            var effect = player.ActiveDevelopmentCardEffect;
            if (effect.FreeTrailsPlaced >= effect.MaxFreeTrails)
            {
                throw new GameRuleException("Road Building has already placed its free Trails.");
            }

            PlayerPieceSupplyService.EnsureTrailAvailable(game, player.PlayerId);

            var edge = game.Board.Edges.FirstOrDefault(candidate => candidate.EdgeId == edgeId)
                ?? throw new GameRuleException("Choose a valid Trail edge.");

            if (edge.OwnerPlayerId is not null)
            {
                throw new GameRuleException("That Trail edge is already occupied.");
            }

            if (!CanPlaceTrailFromNetwork(game, player, edge))
            {
                throw new GameRuleException(GetTrailConnectionFailureMessage(game, player, edge));
            }

            edge.OwnerPlayerId = player.PlayerId;
            player.TrailsBuilt++;
            effect.FreeTrailsPlaced++;
            AddLog(game, $"{player.PlayerName} placed a free Trail.", player.PlayerId);

            if (effect.FreeTrailsPlaced >= effect.MaxFreeTrails)
            {
                player.ActiveDevelopmentCardEffect = null;
            }
            else if (PlayerPieceSupplyService.GetSupply(game, player.PlayerId).RemainingTrails <= 0)
            {
                player.ActiveDevelopmentCardEffect = null;
                AddLog(game, $"{player.PlayerName} has no Trail pieces remaining.", player.PlayerId);
            }

            RecalculateLongestTrail(game, player.PlayerId);
            return game;
        }
    }

    public GameState CancelActiveDevelopmentCard(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId, requireRolled: false, allowActiveDevelopmentEffect: true);
            player.ActiveDevelopmentCardEffect = null;
            AddLog(game, $"{player.PlayerName} canceled their active development effect.", player.PlayerId);
            return game;
        }
    }

    public GameState BuildTrail(string roomCode, string playerId, string edgeId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);
            PlayerPieceSupplyService.EnsureTrailAvailable(game, player.PlayerId);

            var edge = game.Board.Edges.FirstOrDefault(candidate => candidate.EdgeId == edgeId)
                ?? throw new GameRuleException("Choose a valid Trail edge.");

            if (edge.OwnerPlayerId is not null)
            {
                throw new GameRuleException("That Trail edge is already occupied.");
            }

            if (!CanPlaceTrailFromNetwork(game, player, edge))
            {
                throw new GameRuleException(GetTrailConnectionFailureMessage(game, player, edge));
            }

            Spend(player, TrailCost);
            edge.OwnerPlayerId = player.PlayerId;
            player.TrailsBuilt++;
            AddLog(game, $"{player.PlayerName} built a Trail.", player.PlayerId);
            RecalculateLongestTrail(game, player.PlayerId);
            return game;
        }
    }

    public GameState BuildCamp(string roomCode, string playerId, string vertexId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);
            PlayerPieceSupplyService.EnsureCampAvailable(game, player.PlayerId);

            var vertex = game.Board.Vertices.FirstOrDefault(candidate => candidate.VertexId == vertexId)
                ?? throw new GameRuleException("Choose a valid build node.");

            if (vertex.OwnerPlayerId is not null || vertex.StructureType is not null)
            {
                throw new GameRuleException("That build node is already occupied.");
            }

            if (HasAdjacentStructure(game.Board, vertex.VertexId))
            {
                throw new GameRuleException("Camps must leave at least one empty node between settlements.");
            }

            if (!CanBuildCampFromNetwork(game, player, vertex.VertexId))
            {
                throw new GameRuleException("Camps must connect to one of your Trails.");
            }

            Spend(player, CampCost);
            vertex.OwnerPlayerId = player.PlayerId;
            vertex.StructureType = BoardStructureType.Camp;
            player.CampsBuilt++;
            AddLog(game, $"{player.PlayerName} built a Camp.", player.PlayerId);
            RecalculateLongestTrail(game, player.PlayerId);
            CheckWinner(game, player);
            return game;
        }
    }

    public GameState BuildStronghold(string roomCode, string playerId, string vertexId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);
            PlayerPieceSupplyService.EnsureStrongholdAvailable(game, player.PlayerId);

            var vertex = game.Board.Vertices.FirstOrDefault(candidate => candidate.VertexId == vertexId)
                ?? throw new GameRuleException("Choose a valid build node.");

            if (!string.Equals(vertex.OwnerPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase)
                || vertex.StructureType != BoardStructureType.Camp)
            {
                throw new GameRuleException("Choose one of your Camps to upgrade.");
            }

            Spend(player, StrongholdCost);
            vertex.StructureType = BoardStructureType.Stronghold;
            player.CampsBuilt--;
            player.StrongholdsBuilt++;
            AddLog(game, $"{player.PlayerName} upgraded a Camp to a Stronghold.", player.PlayerId);
            RecalculateLongestTrail(game, player.PlayerId);
            CheckWinner(game, player);
            return game;
        }
    }

    public GameState PlaceSetupCamp(string roomCode, string playerId, string vertexId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureSetupPlayer(roomCode, playerId);

            if (game.SetupStep != SetupStep.PlaceCamp)
            {
                throw new GameRuleException("Place your connected Trail before placing another Camp.");
            }

            PlayerPieceSupplyService.EnsureCampAvailable(game, player.PlayerId);

            var vertex = game.Board.Vertices.FirstOrDefault(candidate => candidate.VertexId == vertexId)
                ?? throw new GameRuleException("Choose a valid build node.");

            if (vertex.OwnerPlayerId is not null || vertex.StructureType is not null)
            {
                throw new GameRuleException("That build node is already occupied.");
            }

            if (HasAdjacentStructure(game.Board, vertex.VertexId))
            {
                throw new GameRuleException("Camps must leave at least one empty node between settlements.");
            }

            vertex.OwnerPlayerId = player.PlayerId;
            vertex.StructureType = BoardStructureType.Camp;
            player.CampsBuilt++;
            game.LastSetupCampVertexId = vertex.VertexId;
            game.SetupStep = SetupStep.PlaceTrail;

            AddLog(game, $"{player.PlayerName} placed a setup Camp.", player.PlayerId);

            if (game.SetupRound == SetupRound.SecondPlacement)
            {
                GrantStartingSupplies(game, player, vertex);
            }

            RecalculateLongestTrail(game, player.PlayerId, checkWinner: false);
            return game;
        }
    }

    public GameState PlaceSetupTrail(string roomCode, string playerId, string edgeId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureSetupPlayer(roomCode, playerId);

            if (game.SetupStep != SetupStep.PlaceTrail)
            {
                throw new GameRuleException("Place your setup Camp before placing a Trail.");
            }

            if (string.IsNullOrWhiteSpace(game.LastSetupCampVertexId))
            {
                throw new GameRuleException("Place your setup Camp before placing a Trail.");
            }

            PlayerPieceSupplyService.EnsureTrailAvailable(game, player.PlayerId);

            var edge = game.Board.Edges.FirstOrDefault(candidate => candidate.EdgeId == edgeId)
                ?? throw new GameRuleException("Choose a valid Trail edge.");

            if (edge.OwnerPlayerId is not null)
            {
                throw new GameRuleException("That Trail edge is already occupied.");
            }

            if (!EdgeTouchesVertex(edge, game.LastSetupCampVertexId))
            {
                throw new GameRuleException("Your setup Trail must connect to the Camp you just placed.");
            }

            edge.OwnerPlayerId = player.PlayerId;
            player.TrailsBuilt++;
            AddLog(game, $"{player.PlayerName} placed a setup Trail.", player.PlayerId);
            RecalculateLongestTrail(game, player.PlayerId, checkWinner: false);
            AdvanceSetupTurn(game);
            return game;
        }
    }

    public GameState RollDice(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId, requireRolled: false);

            if (game.HasRolledThisTurn)
            {
                throw new GameRuleException("You have already rolled dice this turn.");
            }

            var diceValue = Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
            ApplyDiceRoll(game, player, diceValue, isDebug: false);
            return game;
        }
    }

    public GameState SkipSetupForDebug(string roomCode, string actorPlayerId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);

            if (game.Status == GameStatus.Finished)
            {
                throw new GameRuleException("This match has already finished.");
            }

            CompleteSetup(game);
            AddLog(game, "[DEBUG] Skipped setup phase.", actorPlayerId);
            return game;
        }
    }

    public GameState ForceDiceRollForDebug(string roomCode, string actorPlayerId, int diceValue)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);

            if (game.Status == GameStatus.Finished)
            {
                throw new GameRuleException("This match has already finished.");
            }

            if (game.Phase == GamePhase.Setup)
            {
                CompleteSetup(game);
            }

            ApplyDiceRoll(game, game.CurrentPlayer, diceValue, isDebug: true, actorPlayerId: actorPlayerId);
            return game;
        }
    }

    public GameState MoveWardenForDebug(string roomCode, string actorPlayerId, string targetTileId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            EnsureMatchInProgress(game);

            var targetTile = game.Board.Tiles.FirstOrDefault(tile => tile.TileId == targetTileId)
                ?? throw new GameRuleException("Choose a valid tile for the Warden.");

            MoveWardenToTile(game, targetTile.TileId, allowCurrentTile: true);
            AddLog(game, $"[DEBUG] Moved the Warden to {targetTile.TileId}.", actorPlayerId);
            return game;
        }
    }

    public GameState ClearWardenStateForDebug(string roomCode, string actorPlayerId)
    {
        lock (_gate)
        {
            var game = EnsureGame(roomCode);
            EnsureMatchInProgress(game);
            CompleteWardenFlow(game, checkWinner: false);
            AddLog(game, "[DEBUG] Cleared pending Warden state.", actorPlayerId);
            return game;
        }
    }

    public void RemoveGame(string roomCode)
    {
        lock (_gate)
        {
            _games.Remove(roomCode);
        }
    }

    public static IReadOnlyList<Port> GetPlayerPorts(GameState game, string playerId)
    {
        var accessibleHarborEdges = GetPlayerHarborSlots(game, playerId)
            .Select(slot => (slot.TileQ, slot.TileR, slot.EdgeIndex))
            .ToHashSet();

        return game.Board.Ports
            .Where(port => accessibleHarborEdges.Contains((port.TileQ, port.TileR, port.EdgeIndex)))
            .ToList();
    }

    public static IReadOnlyList<HarborSlot> GetPlayerHarborSlots(GameState game, string playerId)
    {
        var occupiedVertexIds = game.Board.Vertices
            .Where(vertex => string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
                && vertex.StructureType is not null)
            .Select(vertex => vertex.VertexId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return game.Board.HarborSlots
            .Where(slot => slot.AdjacentVertexIds.Any(occupiedVertexIds.Contains))
            .ToList();
    }

    public static bool PlayerHasGenericPort(GameState game, string playerId)
    {
        return GetPlayerHarborSlots(game, playerId).Any(slot => slot.HarborType == HarborType.Generic);
    }

    public static bool PlayerHasSpecificPort(GameState game, string playerId, ResourceType resource)
    {
        var harborType = ToHarborType(resource);
        return GetPlayerHarborSlots(game, playerId)
            .Any(slot => slot.HarborType == harborType);
    }

    public static int GetBestTradeRate(GameState game, string playerId, ResourceType resource)
    {
        return GetBestTradeRateInfo(game, playerId, resource).Rate;
    }

    public static BankTradeRate GetBestTradeRateInfo(GameState game, string playerId, ResourceType resource)
    {
        var playerHarbors = GetPlayerHarborSlots(game, playerId);
        var harborType = ToHarborType(resource);
        var specificHarbor = playerHarbors.FirstOrDefault(slot => slot.HarborType == harborType);

        if (specificHarbor is not null)
        {
            return new BankTradeRate(resource, specificHarbor.TradeRate ?? 2, BankTradeRateSource.SpecificPort, specificHarbor.HarborSlotId);
        }

        var genericHarbor = playerHarbors.FirstOrDefault(slot => slot.HarborType == HarborType.Generic);
        if (genericHarbor is not null)
        {
            return new BankTradeRate(resource, genericHarbor.TradeRate ?? 3, BankTradeRateSource.GenericPort, genericHarbor.HarborSlotId);
        }

        return new BankTradeRate(resource, 4, BankTradeRateSource.DefaultBank, null);
    }

    public static IReadOnlyList<BankTradeRate> GetTradeRates(GameState game, string playerId)
    {
        return ResourceTypes.All
            .Select(resource => GetBestTradeRateInfo(game, playerId, resource))
            .ToList();
    }

    private static HexTile PickStartingWardenTile(BoardState board)
    {
        return board.Tiles.Single(tile => tile.ResourceType == TileResourceType.None);
    }

    private static HarborType ToHarborType(ResourceType resource)
    {
        return resource switch
        {
            ResourceType.Wood => HarborType.Wood,
            ResourceType.Clay => HarborType.Clay,
            ResourceType.Wool => HarborType.Wool,
            ResourceType.Grain => HarborType.Grain,
            ResourceType.Stone => HarborType.Stone,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
        };
    }

    private static List<DevelopmentCard> CreateDevelopmentDeck()
    {
        var cards = new List<DevelopmentCard>();
        AddCards(cards, DevelopmentCardType.Knight, 14);
        AddCards(cards, DevelopmentCardType.RoadBuilding, 2);
        AddCards(cards, DevelopmentCardType.YearOfPlenty, 2);
        AddCards(cards, DevelopmentCardType.Monopoly, 2);
        AddCards(cards, DevelopmentCardType.VictoryPoint, 5);
        Shuffle(cards);
        return cards;
    }

    private GameState CreateGame(Room room, int? boardSeed = null)
    {
        BoardState board;
        try
        {
            board = boardSeed is null
                ? _boardGenerator.Generate()
                : _boardGenerator.Generate(boardSeed.Value);
        }
        catch (BoardGenerationException exception)
        {
            _logger?.LogError(
                exception,
                "Karo board generation failed for room {RoomCode} and seed {BoardSeed}. Errors: {BoardErrors}",
                room.RoomCode,
                exception.BoardSeed,
                string.Join(" | ", exception.Errors));
            throw new GameRuleException("Karo could not create a valid board. Please try starting the match again.");
        }

        var wardenTile = PickStartingWardenTile(board);
        wardenTile.IsBlocked = true;

        var validation = _boardIntegrityValidator.Validate(board, wardenTile.TileId, requireWardenOnDesert: true);
        if (!validation.IsValid)
        {
            _logger?.LogError(
                "Karo board validation failed for room {RoomCode} and seed {BoardSeed}. Errors: {BoardErrors}",
                room.RoomCode,
                validation.BoardSeed,
                string.Join(" | ", validation.Errors));
            throw new GameRuleException("Karo could not validate the generated board. Please try starting the match again.");
        }

        var game = new GameState
        {
            RoomCode = room.RoomCode,
            Board = board,
            WardenTileId = wardenTile.TileId
        };

        game.DevelopmentDeck.AddRange(CreateDevelopmentDeck());

        var playerOrder = room.Players
            .OrderBy(player => player.JoinedAt)
            .ToList();
        Shuffle(playerOrder);

        foreach (var player in playerOrder)
        {
            var gamePlayer = new PlayerGameState
            {
                PlayerId = player.PlayerId,
                PlayerName = player.PlayerName,
                IsHost = player.IsHost,
                ConnectionStatus = player.ConnectionStatus,
                PlayerColor = player.PlayerColor
            };

            game.Players.Add(gamePlayer);
            game.PlayerOrder.Add(gamePlayer.PlayerId);
        }

        game.CurrentPlayerIndex = 0;
        game.SetupPlayerIndex = 0;

        AddLog(game, $"Setup order: {string.Join(", ", game.Players.Select(player => player.PlayerName))}.");
        AddLog(game, $"{game.CurrentPlayer.PlayerName} begins setup. Place a Camp, then a connected Trail.", game.CurrentPlayer.PlayerId);
        return game;
    }

    private static void AddCards(List<DevelopmentCard> cards, DevelopmentCardType type, int count)
    {
        for (var index = 0; index < count; index++)
        {
            cards.Add(new DevelopmentCard
            {
                CardId = Guid.NewGuid().ToString("N"),
                Type = type
            });
        }
    }

    private (GameState Game, PlayerGameState Player) EnsureCurrentPlayer(string roomCode, string playerId)
    {
        var game = EnsureGame(roomCode);

        EnsureMatchInProgress(game);

        if (game.CurrentPlayer.PlayerId != playerId)
        {
            throw new GameRuleException("It is not your turn.");
        }

        return (game, game.CurrentPlayer);
    }

    private (GameState Game, PlayerGameState Player) EnsureNormalTurnCurrentPlayer(
        string roomCode,
        string playerId,
        bool requireRolled = true,
        string? setupMessage = null,
        string? rollMessage = null,
        bool allowActiveDevelopmentEffect = false)
    {
        var (game, player) = EnsureCurrentPlayer(roomCode, playerId);

        if (game.Phase != GamePhase.NormalTurn)
        {
            throw new GameRuleException(setupMessage ?? "The match is still in setup.");
        }

        if (game.PendingWardenAction != WardenAction.None)
        {
            throw new GameRuleException("Resolve the Warden action first.");
        }

        if (!allowActiveDevelopmentEffect && player.ActiveDevelopmentCardEffect is not null)
        {
            throw new GameRuleException("Resolve the current Development Card action first.");
        }

        if (requireRolled && !game.HasRolledThisTurn)
        {
            throw new GameRuleException(rollMessage ?? "You must roll dice before taking this action.");
        }

        return (game, player);
    }

    private (GameState Game, PlayerGameState Player) EnsurePlayerTradeCreationAllowed(string roomCode, string playerId)
    {
        var game = EnsureGame(roomCode);

        if (game.Status == GameStatus.Finished
            || game.Pause?.IsPaused == true
            || game.Phase != GamePhase.NormalTurn
            || game.PendingWardenAction != WardenAction.None
            || game.CurrentPlayer.ActiveDevelopmentCardEffect is not null)
        {
            throw new GameRuleException("Trading is not available right now.");
        }

        if (!string.Equals(game.CurrentPlayer.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("Only the current player can create trade offers.");
        }

        if (!game.HasRolledThisTurn)
        {
            throw new GameRuleException("You must roll before trading.");
        }

        return (game, game.CurrentPlayer);
    }

    private static void EnsurePlayerTradeResolutionAllowed(GameState game, PlayerTradeOffer offer)
    {
        if (game.Status == GameStatus.Finished
            || game.Pause?.IsPaused == true
            || game.Phase != GamePhase.NormalTurn
            || game.PendingWardenAction != WardenAction.None
            || game.CurrentPlayer.ActiveDevelopmentCardEffect is not null
            || !game.HasRolledThisTurn
            || offer.TurnNumber != game.TurnNumber
            || !string.Equals(game.CurrentPlayer.PlayerId, offer.ProposerPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("This trade offer is no longer available.");
        }
    }

    private static PlayerTradeOffer GetPendingTradeOffer(GameState game, string tradeOfferId)
    {
        var offer = game.TradeOffers.FirstOrDefault(candidate =>
                string.Equals(candidate.TradeOfferId, tradeOfferId, StringComparison.OrdinalIgnoreCase))
            ?? throw new GameRuleException("This trade offer is no longer available.");

        if (offer.Status != PlayerTradeOfferStatus.Pending)
        {
            throw new GameRuleException("This trade offer is no longer available.");
        }

        return offer;
    }

    private static Dictionary<ResourceType, int> NormalizeTradeResources(IReadOnlyDictionary<ResourceType, int> resources)
    {
        var normalized = new Dictionary<ResourceType, int>();

        foreach (var (resource, amount) in resources)
        {
            if (!ResourceTypes.All.Contains(resource))
            {
                throw new GameRuleException("Choose a valid resource.");
            }

            if (amount < 0)
            {
                throw new GameRuleException("Trade offers must include Supplies from both players.");
            }

            if (amount == 0)
            {
                continue;
            }

            normalized[resource] = normalized.GetValueOrDefault(resource) + amount;
        }

        return normalized;
    }

    private static bool HasSupplies(PlayerGameState player, IReadOnlyDictionary<ResourceType, int> requiredResources)
    {
        return requiredResources.All(item => player.Supplies[item.Key] >= item.Value);
    }

    private static void TransferSupplies(
        PlayerGameState fromPlayer,
        PlayerGameState toPlayer,
        IReadOnlyDictionary<ResourceType, int> resources)
    {
        foreach (var (resource, amount) in resources)
        {
            fromPlayer.Supplies[resource] -= amount;
            toPlayer.Supplies[resource] += amount;
        }
    }

    private static void ExpirePendingTradeOffers(GameState game, string? actorPlayerId = null)
    {
        var pendingOffers = game.TradeOffers
            .Where(offer => offer.Status == PlayerTradeOfferStatus.Pending)
            .ToList();

        if (pendingOffers.Count == 0)
        {
            return;
        }

        foreach (var offer in pendingOffers)
        {
            ExpireTradeOffer(offer);
        }

        AddLog(game, "Pending trade offers expired.", actorPlayerId);
    }

    private static void ExpireTradeOffer(PlayerTradeOffer offer)
    {
        if (offer.Status != PlayerTradeOfferStatus.Pending)
        {
            return;
        }

        offer.Status = PlayerTradeOfferStatus.Expired;
        offer.ResolvedAt = DateTimeOffset.UtcNow;
    }

    private static PlayerGameState GetGamePlayer(GameState game, string playerId)
    {
        return game.Players.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new GameRuleException("Choose a valid player.");
    }

    private (GameState Game, PlayerGameState Player) EnsureSetupPlayer(string roomCode, string playerId)
    {
        var game = EnsureGame(roomCode);

        if (game.Status == GameStatus.Finished)
        {
            throw new GameRuleException("This match has already finished.");
        }

        if (game.Pause?.IsPaused == true)
        {
            throw new GameRuleException("The match is paused while a player reconnects.");
        }

        if (game.Phase != GamePhase.Setup)
        {
            throw new GameRuleException("The setup phase is already complete.");
        }

        var currentSetupPlayerId = game.CurrentSetupPlayerId
            ?? throw new GameRuleException("The setup phase is not ready.");

        if (!string.Equals(currentSetupPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("It is not your setup placement turn.");
        }

        var player = game.Players.First(candidate => string.Equals(candidate.PlayerId, currentSetupPlayerId, StringComparison.OrdinalIgnoreCase));
        return (game, player);
    }

    private (GameState Game, PlayerGameState Player, PlayerDevelopmentCard Card) EnsurePlayableActionCard(
        string roomCode,
        string playerId,
        string cardId,
        DevelopmentCardType expectedType)
    {
        var (game, player) = EnsureNormalTurnCurrentPlayer(
            roomCode,
            playerId,
            requireRolled: false,
            setupMessage: "Development Cards cannot be used during setup.");
        var card = player.DevelopmentCards.FirstOrDefault(candidate => candidate.CardId == cardId)
            ?? throw new GameRuleException("You do not own that development card.");

        if (card.Type != expectedType)
        {
            throw new GameRuleException("That development card cannot perform this action.");
        }

        if (card.Type == DevelopmentCardType.VictoryPoint)
        {
            throw new GameRuleException("Victory Point cards stay hidden and count automatically.");
        }

        if (card.IsPlayed)
        {
            throw new GameRuleException("That development card has already been played.");
        }

        if (card.PurchasedTurn == game.TurnNumber)
        {
            throw new GameRuleException("You cannot play a Development Card bought this turn.");
        }

        if (player.HasPlayedDevelopmentCardThisTurn)
        {
            throw new GameRuleException("You can play only one Development Card per turn.");
        }

        return (game, player, card);
    }

    private GameState EnsureGame(string roomCode)
    {
        if (!_games.TryGetValue(roomCode, out var game))
        {
            throw new GameRuleException("No active match exists for this room.");
        }

        return game;
    }

    private static IReadOnlyList<PlayerGameState> ActivePlayers(GameState game)
    {
        return game.Players.Where(player => !player.HasForfeited).ToList();
    }

    private static int FindNextActivePlayerIndex(GameState game, int currentIndex)
    {
        for (var offset = 1; offset <= game.Players.Count; offset++)
        {
            var candidateIndex = (currentIndex + offset) % game.Players.Count;
            if (!game.Players[candidateIndex].HasForfeited)
            {
                return candidateIndex;
            }
        }

        return currentIndex;
    }

    private static void EnsureMatchInProgress(GameState game)
    {
        if (game.Status == GameStatus.Finished)
        {
            throw new GameRuleException("This match has already finished.");
        }

        if (game.Pause?.IsPaused == true)
        {
            throw new GameRuleException("The match is paused while a player reconnects.");
        }
    }

    private static bool HasAdjacentStructure(BoardState board, string vertexId)
    {
        return board.Edges
            .Where(edge => EdgeTouchesVertex(edge, vertexId))
            .Select(edge => edge.StartVertexId == vertexId ? edge.EndVertexId : edge.StartVertexId)
            .Select(otherVertexId => board.Vertices.First(vertex => vertex.VertexId == otherVertexId))
            .Any(vertex => vertex.OwnerPlayerId is not null && vertex.StructureType is not null);
    }

    private static bool CanPlaceTrailFromNetwork(GameState game, PlayerGameState player, BoardEdge edge)
    {
        return CanConnectTrailAtVertex(game, player.PlayerId, edge.StartVertexId)
            || CanConnectTrailAtVertex(game, player.PlayerId, edge.EndVertexId);
    }

    private static bool HasLegalTrailPlacement(GameState game, PlayerGameState player)
    {
        return game.Board.Edges.Any(edge =>
            edge.OwnerPlayerId is null && CanPlaceTrailFromNetwork(game, player, edge));
    }

    private static bool CanConnectTrailAtVertex(GameState game, string playerId, string vertexId)
    {
        if (IsOpponentStructureAtVertex(game, playerId, vertexId))
        {
            return false;
        }

        if (IsOwnStructureAtVertex(game, playerId, vertexId))
        {
            return true;
        }

        return HasOwnedTrailAtVertex(game, playerId, vertexId);
    }

    private static string GetTrailConnectionFailureMessage(GameState game, PlayerGameState player, BoardEdge edge)
    {
        if (IsOpponentStructureAtVertex(game, player.PlayerId, edge.StartVertexId)
            || IsOpponentStructureAtVertex(game, player.PlayerId, edge.EndVertexId))
        {
            return "An opponent's Camp or Stronghold blocks Trail continuation at that node.";
        }

        return "Trails must connect to one of your Trails, Camps, or Strongholds.";
    }

    private static bool IsOpponentStructureAtVertex(GameState game, string playerId, string vertexId)
    {
        var vertex = FindVertex(game, vertexId);
        return vertex?.OwnerPlayerId is not null
            && vertex.StructureType is not null
            && !string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOwnStructureAtVertex(GameState game, string playerId, string vertexId)
    {
        var vertex = FindVertex(game, vertexId);
        return vertex?.OwnerPlayerId is not null
            && vertex.StructureType is not null
            && string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasOwnedTrailAtVertex(GameState game, string playerId, string vertexId)
    {
        return game.Board.Edges
            .Where(candidate => EdgeTouchesVertex(candidate, vertexId))
            .Any(candidate => string.Equals(candidate.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase));
    }

    private static BoardVertex? FindVertex(GameState game, string vertexId)
    {
        return game.Board.Vertices.FirstOrDefault(candidate =>
            string.Equals(candidate.VertexId, vertexId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanBuildCampFromNetwork(GameState game, PlayerGameState player, string vertexId)
    {
        return game.Board.Edges
            .Where(edge => EdgeTouchesVertex(edge, vertexId))
            .Any(edge => string.Equals(edge.OwnerPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EdgeTouchesVertex(BoardEdge edge, string vertexId)
    {
        return string.Equals(edge.StartVertexId, vertexId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(edge.EndVertexId, vertexId, StringComparison.OrdinalIgnoreCase);
    }

    private void GrantStartingSupplies(GameState game, PlayerGameState player, BoardVertex vertex)
    {
        var gained = new Dictionary<ResourceType, int>();
        var adjacentTiles = vertex.AdjacentTileIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tileId => game.Board.Tiles.First(candidate => candidate.TileId == tileId))
            .ToList();

        foreach (var tile in adjacentTiles)
        {
            var resource = ToSupplyResource(tile.ResourceType);

            if (resource is null || tile.NumberToken is null)
            {
                continue;
            }

            player.Supplies[resource.Value]++;
            gained[resource.Value] = gained.GetValueOrDefault(resource.Value) + 1;
        }

        _logger?.LogDebug(
            "Starting supplies from node {NodeId}. Adjacent tiles: {AdjacentTiles}. Granted: {GrantedSupplies}.",
            vertex.VertexId,
            string.Join(", ", adjacentTiles.Select(tile => $"{tile.TileId}:{tile.ResourceType}")),
            gained.Count == 0 ? "none" : FormatStartingSupplySummary(gained));

        AddLog(
            game,
            gained.Count == 0
                ? $"{player.PlayerName}'s second Camp produced no starting supplies."
                : $"{player.PlayerName} gained starting supplies: {FormatStartingSupplySummary(gained)}.",
            player.PlayerId);
    }

    private static void AdvanceSetupTurn(GameState game)
    {
        game.LastSetupCampVertexId = null;

        if (game.SetupRound == SetupRound.FirstPlacement)
        {
            if (game.SetupPlayerIndex < game.PlayerOrder.Count - 1)
            {
                game.SetupPlayerIndex++;
                game.SetupStep = SetupStep.PlaceCamp;
                SetCurrentPlayerFromSetup(game);
                AddLog(game, $"{game.CurrentPlayer.PlayerName}'s setup placement begins.", game.CurrentPlayer.PlayerId);
                return;
            }

            game.SetupRound = SetupRound.SecondPlacement;
            game.SetupDirection = SetupDirection.Reverse;
            game.SetupPlayerIndex = game.PlayerOrder.Count - 1;
            game.SetupStep = SetupStep.PlaceCamp;
            SetCurrentPlayerFromSetup(game);
            AddLog(game, "Second setup round begins in reverse order.");
            AddLog(game, $"{game.CurrentPlayer.PlayerName}'s second setup placement begins.", game.CurrentPlayer.PlayerId);
            return;
        }

        if (game.SetupRound == SetupRound.SecondPlacement && game.SetupPlayerIndex > 0)
        {
            game.SetupPlayerIndex--;
            game.SetupStep = SetupStep.PlaceCamp;
            SetCurrentPlayerFromSetup(game);
            AddLog(game, $"{game.CurrentPlayer.PlayerName}'s second setup placement begins.", game.CurrentPlayer.PlayerId);
            return;
        }

        CompleteSetup(game);
    }

    private static void SetCurrentPlayerFromSetup(GameState game)
    {
        var currentSetupPlayerId = game.CurrentSetupPlayerId;
        if (currentSetupPlayerId is null)
        {
            return;
        }

        var playerIndex = game.Players.FindIndex(player =>
            string.Equals(player.PlayerId, currentSetupPlayerId, StringComparison.OrdinalIgnoreCase));

        if (playerIndex >= 0)
        {
            game.CurrentPlayerIndex = playerIndex;
        }
    }

    private static void CompleteSetup(GameState game)
    {
        game.Phase = GamePhase.NormalTurn;
        game.SetupRound = null;
        game.SetupStep = null;
        game.SetupDirection = null;
        game.SetupPlayerIndex = 0;
        game.LastSetupCampVertexId = null;
        game.CurrentPlayerIndex = GetPlayerIndex(game, game.PlayerOrder.First());
        game.TurnNumber = 1;
        game.LastDiceRoll = null;
        game.HasRolledThisTurn = false;
        game.PendingWardenAction = WardenAction.None;
        game.CurrentWardenPlayerId = null;
        game.PendingWardenDiscards.Clear();
        game.WardenVictimOptions.Clear();
        game.TradeOffers.Clear();
        game.LargestArmyPlayerId = null;
        game.LargestArmyKnightCount = 0;
        game.LargestArmyAwardedAtTurn = null;
        game.LongestTrailPlayerId = null;
        game.LongestTrailLength = 0;

        foreach (var player in game.Players)
        {
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
            player.LongestTrailLength = LongestTrailService.CalculateLongestTrail(game, player.PlayerId);
        }

        AddLog(game, $"Setup complete. {game.CurrentPlayer.PlayerName} takes the first turn.");
    }

    public static void ApplyDiceRoll(
        GameState game,
        PlayerGameState roller,
        int diceValue,
        bool isDebug,
        string? actorPlayerId = null)
    {
        game.LastDiceRoll = diceValue;
        game.HasRolledThisTurn = true;

        AddLog(
            game,
            isDebug
                ? $"[DEBUG] Forced dice result to {diceValue}."
                : $"{roller.PlayerName} rolled {diceValue}.",
            isDebug ? actorPlayerId : roller.PlayerId);

        if (diceValue == 7)
        {
            BeginRolledSevenWardenFlow(game, roller.PlayerId);
            return;
        }

        var gains = new Dictionary<string, Dictionary<ResourceType, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tile in game.Board.Tiles)
        {
            if (tile.NumberToken != diceValue)
            {
                continue;
            }

            if (tile.IsBlocked || string.Equals(tile.TileId, game.WardenTileId, StringComparison.OrdinalIgnoreCase))
            {
                AddLog(game, $"The Warden blocked production on {FormatTileResource(tile)}.");
                continue;
            }

            var resource = ToSupplyResource(tile.ResourceType);
            if (resource is null)
            {
                continue;
            }

            var adjacentVertices = game.Board.Vertices
                .Where(vertex => vertex.AdjacentTileIds.Contains(tile.TileId, StringComparer.OrdinalIgnoreCase));

            foreach (var vertex in adjacentVertices)
            {
                if (vertex.OwnerPlayerId is null || vertex.StructureType is null)
                {
                    continue;
                }

                var owner = game.Players.FirstOrDefault(player =>
                    string.Equals(player.PlayerId, vertex.OwnerPlayerId, StringComparison.OrdinalIgnoreCase));
                if (owner is null || owner.HasForfeited)
                {
                    continue;
                }

                var amount = vertex.StructureType == BoardStructureType.Stronghold ? 2 : 1;
                owner.Supplies[resource.Value] += amount;

                if (!gains.TryGetValue(owner.PlayerId, out var playerGains))
                {
                    playerGains = new Dictionary<ResourceType, int>();
                    gains[owner.PlayerId] = playerGains;
                }

                playerGains[resource.Value] = playerGains.GetValueOrDefault(resource.Value) + amount;
            }
        }

        if (gains.Count == 0)
        {
            AddLog(game, "No supplies produced.");
            return;
        }

        foreach (var player in game.Players)
        {
            if (!gains.TryGetValue(player.PlayerId, out var playerGains))
            {
                continue;
            }

            AddLog(game, $"{player.PlayerName} produced {FormatResourceSummary(playerGains)}.", player.PlayerId);
        }
    }

    private static void BeginRolledSevenWardenFlow(GameState game, string playerId)
    {
        ExpirePendingTradeOffers(game, playerId);
        game.PendingWardenDiscards.Clear();
        game.WardenVictimOptions.Clear();
        game.CurrentWardenPlayerId = playerId;

        foreach (var player in game.Players)
        {
            if (player.HasForfeited)
            {
                continue;
            }

            var supplyCount = player.Supplies.Values.Sum();
            if (supplyCount <= 7)
            {
                continue;
            }

            game.PendingWardenDiscards.Add(new WardenDiscardRequirement
            {
                PlayerId = player.PlayerId,
                RequiredAmount = supplyCount / 2
            });
        }

        if (game.PendingWardenDiscards.Count > 0)
        {
            game.PendingWardenAction = WardenAction.Discarding;
            AddLog(game, "The Warden was activated. Players over 7 supplies must discard half.");
            return;
        }

        AddLog(game, "The Warden was activated. No players needed to discard.");
        StartWardenMove(game, playerId);
    }

    private static void StartWardenMove(GameState game, string playerId)
    {
        game.PendingWardenDiscards.Clear();
        game.WardenVictimOptions.Clear();
        game.CurrentWardenPlayerId = playerId;
        game.PendingWardenAction = WardenAction.MoveWarden;
    }

    private static void EnsureCurrentWardenPlayer(GameState game, string playerId, string message)
    {
        if (!string.Equals(game.CurrentPlayer.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(game.CurrentWardenPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException(message);
        }
    }

    private static void MoveWardenToTile(GameState game, string targetTileId, bool allowCurrentTile = false)
    {
        if (!allowCurrentTile && string.Equals(game.WardenTileId, targetTileId, StringComparison.OrdinalIgnoreCase))
        {
            throw new GameRuleException("The Warden must move to a different tile.");
        }

        var targetTile = game.Board.Tiles.FirstOrDefault(tile =>
                string.Equals(tile.TileId, targetTileId, StringComparison.OrdinalIgnoreCase))
            ?? throw new GameRuleException("Choose a valid tile for the Warden.");

        game.WardenTileId = targetTile.TileId;
        foreach (var tile in game.Board.Tiles)
        {
            tile.IsBlocked = string.Equals(tile.TileId, targetTile.TileId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<PlayerGameState> GetEligibleWardenVictims(GameState game, string playerId, string tileId)
    {
        var adjacentOpponentIds = game.Board.Vertices
            .Where(vertex => vertex.AdjacentTileIds.Contains(tileId, StringComparer.OrdinalIgnoreCase))
            .Where(vertex => vertex.OwnerPlayerId is not null && vertex.StructureType is not null)
            .Select(vertex => vertex.OwnerPlayerId!)
            .Where(ownerPlayerId => !string.Equals(ownerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return game.Players
            .Where(player => adjacentOpponentIds.Contains(player.PlayerId))
            .Where(player => !player.HasForfeited)
            .Where(player => player.Supplies.Values.Sum() > 0)
            .ToList();
    }

    private static void CompleteWardenFlow(GameState game, bool checkWinner = true)
    {
        var wardenPlayer = game.CurrentWardenPlayerId is null
            ? null
            : game.Players.FirstOrDefault(player =>
                string.Equals(player.PlayerId, game.CurrentWardenPlayerId, StringComparison.OrdinalIgnoreCase));

        game.PendingWardenAction = WardenAction.None;
        game.CurrentWardenPlayerId = null;
        game.PendingWardenDiscards.Clear();
        game.WardenVictimOptions.Clear();

        if (checkWinner && wardenPlayer is not null)
        {
            CheckWinner(game, wardenPlayer);
        }
    }

    private static int GetPlayerIndex(GameState game, string playerId)
    {
        var playerIndex = game.Players.FindIndex(player =>
            string.Equals(player.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        return playerIndex >= 0 ? playerIndex : 0;
    }

    private static ResourceType? ToSupplyResource(TileResourceType resourceType)
    {
        return resourceType switch
        {
            TileResourceType.Wood => ResourceType.Wood,
            TileResourceType.Clay => ResourceType.Clay,
            TileResourceType.Wool => ResourceType.Wool,
            TileResourceType.Grain => ResourceType.Grain,
            TileResourceType.Stone => ResourceType.Stone,
            TileResourceType.None => null,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
        };
    }

    private static string FormatResourceSummary(IReadOnlyDictionary<ResourceType, int> resources)
    {
        return string.Join(", ", ResourceTypes.All
            .Where(resource => resources.TryGetValue(resource, out var amount) && amount > 0)
            .Select(resource => $"{resources[resource]} {resource}"));
    }

    private static string FormatStartingSupplySummary(IReadOnlyDictionary<ResourceType, int> resources)
    {
        return string.Join(", ", ResourceTypes.All
            .Where(resource => resources.TryGetValue(resource, out var amount) && amount > 0)
            .Select(resource => $"+{resources[resource]} {resource}"));
    }

    private static string FormatTileResource(HexTile tile)
    {
        return tile.ResourceType == TileResourceType.None
            ? "the Desert"
            : tile.ResourceType.ToString();
    }

    private static ResourceType? TryStealRandomResource(GameState game, PlayerGameState player, string? victimPlayerId)
    {
        if (string.IsNullOrWhiteSpace(victimPlayerId))
        {
            return null;
        }

        var victim = game.Players.FirstOrDefault(candidate => candidate.PlayerId == victimPlayerId);
        if (victim is null || victim.PlayerId == player.PlayerId)
        {
            throw new GameRuleException("Choose a valid opponent to steal from.");
        }

        var available = victim.Supplies
            .Where(item => item.Value > 0)
            .SelectMany(item => Enumerable.Repeat(item.Key, item.Value))
            .ToList();

        if (available.Count == 0)
        {
            return null;
        }

        var resource = available[Random.Shared.Next(available.Count)];
        victim.Supplies[resource]--;
        player.Supplies[resource]++;
        return resource;
    }

    private static void MarkActionCardPlayed(GameState game, PlayerGameState player, PlayerDevelopmentCard card)
    {
        card.IsPlayed = true;
        player.HasPlayedDevelopmentCardThisTurn = true;
        player.ActiveDevelopmentCardEffect = null;
    }

    private static void Spend(PlayerGameState player, IReadOnlyDictionary<ResourceType, int> cost, string? insufficientMessage = null)
    {
        foreach (var (resource, amount) in cost)
        {
            if (player.Supplies[resource] < amount)
            {
                throw new GameRuleException(insufficientMessage ?? $"You need {FormatCost(cost)}.");
            }
        }

        foreach (var (resource, amount) in cost)
        {
            player.Supplies[resource] -= amount;
        }
    }

    private static string FormatCost(IReadOnlyDictionary<ResourceType, int> cost)
    {
        return string.Join(", ", cost.Select(item => $"{item.Value} {item.Key}"));
    }

    private static void UpdateLargestArmy(GameState game, PlayerGameState player)
    {
        if (player.PlayedKnightCount < 3)
        {
            return;
        }

        var currentHolder = game.LargestArmyPlayerId is null
            ? null
            : game.Players.FirstOrDefault(player => player.PlayerId == game.LargestArmyPlayerId);

        AddLog(game, $"{player.PlayerName} now has {player.PlayedKnightCount} played Knights.", player.PlayerId);

        if (currentHolder is null)
        {
            game.LargestArmyPlayerId = player.PlayerId;
            game.LargestArmyKnightCount = player.PlayedKnightCount;
            game.LargestArmyAwardedAtTurn = game.TurnNumber;
            AddLog(game, $"{player.PlayerName} claimed Largest Army.", player.PlayerId);
            return;
        }

        if (currentHolder.PlayerId == player.PlayerId)
        {
            game.LargestArmyKnightCount = player.PlayedKnightCount;
            return;
        }

        if (player.PlayedKnightCount <= currentHolder.PlayedKnightCount)
        {
            return;
        }

        game.LargestArmyPlayerId = player.PlayerId;
        game.LargestArmyKnightCount = player.PlayedKnightCount;
        game.LargestArmyAwardedAtTurn = game.TurnNumber;
        AddLog(game, $"{player.PlayerName} took Largest Army from {currentHolder.PlayerName}.", player.PlayerId);
    }

    private static void RecalculateLargestArmyAfterForfeit(GameState game, string forfeitedPlayerId)
    {
        if (!string.Equals(game.LargestArmyPlayerId, forfeitedPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var eligiblePlayers = game.Players
            .Where(player => !player.HasForfeited && player.PlayedKnightCount >= 3)
            .ToList();
        var highestCount = eligiblePlayers.Count == 0
            ? 0
            : eligiblePlayers.Max(player => player.PlayedKnightCount);
        var leadingPlayers = eligiblePlayers
            .Where(player => player.PlayedKnightCount == highestCount)
            .ToList();
        var nextHolder = leadingPlayers.Count == 1 ? leadingPlayers[0] : null;

        game.LargestArmyPlayerId = nextHolder?.PlayerId;
        game.LargestArmyKnightCount = nextHolder?.PlayedKnightCount ?? 0;
        game.LargestArmyAwardedAtTurn = nextHolder is null ? null : game.TurnNumber;

        if (nextHolder is not null)
        {
            AddLog(game, $"{nextHolder.PlayerName} claimed Largest Army after a player forfeited.", nextHolder.PlayerId);
        }
    }

    public static void RecalculateLongestTrail(GameState game, string? actorPlayerId = null, bool checkWinner = true)
    {
        var previousHolderId = game.LongestTrailPlayerId;
        var previousLength = game.LongestTrailLength;
        var previousHolder = previousHolderId is null
            ? null
            : game.Players.FirstOrDefault(player => player.PlayerId == previousHolderId);

        foreach (var player in game.Players)
        {
            player.LongestTrailLength = player.HasForfeited
                ? 0
                : LongestTrailService.CalculateLongestTrail(game, player.PlayerId);
        }

        var qualifiedPlayers = game.Players
            .Where(player => !player.HasForfeited && player.LongestTrailLength >= LongestTrailService.MinimumTrailLength)
            .ToList();
        var bestLength = qualifiedPlayers.Count == 0
            ? 0
            : qualifiedPlayers.Max(player => player.LongestTrailLength);
        var bestPlayers = qualifiedPlayers
            .Where(player => player.LongestTrailLength == bestLength)
            .ToList();

        PlayerGameState? nextHolder = null;
        if (previousHolder is not null && previousHolder.LongestTrailLength >= LongestTrailService.MinimumTrailLength)
        {
            if (bestLength <= previousHolder.LongestTrailLength)
            {
                nextHolder = previousHolder;
            }
            else if (bestPlayers.Count == 1)
            {
                nextHolder = bestPlayers[0];
            }
        }
        else if (bestPlayers.Count == 1)
        {
            nextHolder = bestPlayers[0];
        }

        if (previousHolder is not null && previousHolder.LongestTrailLength < previousLength)
        {
            AddLog(game, $"{previousHolder.PlayerName}'s Trail was interrupted.", previousHolder.PlayerId);
        }

        game.LongestTrailPlayerId = nextHolder?.PlayerId;
        game.LongestTrailLength = nextHolder?.LongestTrailLength ?? 0;

        if (previousHolderId == game.LongestTrailPlayerId)
        {
            return;
        }

        if (nextHolder is null)
        {
            if (previousHolderId is not null)
            {
                AddLog(game, "Longest Trail is currently unclaimed.", actorPlayerId);
            }

            return;
        }

        if (previousHolderId is null)
        {
            AddLog(game, $"{nextHolder.PlayerName} claimed Longest Trail with {nextHolder.LongestTrailLength} Trails.", nextHolder.PlayerId);
        }
        else
        {
            AddLog(game, $"{nextHolder.PlayerName} took Longest Trail with {nextHolder.LongestTrailLength} Trails.", nextHolder.PlayerId);
        }

        if (checkWinner)
        {
            CheckWinner(game, nextHolder);
        }
    }

    public static void CheckWinner(GameState game, PlayerGameState player)
    {
        if (player.HasForfeited)
        {
            return;
        }

        if (CalculateVictoryPoints(game, player, revealHidden: true) < game.WinningVictoryPoints)
        {
            return;
        }

        ExpirePendingTradeOffers(game, player.PlayerId);
        game.Status = GameStatus.Finished;
        game.Phase = GamePhase.Finished;
        game.WinnerPlayerId = player.PlayerId;
        game.FinishedAt = DateTimeOffset.UtcNow;
        AddLog(game, $"{player.PlayerName} reached {game.WinningVictoryPoints} Victory Points and won the match.", player.PlayerId);
    }

    public static int CalculateVictoryPoints(GameState game, PlayerGameState player, bool revealHidden)
    {
        if (player.HasForfeited)
        {
            return 0;
        }

        if (player.DebugVictoryPointOverride is int debugPoints)
        {
            return debugPoints;
        }

        var points = player.CampsBuilt + player.StrongholdsBuilt * 2;

        if (game.LargestArmyPlayerId == player.PlayerId)
        {
            points += 2;
        }

        if (game.LongestTrailPlayerId == player.PlayerId)
        {
            points += 2;
        }

        if (revealHidden || game.Status == GameStatus.Finished)
        {
            points += player.DevelopmentCards.Count(card => card.Type == DevelopmentCardType.VictoryPoint);
        }

        return points;
    }

    public static void AddLog(GameState game, string message, string? playerId = null)
    {
        game.Log.Add(new GameLogEntry(
            game.Log.Count + 1,
            DateTimeOffset.UtcNow,
            message,
            playerId));
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
