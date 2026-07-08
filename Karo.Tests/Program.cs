using Karo.Api.DTOs;
using Karo.Api.Models;
using Karo.Api.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var tests = new (string Name, Action Run)[]
{
    ("Final resource enum and official-style costs", ResourceEnumAndCosts),
    ("Development deck composition", DevelopmentDeckComposition),
    ("Board generation creates 9 coastal harbor slots", BoardHarborSlotGeneration),
    ("Board generation creates 9 valid ports", BoardPortGeneration),
    ("Setup phase places Camps and Trails in forward then reverse order", SetupPhasePlacementFlow),
    ("First setup Camp grants no starting supplies", FirstSetupCampGrantsNoStartingSupplies),
    ("Second setup Camp grants every adjacent productive resource", SecondSetupCampGrantsAllAdjacentProductiveResources),
    ("Second setup Camp skips Desert starting resources", SecondSetupCampSkipsDesertResources),
    ("Second setup Camp grants duplicate resource types from separate tiles", SecondSetupCampGrantsDuplicateResourceTypes),
    ("Second setup Camp uses the actual placed node for starting resources", SecondSetupCampUsesActualPlacedNode),
    ("Normal turn actions require a dice roll", NormalTurnDiceGate),
    ("Dice production pays adjacent Camps and Strongholds", DiceProduction),
    ("Warden starts on Desert and blocks production", WardenStartAndProductionBlock),
    ("Rolling 7 runs Warden discard, move, and steal flow", WardenRollSevenFlow),
    ("Rolling 7 skips production and bypasses low-supply discards", WardenSevenSkipsProductionAndLowSupplyDiscards),
    ("Warden movement validates target and victim eligibility", WardenMovementAndVictimValidation),
    ("Pending Warden action blocks normal actions", PendingWardenActionBlocksNormalActions),
    ("Development cards are blocked during setup and before rolling", DevelopmentCardsSetupAndRollGate),
    ("Buying development cards validates cost, deck, ownership, and privacy", BuyingDevelopmentCards),
    ("Maritime trading uses default, generic, and specific harbor rates", MaritimeTradingRates),
    ("Development card play restrictions", DevelopmentCardPlayRestrictions),
    ("Year of Plenty and Monopoly effects", YearOfPlentyAndMonopoly),
    ("Knight, Warden movement, random steal, and Strongest Guard", KnightAndLargestArmy),
    ("Knight starts Warden flow without discard", KnightStartsWardenFlow),
    ("Road Building active effect limitation is explicit", RoadBuildingEffect),
    ("Victory Point card can trigger a 10 VP win", VictoryPointWin),
    ("Debug actions are rejected outside Development", DebugActionsRejectOutsideDevelopment),
    ("Debug resource actions update player resources in Development", DebugResourceActions),
    ("Debug force dice updates roll state in Development", DebugForceDice),
    ("Debug development-card actions update cards and deck readout in Development", DebugDevelopmentCardActions),
    ("Debug actions require an in-room host", DebugAuthorization)
};

var failures = 0;

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex.Message);
    }
}

return failures == 0 ? 0 : 1;

static void ResourceEnumAndCosts()
{
    AssertSequence(
        new[] { "Wood", "Clay", "Wool", "Grain", "Stone" },
        Enum.GetNames<ResourceType>(),
        "ResourceType should contain only final Karo supplies.");
    AssertSequence(
        new[] { "Generic", "Wood", "Clay", "Wool", "Grain", "Stone" },
        Enum.GetNames<HarborType>(),
        "HarborType should contain the Karo harbor assignments.");

    AssertCost(GameService.TrailCost, (ResourceType.Wood, 1), (ResourceType.Clay, 1));
    AssertCost(GameService.CampCost, (ResourceType.Wood, 1), (ResourceType.Clay, 1), (ResourceType.Wool, 1), (ResourceType.Grain, 1));
    AssertCost(GameService.StrongholdCost, (ResourceType.Grain, 2), (ResourceType.Stone, 3));
    AssertCost(GameService.DevelopmentCardCost, (ResourceType.Wool, 1), (ResourceType.Grain, 1), (ResourceType.Stone, 1));
}

static void DevelopmentDeckComposition()
{
    var (_, _, game) = CreateGame();
    AssertEqual(25, game.DevelopmentDeck.Count, "Deck should contain 25 development cards.");
    AssertEqual(14, game.DevelopmentDeck.Count(card => card.Type == DevelopmentCardType.Knight), "Knight count mismatch.");
    AssertEqual(2, game.DevelopmentDeck.Count(card => card.Type == DevelopmentCardType.RoadBuilding), "Road Building count mismatch.");
    AssertEqual(2, game.DevelopmentDeck.Count(card => card.Type == DevelopmentCardType.YearOfPlenty), "Year of Plenty count mismatch.");
    AssertEqual(2, game.DevelopmentDeck.Count(card => card.Type == DevelopmentCardType.Monopoly), "Monopoly count mismatch.");
    AssertEqual(5, game.DevelopmentDeck.Count(card => card.Type == DevelopmentCardType.VictoryPoint), "Victory Point count mismatch.");
}

static void BoardHarborSlotGeneration()
{
    var (_, _, game) = CreateGame();
    var board = game.Board;
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    var currentDto = game.ToDto(current.PlayerId);
    var opponentDto = game.ToDto(opponent.PlayerId);

    BoardGenerator.ValidateHarborSlots(board);
    AssertEqual(19, board.Tiles.Count, "Board should contain 19 land tiles.");
    AssertEqual(9, board.HarborSlots.Count, "Board should contain 9 harbor slots.");
    AssertEqual(9, currentDto.Board.HarborSlots.Count, "DTO should expose all 9 harbor slots.");
    AssertSequence(
        currentDto.Board.HarborSlots.Select(slot => $"{slot.HarborSlotId}:{slot.HarborType}:{slot.TradeRate}").ToList(),
        opponentDto.Board.HarborSlots.Select(slot => $"{slot.HarborSlotId}:{slot.HarborType}:{slot.TradeRate}").ToList(),
        "All players should receive the same backend-assigned harbor data.");
    AssertEqual(
        board.HarborSlots.Count,
        board.HarborSlots.Select(slot => slot.HarborSlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        "Harbor slot IDs should be unique.");
    AssertEqual(
        board.HarborSlots.Count,
        board.HarborSlots.Select(slot => slot.AdjacentEdgeId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        "Harbor slots should not duplicate coastal edges.");
    AssertEqual(4, board.HarborSlots.Count(slot => slot.HarborType == HarborType.Generic && slot.TradeRate == 3), "Board should contain four Generic 3:1 harbors.");

    foreach (var resource in ResourceTypes.All)
    {
        var harborType = ToHarborType(resource);
        AssertEqual(
            1,
            board.HarborSlots.Count(slot => slot.HarborType == harborType && slot.TradeRate == 2),
            $"Board should contain one {resource} 2:1 harbor.");
    }

    foreach (var slot in board.HarborSlots)
    {
        AssertEqual(2, slot.AdjacentVertexIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), $"Slot {slot.HarborSlotId} should have 2 adjacent nodes.");
        Assert(slot.HarborType is not null, $"Slot {slot.HarborSlotId} should have an assigned harbor type.");
        Assert(slot.TradeRate is not null, $"Slot {slot.HarborSlotId} should have an assigned trade rate.");
        Assert(!string.IsNullOrWhiteSpace(slot.AdjacentEdgeId), $"Slot {slot.HarborSlotId} should reference a coastal edge.");

        foreach (var vertexId in slot.AdjacentVertexIds)
        {
            var vertex = board.Vertices.FirstOrDefault(candidate => candidate.VertexId == vertexId);
            Assert(vertex is not null, $"Slot {slot.HarborSlotId} should reference existing node {vertexId}.");
            Assert(vertex!.IsCoastal, $"Slot {slot.HarborSlotId} should not reference an interior-only node.");
        }
    }
}

static void BoardPortGeneration()
{
    var board = new BoardGenerator().Generate();
    BoardGenerator.ValidatePorts(board);

    AssertEqual(9, board.Ports.Count, "Board should contain 9 ports.");
    AssertEqual(4, board.Ports.Count(port => port.Type == PortType.Generic3To1), "Board should contain 4 generic ports.");
    AssertEqual(5, board.Ports.Count(port => port.Type == PortType.Specific2To1), "Board should contain 5 specific ports.");

    foreach (var resource in ResourceTypes.All)
    {
        AssertEqual(
            1,
            board.Ports.Count(port => port.Type == PortType.Specific2To1 && port.ResourceType == resource),
            $"Board should contain one {resource} 2:1 port.");
    }

    AssertEqual(
        board.Ports.Count,
        board.Ports.Select(port => $"{port.TileQ}:{port.TileR}:{port.EdgeIndex}").Distinct().Count(),
        "No two ports should occupy the same coastal edge.");

    foreach (var port in board.Ports)
    {
        AssertEqual(2, port.AdjacentVertexIds.Distinct().Count(), $"Port {port.Id} should connect to two distinct vertices.");

        foreach (var vertexId in port.AdjacentVertexIds)
        {
            var vertex = board.Vertices.FirstOrDefault(candidate => candidate.VertexId == vertexId);
            Assert(vertex is not null, $"Port {port.Id} should reference an existing vertex.");
            Assert(vertex!.IsCoastal, $"Port {port.Id} should reference coastal vertices.");
        }
    }
}

static void SetupPhasePlacementFlow()
{
    var (_, service, game) = CreateGame();
    AssertEqual(GamePhase.Setup, game.Phase, "New matches should begin in setup.");
    AssertEqual(SetupRound.FirstPlacement, game.SetupRound, "Setup should begin in the first placement round.");
    AssertEqual(SetupStep.PlaceCamp, game.SetupStep, "Setup should begin by placing a Camp.");
    AssertEqual(2, game.PlayerOrder.Count, "Player order should be stored once at match start.");

    var firstPlayer = game.CurrentPlayer;
    var firstCamp = FindValidSetupVertex(game);
    service.PlaceSetupCamp(game.RoomCode, firstPlayer.PlayerId, firstCamp.VertexId);
    AssertEqual(SetupStep.PlaceTrail, game.SetupStep, "A setup Camp should require a connected Trail next.");

    var invalidTrail = game.Board.Edges.First(edge => !EdgeTouches(edge, firstCamp.VertexId));
    ExpectRuleError(() => service.PlaceSetupTrail(game.RoomCode, firstPlayer.PlayerId, invalidTrail.EdgeId), "connect");
    service.PlaceSetupTrail(game.RoomCode, firstPlayer.PlayerId, FindSetupTrail(game, firstCamp.VertexId).EdgeId);

    var secondPlayer = game.CurrentPlayer;
    Assert(firstPlayer.PlayerId != secondPlayer.PlayerId, "First round should advance to the next player.");

    var secondCamp = FindValidSetupVertex(game);
    service.PlaceSetupCamp(game.RoomCode, secondPlayer.PlayerId, secondCamp.VertexId);
    service.PlaceSetupTrail(game.RoomCode, secondPlayer.PlayerId, FindSetupTrail(game, secondCamp.VertexId).EdgeId);

    AssertEqual(SetupRound.SecondPlacement, game.SetupRound, "Setup should reverse after the first round.");
    AssertEqual(secondPlayer.PlayerId, game.CurrentPlayer.PlayerId, "The last first-round player should place again first in reverse order.");

    var secondRoundCamp = FindValidSetupVertex(game, preferProduction: true);
    var suppliesBefore = secondPlayer.Supplies.Values.Sum();
    service.PlaceSetupCamp(game.RoomCode, secondPlayer.PlayerId, secondRoundCamp.VertexId);
    Assert(secondPlayer.Supplies.Values.Sum() > suppliesBefore, "Second setup Camp should grant starting supplies from adjacent producing regions.");
    service.PlaceSetupTrail(game.RoomCode, secondPlayer.PlayerId, FindSetupTrail(game, secondRoundCamp.VertexId).EdgeId);

    AssertEqual(firstPlayer.PlayerId, game.CurrentPlayer.PlayerId, "Reverse setup should return to the first player.");
    var finalCamp = FindValidSetupVertex(game, preferProduction: true);
    service.PlaceSetupCamp(game.RoomCode, firstPlayer.PlayerId, finalCamp.VertexId);
    service.PlaceSetupTrail(game.RoomCode, firstPlayer.PlayerId, FindSetupTrail(game, finalCamp.VertexId).EdgeId);

    AssertEqual(GamePhase.NormalTurn, game.Phase, "Setup should complete after every player places twice.");
    AssertEqual<int?>(null, game.LastDiceRoll, "Normal turns should begin with no dice result.");
    Assert(!game.HasRolledThisTurn, "Normal turns should begin with dice still unrolled.");
    AssertEqual(game.PlayerOrder[0], game.CurrentPlayer.PlayerId, "The first randomized player should take the first normal turn.");
}

static void FirstSetupCampGrantsNoStartingSupplies()
{
    var (_, service, game) = CreateGame();
    var player = game.CurrentPlayer;
    var vertex = FindValidSetupVertexWithAdjacentTileCount(game, 3);
    SetAdjacentTileResources(game, vertex, TileResourceType.Wool, TileResourceType.Stone, TileResourceType.Grain);
    ClearSupplies(player);

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, vertex.VertexId);

    AssertEqual(0, player.Supplies.Values.Sum(), "First setup Camp should not grant starting supplies.");
    Assert(
        game.Log.All(entry => !entry.Message.Contains("gained starting supplies", StringComparison.OrdinalIgnoreCase)),
        "First setup Camp should not log starting supplies.");
}

static void SecondSetupCampGrantsAllAdjacentProductiveResources()
{
    var (_, service, game, player) = PrepareSecondSetupCampTest();
    var vertex = FindValidSetupVertexWithAdjacentTileCount(game, 3);
    SetAdjacentTileResources(game, vertex, TileResourceType.Wool, TileResourceType.Stone, TileResourceType.Grain);

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, vertex.VertexId);

    AssertEqual(3, player.Supplies.Values.Sum(), "Second setup Camp should grant one supply from each of 3 productive adjacent tiles.");
    AssertEqual(1, player.Supplies[ResourceType.Wool], "Second setup Camp should grant Wool from its adjacent Wool tile.");
    AssertEqual(1, player.Supplies[ResourceType.Stone], "Second setup Camp should grant Stone from its adjacent Stone tile.");
    AssertEqual(1, player.Supplies[ResourceType.Grain], "Second setup Camp should grant Grain from its adjacent Grain tile.");
    Assert(
        game.Log.Any(entry => entry.Message.Contains("+1 Wool, +1 Grain, +1 Stone", StringComparison.OrdinalIgnoreCase)),
        "Game log should list the full starting supplies granted.");
}

static void SecondSetupCampSkipsDesertResources()
{
    var (_, service, game, player) = PrepareSecondSetupCampTest();
    var vertex = FindValidSetupVertexWithAdjacentTileCount(game, 3);
    SetAdjacentTileResources(game, vertex, TileResourceType.Wood, TileResourceType.None, TileResourceType.Clay);

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, vertex.VertexId);

    AssertEqual(2, player.Supplies.Values.Sum(), "Second setup Camp should skip Desert/None adjacent tiles.");
    AssertEqual(1, player.Supplies[ResourceType.Wood], "Second setup Camp should grant Wood.");
    AssertEqual(1, player.Supplies[ResourceType.Clay], "Second setup Camp should grant Clay.");
}

static void SecondSetupCampGrantsDuplicateResourceTypes()
{
    var (_, service, game, player) = PrepareSecondSetupCampTest();
    var vertex = FindValidSetupVertexWithAdjacentTileCount(game, 3);
    SetAdjacentTileResources(game, vertex, TileResourceType.Wool, TileResourceType.Wool, TileResourceType.None);

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, vertex.VertexId);

    AssertEqual(2, player.Supplies.Values.Sum(), "Second setup Camp should count separate productive tiles of the same resource.");
    AssertEqual(2, player.Supplies[ResourceType.Wool], "Two adjacent Wool tiles should grant +2 Wool.");
}

static void SecondSetupCampUsesActualPlacedNode()
{
    var (_, service, game, player) = PrepareSecondSetupCampTest();
    var actualVertex = FindValidSetupVertexWithAdjacentTileCount(game, 3);
    var decoyVertex = game.Board.Vertices
        .Where(vertex => vertex.VertexId != actualVertex.VertexId && vertex.AdjacentTileIds.Count >= 3)
        .OrderBy(vertex => vertex.VertexId)
        .First();

    SetAdjacentTileResources(game, decoyVertex, TileResourceType.Stone, TileResourceType.Stone, TileResourceType.Stone);
    SetAdjacentTileResources(game, actualVertex, TileResourceType.Wood, TileResourceType.Clay, TileResourceType.Grain);
    game.LastSetupCampVertexId = decoyVertex.VertexId;

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, actualVertex.VertexId);

    AssertEqual(3, player.Supplies.Values.Sum(), "Starting supplies should use the Camp node that was just placed.");
    AssertEqual(1, player.Supplies[ResourceType.Wood], "Actual placed node should grant Wood.");
    AssertEqual(1, player.Supplies[ResourceType.Clay], "Actual placed node should grant Clay.");
    AssertEqual(1, player.Supplies[ResourceType.Grain], "Actual placed node should grant Grain.");
    AssertEqual(0, player.Supplies[ResourceType.Stone], "A stale nearby/previous node should not drive starting supplies.");
}

static void NormalTurnDiceGate()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;

    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 6;
    }

    ExpectRuleError(() => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId), "roll");
    ExpectRuleError(() => service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Clay), "You must roll before trading.");
    ExpectRuleError(() => service.EndTurn(game.RoomCode, current.PlayerId), "roll");

    service.ForceDiceRollForDebug(game.RoomCode, current.PlayerId, 8);
    AssertEqual<int?>(8, game.LastDiceRoll, "Forced test roll should set a deterministic dice result.");
    Assert(game.HasRolledThisTurn, "Roll Dice should unlock normal turn actions.");
    ExpectRuleError(() => service.RollDice(game.RoomCode, current.PlayerId), "already");

    service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Clay);
    service.EndTurn(game.RoomCode, current.PlayerId);
    Assert(!game.HasRolledThisTurn, "Ending a turn should require the next player to roll again.");
}

static void DiceProduction()
{
    var (_, _, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var producingTile = game.Board.Tiles.First(tile => tile.NumberToken is not null && !tile.IsBlocked && tile.ResourceType != TileResourceType.None);
    SetOnlyTileToDiceNumber(game, producingTile.TileId, 8);
    producingTile = game.Board.Tiles.Single(tile => tile.TileId == producingTile.TileId);
    var adjacentVertex = game.Board.Vertices.First(vertex => vertex.AdjacentTileIds.Contains(producingTile.TileId));
    adjacentVertex.OwnerPlayerId = current.PlayerId;
    adjacentVertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    ClearSupplies(current);

    GameService.ApplyDiceRoll(game, current, producingTile.NumberToken!.Value, isDebug: false);

    var resource = ToResourceType(producingTile.ResourceType);
    AssertEqual(1, current.Supplies[resource], "A Camp adjacent to the rolled tile should produce 1 supply.");

    adjacentVertex.StructureType = BoardStructureType.Stronghold;
    current.CampsBuilt--;
    current.StrongholdsBuilt++;
    ClearSupplies(current);
    game.HasRolledThisTurn = false;
    game.LastDiceRoll = null;

    GameService.ApplyDiceRoll(game, current, producingTile.NumberToken!.Value, isDebug: false);
    AssertEqual(2, current.Supplies[resource], "A Stronghold adjacent to the rolled tile should produce 2 supplies.");
}

static void WardenStartAndProductionBlock()
{
    var (_, service, game) = CreateGame();
    var startingWardenTile = game.Board.Tiles.Single(tile => tile.TileId == game.WardenTileId);
    AssertEqual(TileResourceType.None, startingWardenTile.ResourceType, "The Warden should start on the Desert/None tile.");
    Assert(startingWardenTile.IsBlocked, "The starting Warden tile should be marked blocked.");

    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var blockedProducer = game.Board.Tiles.First(tile => tile.NumberToken is not null && tile.ResourceType != TileResourceType.None);
    service.MoveWardenForDebug(game.RoomCode, current.PlayerId, blockedProducer.TileId);
    GrantStructureAdjacentToTile(game, current, blockedProducer.TileId);
    ClearSupplies(current);

    GameService.ApplyDiceRoll(game, current, blockedProducer.NumberToken!.Value, isDebug: false);

    var resource = ToResourceType(blockedProducer.ResourceType);
    AssertEqual(0, current.Supplies[resource], "The Warden-blocked tile should not produce supplies.");
    Assert(game.Log.Any(entry => entry.Message.Contains("Warden blocked production", StringComparison.OrdinalIgnoreCase)), "Blocked production should be logged.");
}

static void WardenRollSevenFlow()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    ClearSupplies(current);
    ClearSupplies(opponent);
    current.Supplies[ResourceType.Wood] = 9;
    opponent.Supplies[ResourceType.Grain] = 7;

    service.ForceDiceRollForDebug(game.RoomCode, current.PlayerId, 7);

    Assert(game.HasRolledThisTurn, "Rolling 7 should still mark the turn as rolled.");
    AssertEqual(WardenAction.Discarding, game.PendingWardenAction, "Players over 7 supplies should trigger Warden discards.");
    AssertEqual(1, game.PendingWardenDiscards.Count, "Only players over 7 supplies should discard.");
    AssertEqual(current.PlayerId, game.PendingWardenDiscards.Single().PlayerId, "Current player should be the only discard player.");
    AssertEqual(4, game.PendingWardenDiscards.Single().RequiredAmount, "Discard amount should be floor(total / 2).");
    ExpectRuleError(() => service.MoveWarden(game.RoomCode, current.PlayerId, game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId).TileId), "discard");
    ExpectRuleError(() => service.EndTurn(game.RoomCode, current.PlayerId), "Warden");

    service.DiscardForWarden(game.RoomCode, current.PlayerId, new Dictionary<ResourceType, int> { [ResourceType.Wood] = 4 });
    AssertEqual(5, current.Supplies[ResourceType.Wood], "Discarding should remove selected supplies.");
    AssertEqual(WardenAction.MoveWarden, game.PendingWardenAction, "Warden should move after discards finish.");

    var targetTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId && tile.ResourceType != TileResourceType.None);
    GrantStructureAdjacentToTile(game, opponent, targetTile.TileId);
    opponent.Supplies[ResourceType.Clay] = 1;
    var currentSupplyCountBeforeSteal = current.Supplies.Values.Sum();

    service.MoveWarden(game.RoomCode, current.PlayerId, targetTile.TileId);
    AssertEqual(targetTile.TileId, game.WardenTileId, "MoveWarden should update the Warden tile.");
    AssertEqual(WardenAction.ChooseVictim, game.PendingWardenAction, "Adjacent supplied opponents should become victim options.");
    Assert(game.WardenVictimOptions.Contains(opponent.PlayerId), "Opponent adjacent to the Warden tile should be eligible.");

    service.StealFromWardenVictim(game.RoomCode, current.PlayerId, opponent.PlayerId);
    AssertEqual(WardenAction.None, game.PendingWardenAction, "Warden state should clear after stealing.");
    AssertEqual(currentSupplyCountBeforeSteal + 1, current.Supplies.Values.Sum(), "Stealing should transfer 1 random supply to current player.");
}

static void WardenSevenSkipsProductionAndLowSupplyDiscards()
{
    var (_, _, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var producingTile = game.Board.Tiles.First(tile => tile.NumberToken is not null && tile.ResourceType != TileResourceType.None);

    SetOnlyTileToDiceNumber(game, producingTile.TileId, 7);
    producingTile = game.Board.Tiles.Single(tile => tile.TileId == producingTile.TileId);
    GrantStructureAdjacentToTile(game, current, producingTile.TileId);
    ClearSupplies(current);
    foreach (var player in game.Players.Where(player => player.PlayerId != current.PlayerId))
    {
        ClearSupplies(player);
        player.Supplies[ResourceType.Wool] = 7;
    }

    GameService.ApplyDiceRoll(game, current, 7, isDebug: false);

    AssertEqual(0, current.Supplies.Values.Sum(), "Rolling 7 should not produce supplies from any tile.");
    AssertEqual(WardenAction.MoveWarden, game.PendingWardenAction, "Players with 7 or fewer supplies should skip discards and move directly to Warden movement.");
    AssertEqual(0, game.PendingWardenDiscards.Count, "Players with 7 or fewer supplies should not be asked to discard.");
}

static void WardenMovementAndVictimValidation()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    ClearSupplies(current);
    ClearSupplies(opponent);
    game.PendingWardenAction = WardenAction.MoveWarden;
    game.CurrentWardenPlayerId = current.PlayerId;
    ExpectRuleError(() => service.MoveWarden(game.RoomCode, current.PlayerId, game.WardenTileId), "different tile");

    var emptyVictimTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId);
    GrantStructureAdjacentToTile(game, opponent, emptyVictimTile.TileId);
    service.MoveWarden(game.RoomCode, current.PlayerId, emptyVictimTile.TileId);
    AssertEqual(WardenAction.None, game.PendingWardenAction, "Victims with no supplies should be ignored so the Warden flow can complete.");
    AssertEqual(0, game.WardenVictimOptions.Count, "A victim with no supplies should not be eligible.");

    game.PendingWardenAction = WardenAction.MoveWarden;
    game.CurrentWardenPlayerId = current.PlayerId;
    var suppliedVictimTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId);
    GrantStructureAdjacentToTile(game, opponent, suppliedVictimTile.TileId);
    opponent.Supplies[ResourceType.Clay] = 1;
    service.MoveWarden(game.RoomCode, current.PlayerId, suppliedVictimTile.TileId);
    Assert(game.WardenVictimOptions.Contains(opponent.PlayerId), "Supplied adjacent opponent should be an eligible Warden victim.");

    ClearSupplies(opponent);
    ExpectRuleError(() => service.StealFromWardenVictim(game.RoomCode, current.PlayerId, opponent.PlayerId), "no supplies");
}

static void PendingWardenActionBlocksNormalActions()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    game.PendingWardenAction = WardenAction.MoveWarden;
    game.CurrentWardenPlayerId = current.PlayerId;

    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 8;
    }

    var card = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);
    var road = AddCard(current, DevelopmentCardType.RoadBuilding, game.TurnNumber - 1);

    ExpectRuleError(() => service.EndTurn(game.RoomCode, current.PlayerId), "Warden action");
    ExpectRuleError(() => service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Clay), "Warden action");
    ExpectRuleError(() => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId), "Warden action");
    ExpectRuleError(() => service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, card.CardId, new[] { ResourceType.Wood, ResourceType.Grain }), "Warden action");
    ExpectRuleError(() => service.StartRoadBuilding(game.RoomCode, current.PlayerId, road.CardId), "Warden action");
}

static void BuyingDevelopmentCards()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    game.DevelopmentDeck.Clear();
    game.DevelopmentDeck.Add(new DevelopmentCard { CardId = "card-buy", Type = DevelopmentCardType.Knight });
    current.Supplies[ResourceType.Wool] = 1;
    current.Supplies[ResourceType.Grain] = 1;
    current.Supplies[ResourceType.Stone] = 1;

    service.BuyDevelopmentCard(game.RoomCode, current.PlayerId);

    AssertEqual(0, current.Supplies[ResourceType.Wool], "Buying should spend Wool.");
    AssertEqual(0, current.Supplies[ResourceType.Grain], "Buying should spend Grain.");
    AssertEqual(0, current.Supplies[ResourceType.Stone], "Buying should spend Stone.");
    AssertEqual(1, current.DevelopmentCards.Count, "Bought card should enter owner's hand.");
    AssertEqual(0, game.DevelopmentDeck.Count, "Deck should shrink after buying.");

    var ownerDto = game.ToDto(current.PlayerId);
    var opponentDto = game.ToDto(opponent.PlayerId);
    Assert(ownerDto.Players.Single(player => player.PlayerId == current.PlayerId).DevelopmentCards.Single().Type == "Knight", "Owner should see exact card type.");
    AssertEqual(0, opponentDto.Players.Single(player => player.PlayerId == current.PlayerId).DevelopmentCards.Count, "Opponent should not see exact card type.");
    AssertEqual(1, opponentDto.Players.Single(player => player.PlayerId == current.PlayerId).DevelopmentCardCount, "Opponent should see card count.");

    ExpectRuleError(() => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId), "The Development Card deck is empty.");

    game.DevelopmentDeck.Add(new DevelopmentCard { CardId = "card-fail", Type = DevelopmentCardType.Monopoly });
    current.Supplies[ResourceType.Wool] = 0;
    current.Supplies[ResourceType.Grain] = 0;
    current.Supplies[ResourceType.Stone] = 0;
    ExpectRuleError(() => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId), "Not enough supplies.");
}

static void DevelopmentCardsSetupAndRollGate()
{
    var (_, service, game) = CreateGame();
    var setupPlayer = game.CurrentPlayer;
    var setupCard = AddCard(setupPlayer, DevelopmentCardType.Monopoly, game.TurnNumber - 1);

    ExpectRuleError(
        () => service.BuyDevelopmentCard(game.RoomCode, setupPlayer.PlayerId),
        "You cannot buy Development Cards during setup.");
    ExpectRuleError(
        () => service.PlayMonopoly(game.RoomCode, setupPlayer.PlayerId, setupCard.CardId, ResourceType.Wood),
        "You cannot play Development Cards during setup.");

    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 3;
    }

    var oldCard = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);

    ExpectRuleError(
        () => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId),
        "You must roll before buying a Development Card.");
    ExpectRuleError(
        () => service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, oldCard.CardId, new[] { ResourceType.Wood, ResourceType.Grain }),
        "You must roll before playing a Development Card.");
}

static void MaritimeTradingRates()
{
    var (_, setupService, setupGame) = CreateGame();
    var setupPlayer = setupGame.CurrentPlayer;
    ExpectRuleError(
        () => setupService.MaritimeTrade(setupGame.RoomCode, setupPlayer.PlayerId, ResourceType.Wood, ResourceType.Clay),
        "Trading is not available during setup.");

    var (_, service, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    ExpectRuleError(
        () => service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Clay),
        "You must roll before trading.");

    MarkRolled(game);
    ExpectRuleError(
        () => service.MaritimeTrade(game.RoomCode, opponent.PlayerId, ResourceType.Wood, ResourceType.Clay),
        "It is not your turn.");

    ClearSupplies(current);
    current.Supplies[ResourceType.Wood] = 4;
    AssertEqual(4, GameService.GetBestTradeRate(game, current.PlayerId, ResourceType.Wood), "Default bank trade should be 4:1.");
    service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Clay);
    AssertEqual(0, current.Supplies[ResourceType.Wood], "Default trade should spend 4 offered resources.");
    AssertEqual(1, current.Supplies[ResourceType.Clay], "Default trade should grant 1 requested resource.");

    ExpectRuleError(
        () => service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Clay, ResourceType.Clay),
        "You cannot trade a resource for itself.");
    ExpectRuleError(
        () => service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Stone, ResourceType.Wood),
        "You do not have enough supplies for this trade.");

    var genericPort = game.Board.Ports.First(port => port.Type == PortType.Generic3To1);
    GrantPortAccess(game, current, genericPort);
    Assert(GameService.PlayerHasGenericPort(game, current.PlayerId), "Player should gain generic port access from an adjacent Camp.");
    Assert(
        game.ToDto(current.PlayerId).Players.Single(player => player.PlayerId == current.PlayerId).AccessibleHarborSlotIds.Any(),
        "DTO should expose accessible harbor slot IDs.");

    ClearSupplies(current);
    current.Supplies[ResourceType.Stone] = 3;
    AssertEqual(3, GameService.GetBestTradeRate(game, current.PlayerId, ResourceType.Stone), "Generic port trade should be 3:1.");
    service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Stone, ResourceType.Grain);
    AssertEqual(0, current.Supplies[ResourceType.Stone], "Generic trade should spend 3 offered resources.");
    AssertEqual(1, current.Supplies[ResourceType.Grain], "Generic trade should grant 1 requested resource.");

    var woodPort = game.Board.Ports.First(port => port.Type == PortType.Specific2To1 && port.ResourceType == ResourceType.Wood);
    GrantPortAccess(game, current, woodPort, BoardStructureType.Stronghold);
    ClearSupplies(current);
    current.Supplies[ResourceType.Wood] = 2;
    current.Supplies[ResourceType.Clay] = 3;

    Assert(GameService.PlayerHasSpecificPort(game, current.PlayerId, ResourceType.Wood), "Player should gain Wood port access from an adjacent Stronghold.");
    AssertEqual(2, GameService.GetBestTradeRate(game, current.PlayerId, ResourceType.Wood), "Specific Wood port should beat generic 3:1.");
    AssertEqual(3, GameService.GetBestTradeRate(game, current.PlayerId, ResourceType.Clay), "Specific Wood port should not make Clay trade at 2:1.");
    service.MaritimeTrade(game.RoomCode, current.PlayerId, ResourceType.Wood, ResourceType.Stone);
    AssertEqual(0, current.Supplies[ResourceType.Wood], "Specific trade should spend 2 offered resources.");
    AssertEqual(1, current.Supplies[ResourceType.Stone], "Specific trade should grant 1 requested resource.");

    var (_, _, isolatedGame) = CreateGame();
    var isolatedPlayer = isolatedGame.CurrentPlayer;
    var targetPort = isolatedGame.Board.Ports.First(port => port.Type == PortType.Specific2To1 && port.ResourceType == ResourceType.Clay);
    var nonAdjacentVertex = isolatedGame.Board.Vertices.First(vertex =>
        vertex.IsCoastal && !targetPort.AdjacentVertexIds.Contains(vertex.VertexId));
    nonAdjacentVertex.OwnerPlayerId = isolatedPlayer.PlayerId;
    nonAdjacentVertex.StructureType = BoardStructureType.Camp;
    Assert(
        GameService.GetPlayerPorts(isolatedGame, isolatedPlayer.PlayerId).All(port => port.Id != targetPort.Id),
        "A non-adjacent Camp should not grant access to the target port.");

    var (_, _, trailOnlyGame) = CreateGame();
    var trailOnlyPlayer = trailOnlyGame.CurrentPlayer;
    var trailOnlySlot = trailOnlyGame.Board.HarborSlots.First(slot => slot.HarborType == HarborType.Generic);
    var harborEdge = trailOnlyGame.Board.Edges.Single(edge => edge.EdgeId == trailOnlySlot.AdjacentEdgeId);
    harborEdge.OwnerPlayerId = trailOnlyPlayer.PlayerId;
    trailOnlyPlayer.TrailsBuilt++;
    Assert(
        GameService.GetPlayerHarborSlots(trailOnlyGame, trailOnlyPlayer.PlayerId).All(slot => slot.HarborSlotId != trailOnlySlot.HarborSlotId),
        "Owning only a Trail on a harbor edge should not grant harbor access.");
    AssertEqual(4, GameService.GetBestTradeRate(trailOnlyGame, trailOnlyPlayer.PlayerId, ResourceType.Stone), "Trail-only harbor contact should keep default 4:1.");
}

static void DevelopmentCardPlayRestrictions()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    var boughtThisTurn = AddCard(current, DevelopmentCardType.Monopoly, game.TurnNumber);
    ExpectRuleError(
        () => service.PlayMonopoly(game.RoomCode, current.PlayerId, boughtThisTurn.CardId, ResourceType.Wood),
        "You cannot play a Development Card bought this turn.");

    var playable = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);
    service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, playable.CardId, new[] { ResourceType.Wood, ResourceType.Wood });

    var secondCard = AddCard(current, DevelopmentCardType.Monopoly, game.TurnNumber - 1);
    ExpectRuleError(
        () => service.PlayMonopoly(game.RoomCode, current.PlayerId, secondCard.CardId, ResourceType.Wood),
        "You already played a Development Card this turn.");

    var opponentCard = AddCard(opponent, DevelopmentCardType.Monopoly, game.TurnNumber - 1);
    ExpectRuleError(() => service.PlayMonopoly(game.RoomCode, current.PlayerId, opponentCard.CardId, ResourceType.Wood), "own");

    service.EndTurn(game.RoomCode, current.PlayerId);
    Assert(!current.HasPlayedDevelopmentCardThisTurn, "Ending a turn should reset the ending player's development-card play flag.");
    Assert(!game.CurrentPlayer.HasPlayedDevelopmentCardThisTurn, "Ending a turn should reset the next player's development-card play flag.");
    ExpectRuleError(() => service.PlayMonopoly(game.RoomCode, current.PlayerId, secondCard.CardId, ResourceType.Wood), "not your turn");
}

static void YearOfPlentyAndMonopoly()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);

    current.Supplies[ResourceType.Stone] = 0;
    var plenty = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);
    service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, plenty.CardId, new[] { ResourceType.Stone, ResourceType.Stone });
    AssertEqual(2, current.Supplies[ResourceType.Stone], "Year of Plenty should allow two of the same resource.");

    service.EndTurn(game.RoomCode, current.PlayerId);
    MarkRolled(game);
    service.EndTurn(game.RoomCode, opponent.PlayerId);
    MarkRolled(game);

    current.Supplies[ResourceType.Clay] = 0;
    opponent.Supplies[ResourceType.Clay] = 4;
    var monopoly = AddCard(current, DevelopmentCardType.Monopoly, game.TurnNumber - 1);
    service.PlayMonopoly(game.RoomCode, current.PlayerId, monopoly.CardId, ResourceType.Clay);
    AssertEqual(4, current.Supplies[ResourceType.Clay], "Monopoly should transfer selected resource.");
    AssertEqual(0, opponent.Supplies[ResourceType.Clay], "Opponent should lose selected resource.");
}

static void KnightAndLargestArmy()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    var firstWardenTile = game.WardenTileId;
    var currentSupplyCountBefore = current.Supplies.Values.Sum();

    for (var index = 0; index < 3; index++)
    {
        var targetTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId && tile.ResourceType != TileResourceType.None);
        GrantStructureAdjacentToTile(game, opponent, targetTile.TileId);
        opponent.Supplies[ResourceType.Wood] = 1;
        var knight = AddCard(current, DevelopmentCardType.Knight, game.TurnNumber - 1);
        service.PlayKnight(game.RoomCode, current.PlayerId, knight.CardId, "", null);
        AssertEqual(WardenAction.MoveWarden, game.PendingWardenAction, "Knight should start the Warden move step.");
        service.MoveWarden(game.RoomCode, current.PlayerId, targetTile.TileId);
        service.StealFromWardenVictim(game.RoomCode, current.PlayerId, opponent.PlayerId);

        if (index < 2)
        {
            service.EndTurn(game.RoomCode, current.PlayerId);
            MarkRolled(game);
            service.EndTurn(game.RoomCode, opponent.PlayerId);
            MarkRolled(game);
        }
    }

    Assert(game.WardenTileId != firstWardenTile, "Knight should move the Warden.");
    AssertEqual(3, current.PlayedKnightCount, "Knight should increment played count.");
    AssertEqual(current.PlayerId, game.LargestArmyPlayerId, "Strongest Guard should be awarded after 3 Knights.");
    Assert(current.Supplies.Values.Sum() > currentSupplyCountBefore, "Knight should steal one random available resource from chosen victim.");
}

static void KnightStartsWardenFlow()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var knight = AddCard(current, DevelopmentCardType.Knight, game.TurnNumber - 1);

    service.PlayKnight(game.RoomCode, current.PlayerId, knight.CardId, "", null);

    Assert(knight.IsPlayed, "Knight card should be marked played immediately.");
    AssertEqual(1, current.PlayedKnightCount, "Knight should increment played count.");
    AssertEqual(WardenAction.MoveWarden, game.PendingWardenAction, "Knight should start Warden movement without discards.");
    AssertEqual(0, game.PendingWardenDiscards.Count, "Knight should not trigger Warden discards.");

    var targetTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId);
    service.MoveWarden(game.RoomCode, current.PlayerId, targetTile.TileId);
    AssertEqual(WardenAction.None, game.PendingWardenAction, "Warden flow should complete cleanly when no victim is available.");
}

static void RoadBuildingEffect()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var road = AddCard(current, DevelopmentCardType.RoadBuilding, game.TurnNumber - 1);

    service.StartRoadBuilding(game.RoomCode, current.PlayerId, road.CardId);
    Assert(current.ActiveDevelopmentCardEffect?.Type == ActiveDevelopmentCardType.RoadBuilding, "Road Building should create an active effect.");
    ExpectRuleError(() => service.PlaceFreeTrail(game.RoomCode, current.PlayerId, "edge-1"), "trail board layer");
}

static void VictoryPointWin()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;

    game.DevelopmentDeck.Clear();
    game.DevelopmentDeck.Add(new DevelopmentCard { CardId = "vp-win", Type = DevelopmentCardType.VictoryPoint });
    current.CampsBuilt = 9;
    current.Supplies[ResourceType.Wool] = 1;
    current.Supplies[ResourceType.Grain] = 1;
    current.Supplies[ResourceType.Stone] = 1;

    service.BuyDevelopmentCard(game.RoomCode, current.PlayerId);
    AssertEqual(GameStatus.Finished, game.Status, "Buying a VP card at 10 points should finish the match.");
    AssertEqual(current.PlayerId, game.WinnerPlayerId, "Current player should be winner.");
}

static void DebugActionsRejectOutsideDevelopment()
{
    var (lobby, service, game) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var host = room.Players.Single(player => player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Production"));

    ExpectRuleError(
        () => debug.AddResource(room, host, game.CurrentPlayer.PlayerId, ResourceType.Wood, 1),
        "Development");
}

static void DebugResourceActions()
{
    var (lobby, service, game) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var host = room.Players.Single(player => player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Development"));
    var player = game.CurrentPlayer;

    ClearSupplies(player);
    debug.AddResource(room, host, player.PlayerId, ResourceType.Wood, 5);
    AssertEqual(5, player.Supplies[ResourceType.Wood], "Debug add resource should update supplies.");

    debug.SetTestingResources(room, host, player.PlayerId);
    Assert(ResourceTypes.All.All(resource => player.Supplies[resource] >= 8), "Testing resources should fill every resource.");

    debug.ClearResources(room, host, player.PlayerId);
    Assert(ResourceTypes.All.All(resource => player.Supplies[resource] == 0), "Debug clear resources should clear every resource.");
    Assert(game.Log.Any(entry => entry.Message.StartsWith("[DEBUG]", StringComparison.Ordinal)), "Debug actions should write debug log entries.");
}

static void DebugForceDice()
{
    var (lobby, service, game) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var host = room.Players.Single(player => player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Development"));

    debug.ForceDiceRoll(room, host, 7);
    AssertEqual(7, game.LastDiceRoll, "Debug force dice should set the dice value.");
    Assert(game.HasRolledThisTurn, "Debug force dice should mark the turn as rolled.");
    AssertEqual(WardenAction.MoveWarden, game.PendingWardenAction, "Debug force dice 7 should trigger the Warden flow.");

    var targetTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId);
    debug.MoveWarden(room, host, targetTile.TileId);
    AssertEqual(targetTile.TileId, game.WardenTileId, "Debug move Warden should update the blocked tile.");

    debug.ClearWardenState(room, host);
    AssertEqual(WardenAction.None, game.PendingWardenAction, "Debug clear should remove pending Warden state.");

    debug.ResetRollState(room, host);
    AssertEqual<int?>(null, game.LastDiceRoll, "Debug reset roll should clear dice value.");
    Assert(!game.HasRolledThisTurn, "Debug reset roll should allow rolling again.");
}

static void DebugDevelopmentCardActions()
{
    var (lobby, service, game) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var host = room.Players.Single(player => player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Development"));
    var player = game.CurrentPlayer;

    debug.GiveDevelopmentCard(room, host, player.PlayerId, DevelopmentCardType.VictoryPoint);
    AssertEqual(1, player.DevelopmentCards.Count, "Debug should give the selected player a card.");
    AssertEqual(DevelopmentCardType.VictoryPoint, player.DevelopmentCards.Single().Type, "Debug should give the selected card type.");

    player.HasPlayedDevelopmentCardThisTurn = true;
    debug.ResetDevelopmentCardPlayLimit(room, host, player.PlayerId);
    Assert(!player.HasPlayedDevelopmentCardThisTurn, "Debug reset should clear the development-card play limit.");

    var composition = debug.GetDevelopmentDeckComposition(room, host);
    AssertEqual(game.DevelopmentDeck.Count, composition.Values.Sum(), "Debug deck composition should add up to remaining deck size.");
    AssertEqual(
        Enum.GetNames<DevelopmentCardType>().Length,
        composition.Count,
        "Debug deck composition should include every development-card type.");

    debug.ClearDevelopmentCards(room, host, player.PlayerId);
    AssertEqual(0, player.DevelopmentCards.Count, "Debug clear should remove the selected player's cards.");
}

static void DebugAuthorization()
{
    var (lobby, service, game) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var nonHost = room.Players.Single(player => !player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Development"));
    var outsider = new Player
    {
        PlayerId = "outside",
        PlayerName = "Outside",
        ConnectionId = "outside-connection"
    };

    ExpectRuleError(
        () => debug.AddResource(room, nonHost, game.CurrentPlayer.PlayerId, ResourceType.Wood, 1),
        "host");
    ExpectRuleError(
        () => debug.AddResource(room, outsider, game.CurrentPlayer.PlayerId, ResourceType.Wood, 1),
        "not in");
}

static (LobbyService Lobby, GameService Service, GameState Game) CreateGame()
{
    var lobby = new LobbyService();
    var boardGenerator = new BoardGenerator();
    var service = new GameService(boardGenerator);
    var room = lobby.CreateRoom("connection-1", "Ari");
    room = lobby.JoinRoom("connection-2", room.RoomCode, "Bea");
    room = lobby.StartGame("connection-1", room.RoomCode);
    var game = service.StartGame(room);
    return (lobby, service, game);
}

static (LobbyService Lobby, GameService Service, GameState Game, PlayerGameState Player) PrepareSecondSetupCampTest()
{
    var (lobby, service, game) = CreateGame();
    ClearBoardOwnership(game);

    game.Phase = GamePhase.Setup;
    game.SetupRound = SetupRound.SecondPlacement;
    game.SetupStep = SetupStep.PlaceCamp;
    game.SetupDirection = SetupDirection.Reverse;
    game.SetupPlayerIndex = 0;
    game.CurrentPlayerIndex = 0;
    game.LastSetupCampVertexId = null;

    var player = game.Players.Single(candidate => candidate.PlayerId == game.CurrentSetupPlayerId);
    ClearSupplies(player);
    return (lobby, service, game, player);
}

static PlayerDevelopmentCard AddCard(PlayerGameState player, DevelopmentCardType type, int purchasedTurn)
{
    var card = new PlayerDevelopmentCard
    {
        CardId = Guid.NewGuid().ToString("N"),
        Type = type,
        PurchasedTurn = purchasedTurn
    };
    player.DevelopmentCards.Add(card);
    return card;
}

static void ClearBoardOwnership(GameState game)
{
    foreach (var vertex in game.Board.Vertices)
    {
        vertex.OwnerPlayerId = null;
        vertex.StructureType = null;
    }

    foreach (var edge in game.Board.Edges)
    {
        edge.OwnerPlayerId = null;
    }

    foreach (var player in game.Players)
    {
        player.CampsBuilt = 0;
        player.StrongholdsBuilt = 0;
        player.TrailsBuilt = 0;
    }
}

static void SetAdjacentTileResources(GameState game, BoardVertex vertex, params TileResourceType[] resources)
{
    Assert(
        vertex.AdjacentTileIds.Count >= resources.Length,
        $"Vertex {vertex.VertexId} should have at least {resources.Length} adjacent tiles for this test.");

    for (var index = 0; index < resources.Length; index++)
    {
        ReplaceTileResource(game, vertex.AdjacentTileIds[index], resources[index], index);
    }
}

static void ReplaceTileResource(GameState game, string tileId, TileResourceType resourceType, int index)
{
    ReplaceTile(game, tileId, resourceType, resourceType == TileResourceType.None ? null : index switch
    {
        0 => 5,
        1 => 6,
        _ => 8
    });
}

static void SetOnlyTileToDiceNumber(GameState game, string tileId, int diceNumber)
{
    foreach (var tile in game.Board.Tiles.ToList())
    {
        ReplaceTile(
            game,
            tile.TileId,
            tile.ResourceType,
            tile.ResourceType == TileResourceType.None ? null : tile.TileId == tileId ? diceNumber : 3,
            tile.IsBlocked);
    }
}

static void ReplaceTile(GameState game, string tileId, TileResourceType resourceType, int? numberToken, bool isBlocked = false)
{
    var tileIndex = game.Board.Tiles.FindIndex(tile => tile.TileId == tileId);
    Assert(tileIndex >= 0, $"Expected board tile {tileId} to exist.");

    var tile = game.Board.Tiles[tileIndex];
    game.Board.Tiles[tileIndex] = new HexTile
    {
        TileId = tile.TileId,
        Q = tile.Q,
        R = tile.R,
        ResourceType = resourceType,
        NumberToken = numberToken,
        IsBlocked = isBlocked
    };
}

static void GrantPortAccess(GameState game, PlayerGameState player, Port port, BoardStructureType structureType = BoardStructureType.Camp)
{
    var vertex = game.Board.Vertices.Single(candidate => candidate.VertexId == port.AdjacentVertexIds[0]);
    vertex.OwnerPlayerId = player.PlayerId;
    vertex.StructureType = structureType;

    if (structureType == BoardStructureType.Stronghold)
    {
        player.StrongholdsBuilt++;
    }
    else
    {
        player.CampsBuilt++;
    }
}

static BoardVertex GrantStructureAdjacentToTile(
    GameState game,
    PlayerGameState player,
    string tileId,
    BoardStructureType structureType = BoardStructureType.Camp)
{
    var vertex = game.Board.Vertices
        .Where(candidate => candidate.AdjacentTileIds.Contains(tileId))
        .OrderBy(candidate => candidate.OwnerPlayerId == player.PlayerId ? 0 : 1)
        .ThenBy(candidate => candidate.VertexId)
        .First();

    var alreadyOwnedByPlayer = vertex.OwnerPlayerId == player.PlayerId && vertex.StructureType == structureType;
    vertex.OwnerPlayerId = player.PlayerId;
    vertex.StructureType = structureType;

    if (!alreadyOwnedByPlayer)
    {
        if (structureType == BoardStructureType.Stronghold)
        {
            player.StrongholdsBuilt++;
        }
        else
        {
            player.CampsBuilt++;
        }
    }

    return vertex;
}

static void ClearSupplies(PlayerGameState player)
{
    foreach (var resource in ResourceTypes.All)
    {
        player.Supplies[resource] = 0;
    }
}

static void EnterNormalTurn(GameState game, bool rolled = true)
{
    game.Phase = GamePhase.NormalTurn;
    game.SetupRound = null;
    game.SetupStep = null;
    game.SetupDirection = null;
    game.SetupPlayerIndex = 0;
    game.LastSetupCampVertexId = null;
    game.CurrentPlayerIndex = 0;
    game.TurnNumber = Math.Max(1, game.TurnNumber);
    game.HasRolledThisTurn = rolled;
    game.LastDiceRoll = rolled ? 8 : null;
}

static void MarkRolled(GameState game)
{
    game.HasRolledThisTurn = true;
    game.LastDiceRoll ??= 8;
}

static BoardVertex FindValidSetupVertex(GameState game, bool preferProduction = false)
{
    var candidates = game.Board.Vertices
        .Where(vertex => vertex.OwnerPlayerId is null && vertex.StructureType is null)
        .Where(vertex => !game.Board.Edges
            .Where(edge => EdgeTouches(edge, vertex.VertexId))
            .Select(edge => edge.StartVertexId == vertex.VertexId ? edge.EndVertexId : edge.StartVertexId)
            .Select(otherVertexId => game.Board.Vertices.First(otherVertex => otherVertex.VertexId == otherVertexId))
            .Any(otherVertex => otherVertex.OwnerPlayerId is not null && otherVertex.StructureType is not null));

    if (preferProduction)
    {
        candidates = candidates
            .OrderByDescending(vertex => vertex.AdjacentTileIds.Count(tileId =>
            {
                var tile = game.Board.Tiles.First(candidate => candidate.TileId == tileId);
                return tile.NumberToken is not null && !tile.IsBlocked && tile.ResourceType != TileResourceType.None;
            }))
            .ThenBy(vertex => vertex.VertexId);
    }
    else
    {
        candidates = candidates.OrderBy(vertex => vertex.VertexId);
    }

    return candidates.First();
}

static BoardVertex FindValidSetupVertexWithAdjacentTileCount(GameState game, int adjacentTileCount)
{
    return game.Board.Vertices
        .Where(vertex => vertex.AdjacentTileIds.Count >= adjacentTileCount)
        .Where(vertex => vertex.OwnerPlayerId is null && vertex.StructureType is null)
        .Where(vertex => !game.Board.Edges
            .Where(edge => EdgeTouches(edge, vertex.VertexId))
            .Select(edge => edge.StartVertexId == vertex.VertexId ? edge.EndVertexId : edge.StartVertexId)
            .Select(otherVertexId => game.Board.Vertices.First(otherVertex => otherVertex.VertexId == otherVertexId))
            .Any(otherVertex => otherVertex.OwnerPlayerId is not null && otherVertex.StructureType is not null))
        .OrderBy(vertex => vertex.VertexId)
        .First();
}

static BoardEdge FindSetupTrail(GameState game, string vertexId)
{
    return game.Board.Edges
        .Where(edge => edge.OwnerPlayerId is null && EdgeTouches(edge, vertexId))
        .OrderBy(edge => edge.EdgeId)
        .First();
}

static bool EdgeTouches(BoardEdge edge, string vertexId)
{
    return string.Equals(edge.StartVertexId, vertexId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(edge.EndVertexId, vertexId, StringComparison.OrdinalIgnoreCase);
}

static ResourceType ToResourceType(TileResourceType resourceType)
{
    return resourceType switch
    {
        TileResourceType.Wood => ResourceType.Wood,
        TileResourceType.Clay => ResourceType.Clay,
        TileResourceType.Wool => ResourceType.Wool,
        TileResourceType.Grain => ResourceType.Grain,
        TileResourceType.Stone => ResourceType.Stone,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
    };
}

static HarborType ToHarborType(ResourceType resource)
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

static void ExpectRuleError(Action action, string expectedMessagePart)
{
    try
    {
        action();
    }
    catch (GameRuleException ex) when (ex.Message.Contains(expectedMessagePart, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    throw new InvalidOperationException($"Expected rule error containing '{expectedMessagePart}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{message} Expected {string.Join(", ", expected)}, got {string.Join(", ", actual)}.");
    }
}

static void AssertCost(IReadOnlyDictionary<ResourceType, int> actual, params (ResourceType Resource, int Amount)[] expected)
{
    var expectedDictionary = expected.ToDictionary(item => item.Resource, item => item.Amount);
    AssertEqual(expectedDictionary.Count, actual.Count, "Cost resource count mismatch.");

    foreach (var (resource, amount) in expectedDictionary)
    {
        Assert(actual.TryGetValue(resource, out var actualAmount), $"Missing cost resource {resource}.");
        AssertEqual(amount, actualAmount, $"Cost mismatch for {resource}.");
    }
}

sealed class TestHostEnvironment : IHostEnvironment
{
    public TestHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
        ContentRootPath = Directory.GetCurrentDirectory();
        ContentRootFileProvider = new NullFileProvider();
    }

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "Karo.Tests";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
