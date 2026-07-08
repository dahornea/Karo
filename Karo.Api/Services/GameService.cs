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
    private readonly Dictionary<string, GameState> _games = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GameService>? _logger;

    public GameService(BoardGenerator boardGenerator, ILogger<GameService>? logger = null)
    {
        _boardGenerator = boardGenerator;
        _logger = logger;
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

    public GameState RestartGame(Room room)
    {
        lock (_gate)
        {
            var game = CreateGame(room);
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

    public GameState EndTurn(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);

            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
            game.LastDiceRoll = null;
            game.HasRolledThisTurn = false;
            game.CurrentPlayerIndex = (game.CurrentPlayerIndex + 1) % game.Players.Count;
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
                setupMessage: "You cannot buy Development Cards during setup.",
                rollMessage: "You must roll before buying a Development Card.");

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
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);

            if (player.ActiveDevelopmentCardEffect?.Type != ActiveDevelopmentCardType.RoadBuilding)
            {
                throw new GameRuleException("Road Building is not active.");
            }

            throw new GameRuleException("Free Trail placement will be available when the trail board layer is implemented.");
        }
    }

    public GameState CancelActiveDevelopmentCard(string roomCode, string playerId)
    {
        lock (_gate)
        {
            var (game, player) = EnsureNormalTurnCurrentPlayer(roomCode, playerId);
            player.ActiveDevelopmentCardEffect = null;
            AddLog(game, $"{player.PlayerName} canceled their active development effect.", player.PlayerId);
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
        return board.Tiles.FirstOrDefault(tile => tile.ResourceType == TileResourceType.None)
            ?? board.Tiles.First(tile => tile.NumberToken is null);
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

    private GameState CreateGame(Room room)
    {
        var board = _boardGenerator.Generate();
        var wardenTile = PickStartingWardenTile(board);
        wardenTile.IsBlocked = true;

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
                IsHost = player.IsHost
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
        string? rollMessage = null)
    {
        var (game, player) = EnsureCurrentPlayer(roomCode, playerId);

        if (game.Phase != GamePhase.NormalTurn)
        {
            throw new GameRuleException(setupMessage ?? "The match is still in setup.");
        }

        if (requireRolled && !game.HasRolledThisTurn)
        {
            throw new GameRuleException(rollMessage ?? "You must roll dice before taking this action.");
        }

        if (game.PendingWardenAction != WardenAction.None)
        {
            throw new GameRuleException("Resolve the Warden action first.");
        }

        return (game, player);
    }

    private (GameState Game, PlayerGameState Player) EnsureSetupPlayer(string roomCode, string playerId)
    {
        var game = EnsureGame(roomCode);

        if (game.Status == GameStatus.Finished)
        {
            throw new GameRuleException("This match has already finished.");
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
            setupMessage: "You cannot play Development Cards during setup.",
            rollMessage: "You must roll before playing a Development Card.");
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
            throw new GameRuleException("You already played a Development Card this turn.");
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

    private static void EnsureMatchInProgress(GameState game)
    {
        if (game.Status == GameStatus.Finished)
        {
            throw new GameRuleException("This match has already finished.");
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
        game.LargestArmyPlayerId = null;
        game.LargestArmyKnightCount = 0;
        game.LargestArmyAwardedAtTurn = null;

        foreach (var player in game.Players)
        {
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
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
                if (owner is null)
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
        game.PendingWardenDiscards.Clear();
        game.WardenVictimOptions.Clear();
        game.CurrentWardenPlayerId = playerId;

        foreach (var player in game.Players)
        {
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

    public static void CheckWinner(GameState game, PlayerGameState player)
    {
        if (CalculateVictoryPoints(game, player, revealHidden: true) < game.WinningVictoryPoints)
        {
            return;
        }

        game.Status = GameStatus.Finished;
        game.Phase = GamePhase.Finished;
        game.WinnerPlayerId = player.PlayerId;
        game.FinishedAt = DateTimeOffset.UtcNow;
        AddLog(game, $"{player.PlayerName} reached {game.WinningVictoryPoints} Victory Points and won the match.", player.PlayerId);
    }

    public static int CalculateVictoryPoints(GameState game, PlayerGameState player, bool revealHidden)
    {
        if (player.DebugVictoryPointOverride is int debugPoints)
        {
            return debugPoints;
        }

        var points = player.CampsBuilt + player.StrongholdsBuilt * 2;

        if (game.LargestArmyPlayerId == player.PlayerId)
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
