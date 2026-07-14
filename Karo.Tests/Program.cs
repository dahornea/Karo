using System.Text.Json;
using System.Text.Json.Serialization;
using Karo.Api.Hubs;
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
    ("Board generation is deterministic for a supplied seed", BoardSeedDeterminism),
    ("Board topology exposes complete tile, node, and edge adjacency", BoardTopologyIntegrity),
    ("Board resource values serialize as stable strings", BoardResourceSerialization),
    ("Board generator fuzzes 500 deterministic seeds", BoardGenerationFuzz),
    ("Lobby sessions resume without duplicate players", LobbySessionResume),
    ("Active browser session recovery restores missing local identity", ActiveBrowserSessionRecovery),
    ("Lobby readiness and player count gate game start", LobbyReadinessAndPlayerCount),
    ("Required-player reconnect pauses then resumes the match", RequiredPlayerReconnectPause),
    ("Non-required disconnect does not pause the match", NonRequiredDisconnectDoesNotPause),
    ("Host migration waits for the reconnect grace period", HostMigrationAfterReconnectGrace),
    ("Forfeit preserves board pieces and removes player from active flow", ForfeitLifecycle),
    ("Post-game return restores the lobby for a fresh rematch", PostGameReturnAndRematch),
    ("Setup phase places Camps and Trails in forward then reverse order", SetupPhasePlacementFlow),
    ("Player construction-piece supply starts finite and setup consumes pieces", SetupPieceSupplyConsumption),
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
    ("Development card setup and purchase roll gates", DevelopmentCardsSetupAndRollGate),
    ("Development action cards can be played before rolling", DevelopmentCardsPreRollTiming),
    ("Buying development cards validates cost, deck, ownership, and privacy", BuyingDevelopmentCards),
    ("Maritime trading uses default, generic, and specific harbor rates", MaritimeTradingRates),
    ("Player trade offer creation validation", PlayerTradeOfferCreationValidation),
    ("Player trade offer acceptance, rejection, cancellation, and transfer", PlayerTradeOfferResolution),
    ("Failed player trade acceptance is atomic and disables stale Accept", FailedPlayerTradeAcceptanceIsAtomic),
    ("Player trade offers expire on turn and Warden transitions", PlayerTradeOfferExpiry),
    ("Development card play restrictions", DevelopmentCardPlayRestrictions),
    ("Friendly validation errors stay player-facing", FriendlyValidationErrors),
    ("Year of Plenty and Monopoly effects", YearOfPlentyAndMonopoly),
    ("Knight, Warden movement, random steal, and Largest Army", KnightWardenAndLargestArmy),
    ("Knight starts Warden flow without discard", KnightStartsWardenFlow),
    ("Largest Army threshold, ties, transfer, scoring, and privacy", LargestArmyScoringRules),
    ("Largest Army can trigger the win flow", LargestArmyWinDetection),
    ("Longest Trail award, transfer, interruption, and scoring", LongestTrailScoringRules),
    ("Longest Trail graph traversal rules", LongestTrailGraphRules),
    ("Road Building free Trail recalculates Longest Trail", RoadBuildingFreeTrailRecalculatesLongestTrail),
    ("Trail placement connects through owned Camps and Strongholds", TrailPlacementConnectsThroughOwnedStructures),
    ("Trail placement allows empty connected nodes and rejects disconnected edges", TrailPlacementConnectivityValidation),
    ("Trail placement is blocked by opponent structures", TrailPlacementBlocksOpponentStructures),
    ("Setup Trail still connects to the just-placed Camp", SetupTrailConnectsToJustPlacedCamp),
    ("Road Building uses normal Trail connectivity rules", RoadBuildingUsesTrailConnectivityRules),
    ("Normal building consumes finite construction pieces", NormalBuildingConsumesFinitePieces),
    ("Piece exhaustion blocks builds before spending supplies", PieceExhaustionBlocksBuildsAtomically),
    ("Road Building free Trails consume finite Trail pieces", RoadBuildingConsumesFiniteTrailPieces),
    ("Longest Trail can trigger the win flow", LongestTrailWinDetection),
    ("Road Building active effect limitation is explicit", RoadBuildingEffect),
    ("Road Building prevalidation does not consume an unusable card", RoadBuildingPrevalidationIsAtomic),
    ("Victory Point card can trigger a 10 VP win", VictoryPointWin),
    ("Debug actions are rejected outside Development", DebugActionsRejectOutsideDevelopment),
    ("Debug resource actions update player resources in Development", DebugResourceActions),
    ("Debug force dice updates roll state in Development", DebugForceDice),
    ("Debug development-card actions update cards and deck readout in Development", DebugDevelopmentCardActions),
    ("Debug board inspector validates and regenerates seeded boards", DebugBoardInspector),
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
    AssertEqual(board.BoardSeed, currentDto.Board.BoardSeed, "The backend board seed should be visible in the shared board DTO.");
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

static void BoardSeedDeterminism()
{
    const int seed = 248_319;
    var generator = new BoardGenerator();
    var first = generator.Generate(seed);
    var second = generator.Generate(seed);
    var validator = new BoardIntegrityValidator();

    Assert(validator.Validate(first).IsValid, "The first seeded board should pass integrity validation.");
    Assert(validator.Validate(second).IsValid, "The repeated seeded board should pass integrity validation.");
    AssertEqual(seed, first.BoardSeed, "The generated board should retain its requested seed.");
    AssertSequence(BoardSnapshot(first), BoardSnapshot(second), "The same board seed must reproduce terrain, tokens, topology, and harbors.");
}

static void BoardTopologyIntegrity()
{
    var board = new BoardGenerator().Generate(91_721);
    var validator = new BoardIntegrityValidator();
    Assert(validator.Validate(board).IsValid, "A generated board should pass full topology validation.");

    var center = board.Tiles.Single(tile => tile.Q == 0 && tile.R == 0);
    AssertEqual(6, center.AdjacentTileIds.Count, "The center tile should have six axial neighbors.");
    Assert(board.Tiles.Any(tile => tile.AdjacentTileIds.Count < 6), "Boundary tiles should have fewer than six neighbors.");
    AssertEqual(54, board.Vertices.Count, "A radius-2 Karo board should deduplicate to 54 physical nodes.");
    AssertEqual(72, board.Edges.Count, "A radius-2 Karo board should deduplicate to 72 physical edges.");
    Assert(board.Vertices.Any(vertex => vertex.AdjacentTileIds.Count == 3), "The board should contain interior three-tile nodes.");
    Assert(board.Vertices.Any(vertex => vertex.IsCoastal && vertex.AdjacentTileIds.Count == 2), "The board should contain coastal two-tile nodes.");
    Assert(board.Vertices.Any(vertex => vertex.IsCoastal && vertex.AdjacentTileIds.Count == 1), "The board should contain extreme coastal one-tile nodes.");
    Assert(board.Edges.Any(edge => edge.AdjacentTileIds.Count == 2), "The board should contain interior shared edges.");
    Assert(board.Edges.Any(edge => edge.AdjacentTileIds.Count == 1), "The board should contain coastal edges.");

    foreach (var tile in board.Tiles)
    {
        Assert(tile.AdjacentTileIds.All(id => board.Tiles.Single(candidate => candidate.TileId == id).AdjacentTileIds.Contains(tile.TileId)), "Tile adjacency must be symmetric.");
    }

    foreach (var vertex in board.Vertices)
    {
        Assert(vertex.AdjacentVertexIds.Count is 2 or 3, "Each generated node should have degree two or three.");
        Assert(vertex.AdjacentVertexIds.All(id => board.Vertices.Single(candidate => candidate.VertexId == id).AdjacentVertexIds.Contains(vertex.VertexId)), "Node adjacency must be symmetric.");
        Assert(vertex.AdjacentEdgeIds.All(id => board.Edges.Single(candidate => candidate.EdgeId == id).StartVertexId == vertex.VertexId
            || board.Edges.Single(candidate => candidate.EdgeId == id).EndVertexId == vertex.VertexId), "Node-to-edge links must be symmetric.");
    }

    var corrupted = new BoardGenerator().Generate(91_721);
    corrupted.Tiles[0].AdjacentTileIds.Clear();
    var corruptedResult = validator.Validate(corrupted);
    Assert(!corruptedResult.IsValid && corruptedResult.Errors.Any(error => error.Contains("Tile adjacency", StringComparison.OrdinalIgnoreCase)), "The validator should reject corrupt tile adjacency.");

    var duplicateIdBoard = new BoardGenerator().Generate(91_721);
    var copiedTile = duplicateIdBoard.Tiles[0];
    duplicateIdBoard.Tiles.Add(new HexTile
    {
        TileId = copiedTile.TileId,
        Q = copiedTile.Q,
        R = copiedTile.R,
        ResourceType = copiedTile.ResourceType,
        NumberToken = copiedTile.NumberToken
    });
    var duplicateIdResult = validator.Validate(duplicateIdBoard);
    Assert(!duplicateIdResult.IsValid && duplicateIdResult.Errors.Any(error => error.Contains("Tile IDs", StringComparison.OrdinalIgnoreCase)), "The validator should report duplicate tile IDs without throwing.");
}

static void BoardResourceSerialization()
{
    var options = new JsonSerializerOptions();
    options.Converters.Add(new JsonStringEnumConverter());

    foreach (var resource in ResourceTypes.All)
    {
        var serialized = JsonSerializer.Serialize(resource, options);
        var roundTrip = JsonSerializer.Deserialize<ResourceType>(serialized, options);
        AssertEqual(resource, roundTrip, $"{resource} should round-trip through string enum serialization.");
        Assert(serialized.Contains(resource.ToString(), StringComparison.Ordinal), $"{resource} should serialize by name.");
    }

    var (_, _, game) = CreateGame();
    var dto = game.ToDto(game.CurrentPlayer.PlayerId).Board;
    Assert(dto.Tiles.Any(tile => tile.ResourceType == "None" && tile.NumberToken is null), "The DTO should expose Desert as the non-producing tile resource.");
    Assert(dto.Tiles.Where(tile => tile.ResourceType != "None").All(tile => ResourceTypes.All.Any(resource => resource.ToString() == tile.ResourceType)), "Productive tile DTO resources should use only final Karo resource names.");
}

static void BoardGenerationFuzz()
{
    var generator = new BoardGenerator();
    var validator = new BoardIntegrityValidator();

    for (var seed = 0; seed < 500; seed++)
    {
        var board = generator.Generate(seed);
        var desert = board.Tiles.Single(tile => tile.ResourceType == TileResourceType.None);
        desert.IsBlocked = true;
        var result = validator.Validate(board, desert.TileId, requireWardenOnDesert: true);
        Assert(result.IsValid, $"Board seed {seed} failed integrity validation: {string.Join(" | ", result.Errors)}");
        AssertEqual(19, board.Tiles.Count, $"Board seed {seed} should have 19 tiles.");
        AssertEqual(9, board.HarborSlots.Count, $"Board seed {seed} should have 9 harbors.");
        AssertEqual(desert.TileId, board.Tiles.Single(tile => tile.IsBlocked).TileId, $"Board seed {seed} should start the Warden on Desert.");
    }
}

static IReadOnlyList<string> BoardSnapshot(BoardState board)
{
    return board.Tiles
        .OrderBy(tile => tile.TileId)
        .Select(tile => $"tile:{tile.TileId}:{tile.Q}:{tile.R}:{tile.ResourceType}:{tile.NumberToken}:{string.Join(',', tile.AdjacentTileIds.OrderBy(id => id))}")
        .Concat(board.Vertices
            .OrderBy(vertex => vertex.VertexId)
            .Select(vertex => $"node:{vertex.VertexId}:{vertex.X}:{vertex.Y}:{string.Join(',', vertex.AdjacentTileIds.OrderBy(id => id))}:{string.Join(',', vertex.AdjacentVertexIds.OrderBy(id => id))}:{string.Join(',', vertex.AdjacentEdgeIds.OrderBy(id => id))}"))
        .Concat(board.Edges
            .OrderBy(edge => edge.EdgeId)
            .Select(edge => $"edge:{edge.EdgeId}:{edge.StartVertexId}:{edge.EndVertexId}:{string.Join(',', edge.AdjacentTileIds.OrderBy(id => id))}"))
        .Concat(board.HarborSlots
            .OrderBy(slot => slot.HarborSlotId)
            .Select(slot => $"harbor:{slot.HarborSlotId}:{slot.AdjacentEdgeId}:{slot.HarborType}:{slot.TradeRate}:{string.Join(',', slot.AdjacentVertexIds.OrderBy(id => id))}"))
        .ToList();
}

static void SetupPieceSupplyConsumption()
{
    var (_, service, game) = CreateGame();
    foreach (var player in game.Players)
    {
        var initialSupply = PlayerPieceSupplyService.GetSupply(game, player.PlayerId);
        AssertEqual(15, initialSupply.RemainingTrails, "Players should start with 15 Trails.");
        AssertEqual(5, initialSupply.RemainingCamps, "Players should start with 5 Camps.");
        AssertEqual(4, initialSupply.RemainingStrongholds, "Players should start with 4 Strongholds.");
    }

    var firstPlayer = game.CurrentPlayer;
    var firstCamp = FindValidSetupVertex(game);
    service.PlaceSetupCamp(game.RoomCode, firstPlayer.PlayerId, firstCamp.VertexId);
    var afterCamp = PlayerPieceSupplyService.GetSupply(game, firstPlayer.PlayerId);
    AssertEqual(4, afterCamp.RemainingCamps, "A setup Camp should consume one Camp piece.");
    AssertEqual(15, afterCamp.RemainingTrails, "A setup Camp should not consume a Trail piece.");

    service.PlaceSetupTrail(game.RoomCode, firstPlayer.PlayerId, FindSetupTrail(game, firstCamp.VertexId).EdgeId);
    var afterTrail = PlayerPieceSupplyService.GetSupply(game, firstPlayer.PlayerId);
    AssertEqual(14, afterTrail.RemainingTrails, "A setup Trail should consume one Trail piece.");

    CompleteSetup(service, game);

    foreach (var player in game.Players)
    {
        var supply = PlayerPieceSupplyService.GetSupply(game, player.PlayerId);
        AssertEqual(13, supply.RemainingTrails, "Each player should have two setup Trails on the board after setup.");
        AssertEqual(3, supply.RemainingCamps, "Each player should have two setup Camps on the board after setup.");
        AssertEqual(4, supply.RemainingStrongholds, "Setup should not consume Strongholds.");
        Assert(PlayerPieceSupplyService.HasValidInvariants(game, player.PlayerId), "Piece supply invariants should remain valid after setup.");
    }
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

    var (_, buildService, buildGame) = CreateGame();
    EnterNormalTurn(buildGame, rolled: false);
    ClearBoardOwnership(buildGame);
    var builder = buildGame.CurrentPlayer;
    GiveBuildSupplies(builder);
    var trailSeed = FindBuildableTrailSeed(buildGame);
    trailSeed.Vertex.OwnerPlayerId = builder.PlayerId;
    trailSeed.Vertex.StructureType = BoardStructureType.Camp;
    builder.CampsBuilt++;
    var suppliesBeforeRejectedBuild = builder.Supplies.ToDictionary(entry => entry.Key, entry => entry.Value);

    ExpectRuleError(() => buildService.BuildTrail(buildGame.RoomCode, builder.PlayerId, trailSeed.Edge.EdgeId), "roll");
    AssertEqual<string?>(null, trailSeed.Edge.OwnerPlayerId, "Paid Trail construction before rolling must not claim the edge.");
    AssertSuppliesEqual(suppliesBeforeRejectedBuild, builder, "Paid Trail construction before rolling must not spend supplies.");

    buildGame.HasRolledThisTurn = true;
    builder.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "active-road-building"
    };
    ExpectRuleError(() => buildService.BuildTrail(buildGame.RoomCode, builder.PlayerId, trailSeed.Edge.EdgeId), "Development Card action");
    AssertEqual<string?>(null, trailSeed.Edge.OwnerPlayerId, "Paid Trail construction must not bypass an active Road Building effect.");
    AssertSuppliesEqual(suppliesBeforeRejectedBuild, builder, "Rejected paid Trail construction during Road Building must remain atomic.");

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
        "Development Cards cannot be bought during setup.");
    ExpectRuleError(
        () => service.PlayMonopoly(game.RoomCode, setupPlayer.PlayerId, setupCard.CardId, ResourceType.Wood),
        "Development Cards cannot be used during setup.");

    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 3;
    }

    var oldCard = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);

    ExpectRuleError(
        () => service.BuyDevelopmentCard(game.RoomCode, current.PlayerId),
        "Roll the dice before buying a Development Card.");

    service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, oldCard.CardId, new[] { ResourceType.Wood, ResourceType.Grain });
    Assert(!game.HasRolledThisTurn, "Playing a Development Card before rolling should not mark dice as rolled.");
    Assert(current.HasPlayedDevelopmentCardThisTurn, "Playing a pre-roll Development Card should consume the one-card turn limit.");
    ExpectRuleError(
        () => service.EndTurn(game.RoomCode, current.PlayerId),
        "roll dice");
}

static void DevelopmentCardsPreRollTiming()
{
    var (_, plentyService, plentyGame) = CreateGame();
    EnterNormalTurn(plentyGame, rolled: false);
    var plentyPlayer = plentyGame.CurrentPlayer;
    ClearSupplies(plentyPlayer);
    var plenty = AddCard(plentyPlayer, DevelopmentCardType.YearOfPlenty, plentyGame.TurnNumber - 1);

    plentyService.PlayYearOfPlenty(plentyGame.RoomCode, plentyPlayer.PlayerId, plenty.CardId, new[] { ResourceType.Wood, ResourceType.Wood });
    AssertEqual(2, plentyPlayer.Supplies[ResourceType.Wood], "Year of Plenty should resolve before rolling.");
    Assert(!plentyGame.HasRolledThisTurn, "Year of Plenty before rolling should leave the roll gate closed.");
    plentyService.RollDice(plentyGame.RoomCode, plentyPlayer.PlayerId);
    Assert(plentyGame.HasRolledThisTurn, "The player should still be able to roll after resolving a pre-roll card.");

    var (_, monopolyService, monopolyGame) = CreateGame();
    EnterNormalTurn(monopolyGame, rolled: false);
    var monopolyPlayer = monopolyGame.CurrentPlayer;
    var monopolyOpponent = monopolyGame.Players.Single(player => player.PlayerId != monopolyPlayer.PlayerId);
    ClearSupplies(monopolyPlayer);
    ClearSupplies(monopolyOpponent);
    monopolyOpponent.Supplies[ResourceType.Stone] = 3;
    var monopoly = AddCard(monopolyPlayer, DevelopmentCardType.Monopoly, monopolyGame.TurnNumber - 1);

    monopolyService.PlayMonopoly(monopolyGame.RoomCode, monopolyPlayer.PlayerId, monopoly.CardId, ResourceType.Stone);
    AssertEqual(3, monopolyPlayer.Supplies[ResourceType.Stone], "Monopoly should transfer supplies before rolling.");
    AssertEqual(0, monopolyOpponent.Supplies[ResourceType.Stone], "Monopoly should remove selected supplies from opponents.");
    Assert(!monopolyGame.HasRolledThisTurn, "Monopoly before rolling should leave the roll gate closed.");

    var (_, knightService, knightGame) = CreateGame();
    EnterNormalTurn(knightGame, rolled: false);
    ClearBoardOwnership(knightGame);
    var knightPlayer = knightGame.CurrentPlayer;
    var knight = AddCard(knightPlayer, DevelopmentCardType.Knight, knightGame.TurnNumber - 1);

    knightService.PlayKnight(knightGame.RoomCode, knightPlayer.PlayerId, knight.CardId, "", null);
    AssertEqual(WardenAction.MoveWarden, knightGame.PendingWardenAction, "Knight before rolling should start Warden movement.");
    Assert(!knightGame.HasRolledThisTurn, "Knight before rolling should not roll dice.");
    ExpectRuleError(() => knightService.RollDice(knightGame.RoomCode, knightPlayer.PlayerId), "Warden");
    var knightTarget = knightGame.Board.Tiles.First(tile => tile.TileId != knightGame.WardenTileId);
    knightService.MoveWarden(knightGame.RoomCode, knightPlayer.PlayerId, knightTarget.TileId);
    AssertEqual(WardenAction.None, knightGame.PendingWardenAction, "Knight Warden flow should complete before rolling.");
    Assert(!knightGame.HasRolledThisTurn, "Resolving Knight should still leave dice unrolled.");

    var (_, roadService, roadGame) = CreateGame();
    EnterNormalTurn(roadGame, rolled: false);
    ClearBoardOwnership(roadGame);
    var roadPlayer = roadGame.CurrentPlayer;
    var trailSeed = FindBuildableTrailSeed(roadGame);
    trailSeed.Vertex.OwnerPlayerId = roadPlayer.PlayerId;
    trailSeed.Vertex.StructureType = BoardStructureType.Camp;
    roadPlayer.CampsBuilt++;
    var roadBuilding = AddCard(roadPlayer, DevelopmentCardType.RoadBuilding, roadGame.TurnNumber - 1);
    var secondCard = AddCard(roadPlayer, DevelopmentCardType.YearOfPlenty, roadGame.TurnNumber - 1);

    roadService.StartRoadBuilding(roadGame.RoomCode, roadPlayer.PlayerId, roadBuilding.CardId);
    Assert(roadPlayer.ActiveDevelopmentCardEffect is not null, "Road Building should create an active placement effect before rolling.");
    ExpectRuleError(() => roadService.RollDice(roadGame.RoomCode, roadPlayer.PlayerId), "Development Card action");
    ExpectRuleError(
        () => roadService.PlayYearOfPlenty(roadGame.RoomCode, roadPlayer.PlayerId, secondCard.CardId, new[] { ResourceType.Wood, ResourceType.Grain }),
        "Development Card action");
    roadService.PlaceFreeTrail(roadGame.RoomCode, roadPlayer.PlayerId, trailSeed.Edge.EdgeId);
    Assert(!roadGame.HasRolledThisTurn, "Placing a Road Building free Trail before rolling should leave dice unrolled.");
    roadService.CancelActiveDevelopmentCard(roadGame.RoomCode, roadPlayer.PlayerId);
    roadService.RollDice(roadGame.RoomCode, roadPlayer.PlayerId);
    Assert(roadGame.HasRolledThisTurn, "The player should roll after finishing the Road Building effect.");
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

static void PlayerTradeOfferCreationValidation()
{
    var (_, setupService, setupGame) = CreateGame();
    var setupCurrent = setupGame.CurrentPlayer;
    var setupOpponent = setupGame.Players.Single(player => player.PlayerId != setupCurrent.PlayerId);
    ExpectRuleError(
        () => setupService.CreateTradeOffer(
            setupGame.RoomCode,
            setupCurrent.PlayerId,
            setupOpponent.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "Trading is not available right now.");

    var (_, service, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    current.Supplies[ResourceType.Wood] = 2;

    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "You must roll before trading.");

    MarkRolled(game);
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            opponent.PlayerId,
            current.PlayerId,
            Supplies((ResourceType.Grain, 1)),
            Supplies((ResourceType.Wood, 1))),
        "Only the current player can create trade offers.");
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            current.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "You cannot trade with yourself.");
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, 0)),
            Supplies((ResourceType.Grain, 1))),
        "Trade offers must include Supplies from both players.");
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, -1)),
            Supplies((ResourceType.Grain, 1))),
        "Trade offers must include Supplies from both players.");
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Stone, 1)),
            Supplies((ResourceType.Grain, 1))),
        "Not enough Supplies for this offer.");

    opponent.Supplies[ResourceType.Grain] = 1;
    var updated = service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var offer = updated.TradeOffers.Single();
    AssertEqual(PlayerTradeOfferStatus.Pending, offer.Status, "Valid player trade offer should be pending.");

    var proposerDto = game.ToDto(current.PlayerId).TradeOffers.Single(dto => dto.TradeOfferId == offer.TradeOfferId);
    var targetDto = game.ToDto(opponent.PlayerId).TradeOffers.Single(dto => dto.TradeOfferId == offer.TradeOfferId);
    Assert(proposerDto.CanCancel, "Proposer DTO should allow cancelling their pending offer.");
    Assert(!proposerDto.CanAccept, "Proposer DTO should not allow accepting their own offer.");
    Assert(targetDto.CanAccept, "Target DTO should allow accepting an incoming offer.");
    Assert(targetDto.CanReject, "Target DTO should allow rejecting an incoming offer.");

    game.PendingWardenAction = WardenAction.MoveWarden;
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "Trading is not available right now.");

    game.PendingWardenAction = WardenAction.None;
    current.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "active-trade-block"
    };
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "Trading is not available right now.");

    current.ActiveDevelopmentCardEffect = null;
    game.Status = GameStatus.Finished;
    game.Phase = GamePhase.Finished;
    ExpectRuleError(
        () => service.CreateTradeOffer(
            game.RoomCode,
            current.PlayerId,
            opponent.PlayerId,
            Supplies((ResourceType.Wood, 1)),
            Supplies((ResourceType.Grain, 1))),
        "Trading is not available right now.");
}

static void PlayerTradeOfferResolution()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    var third = new PlayerGameState
    {
        PlayerId = "third-player",
        PlayerName = "Cy"
    };
    game.Players.Add(third);
    game.PlayerOrder.Add(third.PlayerId);

    ClearSupplies(current);
    ClearSupplies(opponent);
    ClearSupplies(third);
    current.Supplies[ResourceType.Wood] = 3;
    opponent.Supplies[ResourceType.Grain] = 2;

    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 2)),
        Supplies((ResourceType.Grain, 1)));
    var offer = game.TradeOffers.Single();

    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, third.PlayerId, offer.TradeOfferId),
        "Only the target player can accept this trade offer.");

    service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, offer.TradeOfferId);
    AssertEqual(PlayerTradeOfferStatus.Accepted, offer.Status, "Accepted offer should be marked accepted.");
    AssertEqual(1, current.Supplies[ResourceType.Wood], "Proposer should spend offered Wood.");
    AssertEqual(1, current.Supplies[ResourceType.Grain], "Proposer should receive requested Grain.");
    AssertEqual(2, opponent.Supplies[ResourceType.Wood], "Target should receive offered Wood.");
    AssertEqual(1, opponent.Supplies[ResourceType.Grain], "Target should spend requested Grain.");
    Assert(game.Log.Any(entry => entry.Message.Contains("accepted", StringComparison.OrdinalIgnoreCase)), "Accepted trade should write a log entry.");

    current.Supplies[ResourceType.Wood] = 1;
    opponent.Supplies[ResourceType.Grain] = 1;
    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var rejected = game.TradeOffers.Last();
    service.RejectTradeOffer(game.RoomCode, opponent.PlayerId, rejected.TradeOfferId);
    AssertEqual(PlayerTradeOfferStatus.Rejected, rejected.Status, "Rejected offer should be marked rejected.");
    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, rejected.TradeOfferId),
        "This trade offer is no longer available.");

    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var cancelled = game.TradeOffers.Last();
    service.CancelTradeOffer(game.RoomCode, current.PlayerId, cancelled.TradeOfferId);
    AssertEqual(PlayerTradeOfferStatus.Cancelled, cancelled.Status, "Cancelled offer should be marked cancelled.");
    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, cancelled.TradeOfferId),
        "This trade offer is no longer available.");

    current.Supplies[ResourceType.Wood] = 1;
    opponent.Supplies[ResourceType.Grain] = 1;
    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var staleTargetSupplies = game.TradeOffers.Last();
    opponent.Supplies[ResourceType.Grain] = 0;
    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, staleTargetSupplies.TradeOfferId),
        "The other player no longer has the requested Supplies.");
    AssertEqual(PlayerTradeOfferStatus.Pending, staleTargetSupplies.Status, "Failed acceptance should leave the offer pending and unchanged.");
    Assert(
        !game.ToDto(opponent.PlayerId).TradeOffers.Single(dto => dto.TradeOfferId == staleTargetSupplies.TradeOfferId).CanAccept,
        "DTO should proactively disable Accept when target Supplies changed.");

    current.Supplies[ResourceType.Wood] = 1;
    opponent.Supplies[ResourceType.Grain] = 1;
    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var staleProposerSupplies = game.TradeOffers.Last();
    current.Supplies[ResourceType.Wood] = 0;
    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, staleProposerSupplies.TradeOfferId),
        "The proposing player no longer has the offered Supplies.");
    AssertEqual(PlayerTradeOfferStatus.Pending, staleProposerSupplies.Status, "Failed acceptance should not mutate the offer when proposer Supplies changed.");
    Assert(
        !game.ToDto(opponent.PlayerId).TradeOffers.Single(dto => dto.TradeOfferId == staleProposerSupplies.TradeOfferId).CanAccept,
        "DTO should proactively disable Accept when proposer Supplies changed.");
}

static void FailedPlayerTradeAcceptanceIsAtomic()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var proposer = game.CurrentPlayer;
    var target = game.Players.Single(player => player.PlayerId != proposer.PlayerId);
    ClearSupplies(proposer);
    ClearSupplies(target);
    proposer.Supplies[ResourceType.Wood] = 2;
    target.Supplies[ResourceType.Grain] = 1;

    service.CreateTradeOffer(
        game.RoomCode,
        proposer.PlayerId,
        target.PlayerId,
        Supplies((ResourceType.Wood, 2)),
        Supplies((ResourceType.Grain, 1)));
    var offer = game.TradeOffers.Single();
    target.Supplies[ResourceType.Grain] = 0;
    var proposerBefore = proposer.Supplies.ToDictionary(item => item.Key, item => item.Value);
    var targetBefore = target.Supplies.ToDictionary(item => item.Key, item => item.Value);

    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, target.PlayerId, offer.TradeOfferId),
        "no longer has the requested Supplies");

    AssertEqual(PlayerTradeOfferStatus.Pending, offer.Status, "Failed acceptance should not mutate offer status.");
    Assert(offer.ResolvedAt is null, "Failed acceptance should not stamp a resolution time.");
    AssertSuppliesEqual(proposerBefore, proposer, "Failed acceptance proposer atomicity:");
    AssertSuppliesEqual(targetBefore, target, "Failed acceptance target atomicity:");

    var targetDto = game.ToDto(target.PlayerId).TradeOffers.Single(dto => dto.TradeOfferId == offer.TradeOfferId);
    Assert(!targetDto.CanAccept, "Authoritative DTO should disable Accept when requested Supplies are gone.");
    Assert(targetDto.CanReject, "Target should still be able to reject a stale pending offer.");
}

static void PlayerTradeOfferExpiry()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    ClearSupplies(current);
    ClearSupplies(opponent);
    current.Supplies[ResourceType.Wood] = 2;
    opponent.Supplies[ResourceType.Grain] = 2;

    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var turnOffer = game.TradeOffers.Single();
    service.EndTurn(game.RoomCode, current.PlayerId);
    AssertEqual(PlayerTradeOfferStatus.Expired, turnOffer.Status, "Pending offers should expire when the current player ends the turn.");
    ExpectRuleError(
        () => service.AcceptTradeOffer(game.RoomCode, opponent.PlayerId, turnOffer.TradeOfferId),
        "This trade offer is no longer available.");

    game.CurrentPlayerIndex = game.Players.FindIndex(player => player.PlayerId == current.PlayerId);
    MarkRolled(game);
    current.Supplies[ResourceType.Wood] = 1;
    opponent.Supplies[ResourceType.Grain] = 1;
    service.CreateTradeOffer(
        game.RoomCode,
        current.PlayerId,
        opponent.PlayerId,
        Supplies((ResourceType.Wood, 1)),
        Supplies((ResourceType.Grain, 1)));
    var wardenOffer = game.TradeOffers.Last();
    GameService.ApplyDiceRoll(game, current, 7, isDebug: false);
    AssertEqual(PlayerTradeOfferStatus.Expired, wardenOffer.Status, "Pending offers should expire when Warden flow starts.");
    Assert(game.Log.Any(entry => entry.Message == "Pending trade offers expired."), "Offer expiry should write a compact log entry.");
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
        "You can play only one Development Card per turn.");

    var opponentCard = AddCard(opponent, DevelopmentCardType.Monopoly, game.TurnNumber - 1);
    ExpectRuleError(() => service.PlayMonopoly(game.RoomCode, current.PlayerId, opponentCard.CardId, ResourceType.Wood), "own");

    service.EndTurn(game.RoomCode, current.PlayerId);
    Assert(!current.HasPlayedDevelopmentCardThisTurn, "Ending a turn should reset the ending player's development-card play flag.");
    Assert(!game.CurrentPlayer.HasPlayedDevelopmentCardThisTurn, "Ending a turn should reset the next player's development-card play flag.");
    ExpectRuleError(() => service.PlayMonopoly(game.RoomCode, current.PlayerId, secondCard.CardId, ResourceType.Wood), "not your turn");
}

static void FriendlyValidationErrors()
{
    var directError = new GameRuleException("You can play only one Development Card per turn.");
    AssertEqual(
        "DevelopmentCardAlreadyPlayedThisTurn",
        directError.ErrorCode,
        "Development-card play-limit errors should have a stable code.");
    AssertEqual(
        "You can play only one Development Card per turn.",
        directError.UserMessage,
        "UserMessage should stay player-facing.");

    var payload = HubErrorSerializer.Serialize(directError.ErrorCode, directError.UserMessage);
    using var payloadDocument = JsonDocument.Parse(payload);
    var payloadRoot = payloadDocument.RootElement;
    AssertEqual(
        "DevelopmentCardAlreadyPlayedThisTurn",
        payloadRoot.GetProperty("errorCode").GetString() ?? "",
        "Hub validation payload should include the error code.");
    AssertEqual(
        "You can play only one Development Card per turn.",
        payloadRoot.GetProperty("userMessage").GetString() ?? "",
        "Hub validation payload should include the user-facing message.");
    Assert(!payload.Contains("PlayKnight", StringComparison.OrdinalIgnoreCase), "Hub validation payload should not expose hub method names.");
    Assert(!payload.Contains("unexpected error", StringComparison.OrdinalIgnoreCase), "Hub validation payload should not include SignalR failure text.");

    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var firstCard = AddCard(current, DevelopmentCardType.YearOfPlenty, game.TurnNumber - 1);
    service.PlayYearOfPlenty(game.RoomCode, current.PlayerId, firstCard.CardId, new[] { ResourceType.Wood, ResourceType.Wood });

    var knight = AddCard(current, DevelopmentCardType.Knight, game.TurnNumber - 1);
    var playKnightError = CaptureRuleError(() => service.PlayKnight(game.RoomCode, current.PlayerId, knight.CardId, "", null));
    AssertEqual(
        "DevelopmentCardAlreadyPlayedThisTurn",
        playKnightError.ErrorCode,
        "PlayKnight should return the friendly one-card-per-turn validation code.");
    AssertEqual(
        "You can play only one Development Card per turn.",
        playKnightError.UserMessage,
        "PlayKnight should return the friendly one-card-per-turn message.");
    Assert(!playKnightError.Message.Contains("PlayKnight", StringComparison.OrdinalIgnoreCase), "Rule error should not expose the hub method name.");
    Assert(!playKnightError.Message.Contains("unexpected error", StringComparison.OrdinalIgnoreCase), "Rule error should not expose SignalR invocation text.");
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

static void KnightWardenAndLargestArmy()
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
    AssertEqual(current.PlayerId, game.LargestArmyPlayerId, "Largest Army should be awarded after 3 Knights.");
    AssertEqual(3, game.LargestArmyKnightCount, "Largest Army should track the holder's played Knight count.");
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

static void LargestArmyScoringRules()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.Single(player => player.PlayerId != current.PlayerId);
    ClearBoardOwnership(game);
    ClearSupplies(current);
    ClearSupplies(opponent);

    AddCard(current, DevelopmentCardType.Knight, game.TurnNumber - 1);
    PlayKnightAndResolveWarden(service, game, current);
    PlayKnightAndResolveWarden(service, game, current);
    AssertEqual(2, current.PlayedKnightCount, "Only played Knights should count toward Largest Army.");
    AssertEqual<string?>(null, game.LargestArmyPlayerId, "Largest Army should not be awarded before 3 played Knights.");
    AssertEqual(0, game.LargestArmyKnightCount, "Largest Army count should stay 0 before it is awarded.");

    var thirdKnight = PlayKnightAndResolveWarden(service, game, current);
    Assert(thirdKnight.IsPlayed, "The Knight card should be marked played immediately.");
    AssertEqual(3, current.PlayedKnightCount, "Played Knight count should increment once per successful Knight play.");
    AssertEqual(current.PlayerId, game.LargestArmyPlayerId, "First player to 3 played Knights should claim Largest Army.");
    AssertEqual(3, game.LargestArmyKnightCount, "Largest Army should record the claimed Knight count.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Largest Army should add +2 Victory Points.");
    Assert(game.Log.Any(entry => entry.Message.Contains("claimed Largest Army", StringComparison.OrdinalIgnoreCase)), "Claiming Largest Army should be logged.");

    ExpectRuleError(() => service.PlayKnight(game.RoomCode, current.PlayerId, thirdKnight.CardId, "", null), "already");
    AssertEqual(3, current.PlayedKnightCount, "Replaying a Knight should not increment playedKnightCount twice.");

    PlayKnightAndResolveWarden(service, game, opponent);
    PlayKnightAndResolveWarden(service, game, opponent);
    PlayKnightAndResolveWarden(service, game, opponent);
    AssertEqual(3, opponent.PlayedKnightCount, "Opponent should reach a tied Knight count.");
    AssertEqual(current.PlayerId, game.LargestArmyPlayerId, "Ties should not transfer Largest Army.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Current holder should keep Largest Army points on a tie.");
    AssertEqual(0, GameService.CalculateVictoryPoints(game, opponent, revealHidden: true), "Tied challenger should not receive Largest Army points.");

    PlayKnightAndResolveWarden(service, game, opponent);
    AssertEqual(opponent.PlayerId, game.LargestArmyPlayerId, "A player with strictly more played Knights should take Largest Army.");
    AssertEqual(4, game.LargestArmyKnightCount, "Largest Army count should update after transfer.");
    AssertEqual(0, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Previous holder should lose Largest Army points after transfer.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, opponent, revealHidden: true), "New holder should gain Largest Army points after transfer.");
    Assert(game.Log.Any(entry => entry.Message.Contains("took Largest Army", StringComparison.OrdinalIgnoreCase)), "Largest Army transfer should be logged.");

    var hiddenKnight = AddCard(current, DevelopmentCardType.Knight, game.TurnNumber - 1);
    var hiddenVictoryPoint = AddCard(current, DevelopmentCardType.VictoryPoint, game.TurnNumber - 1);
    var opponentDto = game.ToDto(opponent.PlayerId).Players.Single(player => player.PlayerId == current.PlayerId);
    AssertEqual(3, current.PlayedKnightCount, "Bought but unplayed Knights should not count toward Largest Army.");
    AssertEqual(current.DevelopmentCards.Count, opponentDto.DevelopmentCardCount, "Opponents should see the total development-card count.");
    AssertEqual(0, opponentDto.DevelopmentCards.Count, "Opponents should not see exact hidden unplayed card types.");
    Assert(!hiddenKnight.IsPlayed && !hiddenVictoryPoint.IsPlayed, "Hidden card setup should leave cards unplayed.");
}

static void LargestArmyWinDetection()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    ClearBoardOwnership(game);
    current.CampsBuilt = 8;

    PlayKnightAndResolveWarden(service, game, current);
    PlayKnightAndResolveWarden(service, game, current);
    PlayKnightAndResolveWarden(service, game, current);

    AssertEqual(current.PlayerId, game.LargestArmyPlayerId, "Largest Army should be held by the winning player.");
    AssertEqual(10, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Largest Army should be included in win scoring.");
    AssertEqual(GameStatus.Finished, game.Status, "Largest Army points should be able to trigger the normal win flow.");
    AssertEqual(GamePhase.Finished, game.Phase, "Winning through Largest Army should finish the match.");
    AssertEqual(current.PlayerId, game.WinnerPlayerId, "Winning through Largest Army should set the winner.");
}

static void LongestTrailScoringRules()
{
    var (game, current, opponent) = CreateTrailScoringGame();
    AddTrailPath(game, current, "a", 4);
    GameService.RecalculateLongestTrail(game, current.PlayerId);
    AssertEqual<string?>(null, game.LongestTrailPlayerId, "Longest Trail should not be awarded below 5 Trails.");
    AssertEqual(0, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "No Longest Trail bonus should apply below 5.");

    ClearTrailGraphOwnership(game);
    AddTrailPath(game, current, "a", 5);
    GameService.RecalculateLongestTrail(game, current.PlayerId);
    AssertEqual(current.PlayerId, game.LongestTrailPlayerId, "Exactly 5 connected Trails should claim Longest Trail.");
    AssertEqual(5, game.LongestTrailLength, "Game state should store the holder length.");
    AssertEqual(5, current.LongestTrailLength, "Player summary length should be updated.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Longest Trail should add +2 Victory Points.");
    var currentDto = game.ToDto(current.PlayerId);
    AssertEqual(current.PlayerId, currentDto.LongestTrailPlayerId, "DTO should expose the Longest Trail holder.");
    AssertEqual(5, currentDto.LongestTrailLength, "DTO should expose the Longest Trail holder length.");
    Assert(currentDto.Players.Single(player => player.PlayerId == current.PlayerId).HasLongestTrail, "Player DTO should mark the Longest Trail holder.");

    AddTrailPath(game, opponent, "b", 5);
    GameService.RecalculateLongestTrail(game, opponent.PlayerId);
    AssertEqual(current.PlayerId, game.LongestTrailPlayerId, "A tied Longest Trail should not transfer the bonus.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Current holder should keep the +2 VP on a tie.");
    AssertEqual(0, GameService.CalculateVictoryPoints(game, opponent, revealHidden: true), "Tied challenger should not receive +2 VP.");

    ClearPlayerTrail(game, opponent.PlayerId);
    AddTrailPath(game, opponent, "b", 6);
    GameService.RecalculateLongestTrail(game, opponent.PlayerId);
    AssertEqual(opponent.PlayerId, game.LongestTrailPlayerId, "A strictly longer Trail should transfer the bonus.");
    AssertEqual(6, game.LongestTrailLength, "Transferred Longest Trail should store the new length.");
    AssertEqual(0, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Previous holder should lose the Longest Trail bonus.");
    AssertEqual(2, GameService.CalculateVictoryPoints(game, opponent, revealHidden: true), "New holder should gain the Longest Trail bonus.");
    Assert(game.Log.Any(entry => entry.Message.Contains("took Longest Trail", StringComparison.OrdinalIgnoreCase)), "Taking Longest Trail should be logged.");

    var blocker = game.Board.Vertices.Single(vertex => vertex.VertexId == "b-v3");
    blocker.OwnerPlayerId = current.PlayerId;
    blocker.StructureType = BoardStructureType.Camp;
    var currentBlocker = game.Board.Vertices.Single(vertex => vertex.VertexId == "a-v3");
    currentBlocker.OwnerPlayerId = opponent.PlayerId;
    currentBlocker.StructureType = BoardStructureType.Camp;
    GameService.RecalculateLongestTrail(game, current.PlayerId);
    AssertEqual<string?>(null, game.LongestTrailPlayerId, "Interrupted holder with no unique replacement should clear Longest Trail.");
    AssertEqual(0, game.LongestTrailLength, "Cleared Longest Trail should store length 0.");
    Assert(game.Log.Any(entry => entry.Message.Contains("Trail was interrupted", StringComparison.OrdinalIgnoreCase)), "Trail interruption should be logged.");
    Assert(game.Log.Any(entry => entry.Message.Contains("Longest Trail is currently unclaimed", StringComparison.OrdinalIgnoreCase)), "Clearing Longest Trail should be logged.");
}

static void LongestTrailGraphRules()
{
    var (branchGame, current, _) = CreateTrailScoringGame();
    AddTrailVertex(branchGame, "center");
    AddTrailVertex(branchGame, "n");
    AddTrailVertex(branchGame, "s");
    AddTrailVertex(branchGame, "e");
    AddTrailVertex(branchGame, "w");
    AddTrailEdge(branchGame, "branch-1", "center", "n", current.PlayerId);
    AddTrailEdge(branchGame, "branch-2", "center", "s", current.PlayerId);
    AddTrailEdge(branchGame, "branch-3", "center", "e", current.PlayerId);
    AddTrailEdge(branchGame, "branch-4", "center", "w", current.PlayerId);
    GameService.RecalculateLongestTrail(branchGame, current.PlayerId);
    AssertEqual(2, current.LongestTrailLength, "Branches should not be summed into one Longest Trail path.");

    var (cycleGame, cyclePlayer, _) = CreateTrailScoringGame();
    AddTrailVertex(cycleGame, "c0");
    AddTrailVertex(cycleGame, "c1");
    AddTrailVertex(cycleGame, "c2");
    AddTrailVertex(cycleGame, "c3");
    AddTrailEdge(cycleGame, "cycle-1", "c0", "c1", cyclePlayer.PlayerId);
    AddTrailEdge(cycleGame, "cycle-2", "c1", "c2", cyclePlayer.PlayerId);
    AddTrailEdge(cycleGame, "cycle-3", "c2", "c0", cyclePlayer.PlayerId);
    AddTrailEdge(cycleGame, "cycle-4", "c2", "c3", cyclePlayer.PlayerId);
    GameService.RecalculateLongestTrail(cycleGame, cyclePlayer.PlayerId);
    AssertEqual(4, cyclePlayer.LongestTrailLength, "Cycles should be traversed without reusing an edge or looping forever.");

    var (ownStructureGame, ownPlayer, _) = CreateTrailScoringGame();
    AddTrailPath(ownStructureGame, ownPlayer, "own", 5);
    var ownNode = ownStructureGame.Board.Vertices.Single(vertex => vertex.VertexId == "own-v2");
    ownNode.OwnerPlayerId = ownPlayer.PlayerId;
    ownNode.StructureType = BoardStructureType.Camp;
    GameService.RecalculateLongestTrail(ownStructureGame, ownPlayer.PlayerId);
    AssertEqual(5, ownPlayer.LongestTrailLength, "A player's own Camp or Stronghold should not interrupt their Trail.");
    AssertEqual(ownPlayer.PlayerId, ownStructureGame.LongestTrailPlayerId, "Own structures should allow Longest Trail to remain claimable.");

    var (opponentBlockGame, blockedPlayer, blockerPlayer) = CreateTrailScoringGame();
    AddTrailPath(opponentBlockGame, blockedPlayer, "blocked", 6);
    var blockNode = opponentBlockGame.Board.Vertices.Single(vertex => vertex.VertexId == "blocked-v3");
    blockNode.OwnerPlayerId = blockerPlayer.PlayerId;
    blockNode.StructureType = BoardStructureType.Stronghold;
    GameService.RecalculateLongestTrail(opponentBlockGame, blockedPlayer.PlayerId);
    AssertEqual(3, blockedPlayer.LongestTrailLength, "Opponent structures should stop traversal but still allow endpoints to count.");
    AssertEqual<string?>(null, opponentBlockGame.LongestTrailPlayerId, "An interrupted path below 5 should not qualify.");
}

static void RoadBuildingFreeTrailRecalculatesLongestTrail()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    ClearBoardOwnership(game);
    var path = FindConnectedEdgePath(game, 5);
    var startVertex = game.Board.Vertices.Single(vertex => vertex.VertexId == path[0].StartVertexId);
    startVertex.OwnerPlayerId = current.PlayerId;
    startVertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;

    foreach (var edge in path.Take(4))
    {
        edge.OwnerPlayerId = current.PlayerId;
        current.TrailsBuilt++;
    }

    current.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "debug-road-building"
    };

    service.PlaceFreeTrail(game.RoomCode, current.PlayerId, path[4].EdgeId);
    AssertEqual(current.PlayerId, game.LongestTrailPlayerId, "Placing a free Road Building Trail should recalculate and claim Longest Trail.");
    AssertEqual(5, game.LongestTrailLength, "Free Trail placement should update the Longest Trail length.");
    AssertEqual(5, current.LongestTrailLength, "Player length should update after free Trail placement.");
    AssertEqual(1, current.ActiveDevelopmentCardEffect?.FreeTrailsPlaced ?? 0, "Road Building should track the placed free Trail.");
}

static void TrailPlacementConnectsThroughOwnedStructures()
{
    foreach (var structureType in new[] { BoardStructureType.Camp, BoardStructureType.Stronghold })
    {
        var (_, service, game) = CreateGame();
        EnterNormalTurn(game);
        ClearBoardOwnership(game);
        var current = game.CurrentPlayer;
        var opponent = game.Players.First(player => player.PlayerId != current.PlayerId);
        GiveBuildSupplies(current);

        var connector = FindTrailConnector(game);
        connector.Vertex.OwnerPlayerId = current.PlayerId;
        connector.Vertex.StructureType = structureType;
        IncrementStructureCount(current, structureType);

        var farVertexId = OtherVertexId(connector.TargetEdge, connector.Vertex.VertexId);
        var farVertex = game.Board.Vertices.Single(vertex => vertex.VertexId == farVertexId);
        farVertex.OwnerPlayerId = opponent.PlayerId;
        farVertex.StructureType = BoardStructureType.Camp;
        opponent.CampsBuilt++;

        service.BuildTrail(game.RoomCode, current.PlayerId, connector.TargetEdge.EdgeId);
        AssertEqual(
            current.PlayerId,
            connector.TargetEdge.OwnerPlayerId,
            $"Trail should be placeable from the player's own {structureType}, even when the other endpoint is occupied by an opponent.");
    }
}

static void TrailPlacementConnectivityValidation()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;
    GiveBuildSupplies(current);

    var connector = FindTrailConnector(game);
    connector.ExistingEdge.OwnerPlayerId = current.PlayerId;
    current.TrailsBuilt++;

    service.BuildTrail(game.RoomCode, current.PlayerId, connector.TargetEdge.EdgeId);
    AssertEqual(current.PlayerId, connector.TargetEdge.OwnerPlayerId, "Trail should be placeable through an empty node connected to an owned Trail.");

    var (_, disconnectedService, disconnectedGame) = CreateGame();
    EnterNormalTurn(disconnectedGame);
    ClearBoardOwnership(disconnectedGame);
    var disconnectedPlayer = disconnectedGame.CurrentPlayer;
    GiveBuildSupplies(disconnectedPlayer);
    var openEdge = disconnectedGame.Board.Edges.OrderBy(edge => edge.EdgeId).First();

    ExpectRuleError(
        () => disconnectedService.BuildTrail(disconnectedGame.RoomCode, disconnectedPlayer.PlayerId, openEdge.EdgeId),
        "Trails must connect");
    AssertEqual<string?>(null, openEdge.OwnerPlayerId, "Disconnected rejected Trail should not claim an edge.");
}

static void TrailPlacementBlocksOpponentStructures()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;
    var opponent = game.Players.First(player => player.PlayerId != current.PlayerId);
    GiveBuildSupplies(current);

    var connector = FindTrailConnector(game);
    connector.ExistingEdge.OwnerPlayerId = current.PlayerId;
    current.TrailsBuilt++;
    connector.Vertex.OwnerPlayerId = opponent.PlayerId;
    connector.Vertex.StructureType = BoardStructureType.Stronghold;
    opponent.StrongholdsBuilt++;

    ExpectRuleError(
        () => service.BuildTrail(game.RoomCode, current.PlayerId, connector.TargetEdge.EdgeId),
        "opponent");
    AssertEqual<string?>(null, connector.TargetEdge.OwnerPlayerId, "Opponent-occupied connector should block Trail continuation.");
}

static void SetupTrailConnectsToJustPlacedCamp()
{
    var (_, service, game) = CreateGame();
    ClearBoardOwnership(game);
    var player = game.CurrentPlayer;
    var camp = FindValidSetupVertex(game);

    service.PlaceSetupCamp(game.RoomCode, player.PlayerId, camp.VertexId);
    var trail = FindSetupTrail(game, camp.VertexId);
    service.PlaceSetupTrail(game.RoomCode, player.PlayerId, trail.EdgeId);

    AssertEqual(player.PlayerId, camp.OwnerPlayerId, "Setup Camp should remain on the selected build node.");
    AssertEqual(player.PlayerId, trail.OwnerPlayerId, "Setup Trail should connect to the just-placed Camp.");
}

static void RoadBuildingUsesTrailConnectivityRules()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;

    var connector = FindTrailConnector(game);
    connector.Vertex.OwnerPlayerId = current.PlayerId;
    connector.Vertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    current.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "road-building-connectivity"
    };

    service.PlaceFreeTrail(game.RoomCode, current.PlayerId, connector.TargetEdge.EdgeId);
    AssertEqual(current.PlayerId, connector.TargetEdge.OwnerPlayerId, "Road Building should allow free Trail placement from an owned Camp.");

    var (_, blockedService, blockedGame) = CreateGame();
    EnterNormalTurn(blockedGame);
    ClearBoardOwnership(blockedGame);
    var blockedPlayer = blockedGame.CurrentPlayer;
    var blockingOpponent = blockedGame.Players.First(player => player.PlayerId != blockedPlayer.PlayerId);
    var blockedConnector = FindTrailConnector(blockedGame);
    blockedConnector.ExistingEdge.OwnerPlayerId = blockedPlayer.PlayerId;
    blockedPlayer.TrailsBuilt++;
    blockedConnector.Vertex.OwnerPlayerId = blockingOpponent.PlayerId;
    blockedConnector.Vertex.StructureType = BoardStructureType.Camp;
    blockingOpponent.CampsBuilt++;
    blockedPlayer.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "road-building-blocked-connectivity"
    };

    ExpectRuleError(
        () => blockedService.PlaceFreeTrail(blockedGame.RoomCode, blockedPlayer.PlayerId, blockedConnector.TargetEdge.EdgeId),
        "opponent");
    AssertEqual<string?>(null, blockedConnector.TargetEdge.OwnerPlayerId, "Road Building should not bypass opponent structure blocking.");
}

static void NormalBuildingConsumesFinitePieces()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;
    ClearSupplies(current);

    var trailSeed = FindBuildableTrailSeed(game);
    trailSeed.Vertex.OwnerPlayerId = current.PlayerId;
    trailSeed.Vertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 10;
    }

    service.BuildTrail(game.RoomCode, current.PlayerId, trailSeed.Edge.EdgeId);
    var afterTrail = PlayerPieceSupplyService.GetSupply(game, current.PlayerId);
    AssertEqual(14, afterTrail.RemainingTrails, "Paid Trail build should consume one Trail piece.");
    AssertEqual(4, afterTrail.RemainingCamps, "Seed Camp should consume one Camp piece.");

    var campExtension = FindCampExtensionEdge(game, current.PlayerId);
    campExtension.Edge.OwnerPlayerId = current.PlayerId;
    current.TrailsBuilt++;
    var campTarget = campExtension.TargetVertex;
    service.BuildCamp(game.RoomCode, current.PlayerId, campTarget.VertexId);
    var afterCamp = PlayerPieceSupplyService.GetSupply(game, current.PlayerId);
    AssertEqual(3, afterCamp.RemainingCamps, "Paid Camp build should consume one Camp piece.");

    service.BuildStronghold(game.RoomCode, current.PlayerId, campTarget.VertexId);
    var afterStronghold = PlayerPieceSupplyService.GetSupply(game, current.PlayerId);
    AssertEqual(4, afterStronghold.RemainingCamps, "Upgrading to a Stronghold should return the Camp piece.");
    AssertEqual(3, afterStronghold.RemainingStrongholds, "Upgrading should consume one Stronghold piece.");
    Assert(PlayerPieceSupplyService.HasValidInvariants(game, current.PlayerId), "Paid builds should preserve finite piece invariants.");
}

static void PieceExhaustionBlocksBuildsAtomically()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;
    foreach (var resource in ResourceTypes.All)
    {
        current.Supplies[resource] = 10;
    }

    var trailSeed = FindBuildableTrailSeed(game);
    trailSeed.Vertex.OwnerPlayerId = current.PlayerId;
    trailSeed.Vertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    foreach (var edge in game.Board.Edges.Take(PlayerPieceSupplyService.TotalTrails))
    {
        edge.OwnerPlayerId = current.PlayerId;
        current.TrailsBuilt++;
    }

    var suppliesBeforeTrail = current.Supplies.ToDictionary(entry => entry.Key, entry => entry.Value);
    ExpectRuleError(() => service.BuildTrail(game.RoomCode, current.PlayerId, game.Board.Edges.First(edge => edge.OwnerPlayerId is null).EdgeId), "no Trail");
    AssertSuppliesEqual(suppliesBeforeTrail, current, "Failed Trail build should not spend supplies.");

    ClearBoardOwnership(game);
    foreach (var vertex in game.Board.Vertices.Take(PlayerPieceSupplyService.TotalCamps))
    {
        vertex.OwnerPlayerId = current.PlayerId;
        vertex.StructureType = BoardStructureType.Camp;
        current.CampsBuilt++;
    }
    var suppliesBeforeCamp = current.Supplies.ToDictionary(entry => entry.Key, entry => entry.Value);
    ExpectRuleError(() => service.BuildCamp(game.RoomCode, current.PlayerId, game.Board.Vertices.First(vertex => vertex.OwnerPlayerId is null).VertexId), "no Camp");
    AssertSuppliesEqual(suppliesBeforeCamp, current, "Failed Camp build should not spend supplies.");

    ClearBoardOwnership(game);
    foreach (var vertex in game.Board.Vertices.Take(PlayerPieceSupplyService.TotalStrongholds))
    {
        vertex.OwnerPlayerId = current.PlayerId;
        vertex.StructureType = BoardStructureType.Stronghold;
        current.StrongholdsBuilt++;
    }
    var upgradeCamp = game.Board.Vertices.Skip(PlayerPieceSupplyService.TotalStrongholds).First();
    upgradeCamp.OwnerPlayerId = current.PlayerId;
    upgradeCamp.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    var suppliesBeforeStronghold = current.Supplies.ToDictionary(entry => entry.Key, entry => entry.Value);
    ExpectRuleError(() => service.BuildStronghold(game.RoomCode, current.PlayerId, upgradeCamp.VertexId), "no Stronghold");
    AssertSuppliesEqual(suppliesBeforeStronghold, current, "Failed Stronghold build should not spend supplies.");
}

static void RoadBuildingConsumesFiniteTrailPieces()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    ClearBoardOwnership(game);
    var current = game.CurrentPlayer;
    var path = FindConnectedEdgePath(game, PlayerPieceSupplyService.TotalTrails);

    var startVertex = game.Board.Vertices.Single(vertex => vertex.VertexId == path[0].StartVertexId);
    startVertex.OwnerPlayerId = current.PlayerId;
    startVertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;

    foreach (var edge in path.Take(PlayerPieceSupplyService.TotalTrails - 1))
    {
        edge.OwnerPlayerId = current.PlayerId;
        current.TrailsBuilt++;
    }

    current.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "road-building-piece-test"
    };

    service.PlaceFreeTrail(game.RoomCode, current.PlayerId, path.Last().EdgeId);
    var afterLastTrail = PlayerPieceSupplyService.GetSupply(game, current.PlayerId);
    AssertEqual(0, afterLastTrail.RemainingTrails, "Road Building free Trail should consume the final Trail piece.");
    AssertEqual<ActiveDevelopmentCardEffect?>(null, current.ActiveDevelopmentCardEffect, "Road Building should clear when no Trail pieces remain.");

    current.ActiveDevelopmentCardEffect = new ActiveDevelopmentCardEffect
    {
        Type = ActiveDevelopmentCardType.RoadBuilding,
        CardId = "road-building-empty-test"
    };
    var openEdge = game.Board.Edges.First(edge => edge.OwnerPlayerId is null);
    ExpectRuleError(() => service.PlaceFreeTrail(game.RoomCode, current.PlayerId, openEdge.EdgeId), "no Trail");
    AssertEqual<string?>(null, openEdge.OwnerPlayerId, "Rejected Road Building placement should not claim an edge.");
}

static void LongestTrailWinDetection()
{
    var (game, current, _) = CreateTrailScoringGame();
    current.CampsBuilt = 8;
    AddTrailPath(game, current, "win", 5);

    GameService.RecalculateLongestTrail(game, current.PlayerId);

    AssertEqual(current.PlayerId, game.LongestTrailPlayerId, "Winning player should claim Longest Trail.");
    AssertEqual(10, GameService.CalculateVictoryPoints(game, current, revealHidden: true), "Longest Trail should be included in win scoring.");
    AssertEqual(GameStatus.Finished, game.Status, "Longest Trail points should be able to trigger the normal win flow.");
    AssertEqual(GamePhase.Finished, game.Phase, "Winning through Longest Trail should finish the match.");
    AssertEqual(current.PlayerId, game.WinnerPlayerId, "Winning through Longest Trail should set the winner.");
}

static void RoadBuildingEffect()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game);
    var current = game.CurrentPlayer;
    ClearBoardOwnership(game);
    var seed = FindBuildableTrailSeed(game);
    seed.Vertex.OwnerPlayerId = current.PlayerId;
    seed.Vertex.StructureType = BoardStructureType.Camp;
    current.CampsBuilt++;
    var road = AddCard(current, DevelopmentCardType.RoadBuilding, game.TurnNumber - 1);

    service.StartRoadBuilding(game.RoomCode, current.PlayerId, road.CardId);
    Assert(current.ActiveDevelopmentCardEffect?.Type == ActiveDevelopmentCardType.RoadBuilding, "Road Building should create an active effect.");
    ExpectRuleError(() => service.PlaceFreeTrail(game.RoomCode, current.PlayerId, "missing-edge"), "valid Trail edge");
}

static void RoadBuildingPrevalidationIsAtomic()
{
    var (_, service, game) = CreateGame();
    EnterNormalTurn(game, rolled: false);
    var current = game.CurrentPlayer;
    ClearBoardOwnership(game);
    var road = AddCard(current, DevelopmentCardType.RoadBuilding, game.TurnNumber - 1);
    var deckCount = game.DevelopmentDeck.Count;

    ExpectRuleError(
        () => service.StartRoadBuilding(game.RoomCode, current.PlayerId, road.CardId),
        "No legal Trail placement");

    Assert(!road.IsPlayed, "Unusable Road Building card should remain unplayed.");
    Assert(!current.HasPlayedDevelopmentCardThisTurn, "Failed Road Building should not consume the per-turn card limit.");
    Assert(current.ActiveDevelopmentCardEffect is null, "Failed Road Building should not create a pending effect.");
    AssertEqual(deckCount, game.DevelopmentDeck.Count, "Failed Road Building should not change the finite deck.");
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
    var deckCountBefore = game.DevelopmentDeck.Count;

    debug.GiveDevelopmentCard(room, host, player.PlayerId, DevelopmentCardType.VictoryPoint);
    AssertEqual(1, player.DevelopmentCards.Count, "Debug should give the selected player a card.");
    AssertEqual(DevelopmentCardType.VictoryPoint, player.DevelopmentCards.Single().Type, "Debug should give the selected card type.");
    AssertEqual(deckCountBefore - 1, game.DevelopmentDeck.Count, "Debug selected draw should remove a card from the real deck.");

    game.DevelopmentDeck.RemoveAll(card => card.Type == DevelopmentCardType.VictoryPoint);
    ExpectRuleError(
        () => debug.GiveDevelopmentCard(room, host, player.PlayerId, DevelopmentCardType.VictoryPoint),
        "No VictoryPoint cards remain");

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

static void LobbySessionResume()
{
    var lobby = new LobbyService();
    var host = lobby.CreateRoomSession("session-host", "Ari");
    var guest = lobby.JoinRoomSession("session-guest", host.Room.RoomCode, "Bea");
    var versionBeforeResume = host.Room.RoomStateVersion;

    ExpectLobbyError(
        () => lobby.ResumeRoomSession("session-invalid", host.Room.RoomCode, host.Player.PlayerId, "not-a-valid-token"),
        "restored");

    var resumed = lobby.ResumeRoomSession("session-host-reconnected", host.Room.RoomCode, host.Player.PlayerId, host.ReconnectToken);
    var room = lobby.GetRoom(host.Room.RoomCode)!;

    AssertEqual(2, room.Players.Count, "Resuming must reuse the existing player instead of adding a duplicate.");
    AssertEqual(host.Player.PlayerId, resumed.Player.PlayerId, "The stable player identity should survive reconnect.");
    AssertEqual("session-host-reconnected", resumed.Player.ConnectionId, "The resumed player should be bound to the new SignalR connection.");
    Assert(lobby.GetPlayerForConnection("session-host") is null, "The prior connection should no longer control the session.");
    Assert(lobby.IsStaleConnection("session-host"), "The prior connection should be marked stale after a session replacement.");
    Assert(room.RoomStateVersion > versionBeforeResume, "A resume should advance the public room state version.");
    AssertEqual(guest.Player.PlayerId, room.Players.Single(player => player.PlayerName == "Bea").PlayerId, "Other room members should remain unchanged.");

    var publicPayload = JsonSerializer.Serialize(room.ToDto());
    Assert(!publicPayload.Contains(host.ReconnectToken, StringComparison.Ordinal), "Reconnect tokens must never be included in public room DTOs.");
}

static void ActiveBrowserSessionRecovery()
{
    var lobby = new LobbyService();
    var original = lobby.CreateRoomSession("active-browser", "Ari");

    var recovered = lobby.RecoverCurrentSession("active-browser");
    AssertEqual(original.Player.PlayerId, recovered.Player.PlayerId, "Recovering an active browser session must keep the stable player identity.");
    Assert(!string.Equals(original.ReconnectToken, recovered.ReconnectToken, StringComparison.Ordinal), "Recovery should rotate the missing reconnect token.");

    lobby.Disconnect("active-browser");
    ExpectLobbyError(
        () => lobby.ResumeRoomSession("old-token-browser", original.Room.RoomCode, original.Player.PlayerId, original.ReconnectToken),
        "restored");

    var resumed = lobby.ResumeRoomSession("recovered-browser", original.Room.RoomCode, original.Player.PlayerId, recovered.ReconnectToken);
    AssertEqual(original.Player.PlayerId, resumed.Player.PlayerId, "The reissued token should restore the original player after disconnect.");
}

static void LobbyReadinessAndPlayerCount()
{
    var developmentLobby = new LobbyService();
    var host = developmentLobby.CreateRoomSession("ready-host", "Ari");
    var guest = developmentLobby.JoinRoomSession("ready-guest", host.Room.RoomCode, "Bea");

    ExpectLobbyError(() => developmentLobby.StartGame("ready-host", host.Room.RoomCode), "Waiting for all players to be ready");
    developmentLobby.SetReady("ready-host", host.Room.RoomCode, true);
    ExpectLobbyError(() => developmentLobby.StartGame("ready-host", host.Room.RoomCode), "Waiting for all players to be ready");
    developmentLobby.SetReady("ready-guest", host.Room.RoomCode, true);
    var startedRoom = developmentLobby.StartGame("ready-host", host.Room.RoomCode);
    AssertEqual(RoomStatus.InGame, startedRoom.Status, "All ready players should allow the host to start the match.");
    Assert(startedRoom.Players.All(player => !player.IsReady), "Starting a match should reset ready state.");

    var options = new RoomLifecycleOptions { MinimumPlayers = 3 };
    var productionLobby = new LobbyService(options, new TestHostEnvironment("Production"));
    var productionHost = productionLobby.CreateRoomSession("production-host", "Ari");
    var productionGuest = productionLobby.JoinRoomSession("production-guest", productionHost.Room.RoomCode, "Bea");
    productionLobby.SetReady("production-host", productionHost.Room.RoomCode, true);
    productionLobby.SetReady("production-guest", productionHost.Room.RoomCode, true);
    ExpectLobbyError(() => productionLobby.StartGame("production-host", productionHost.Room.RoomCode), "requires 3-4 players");
}

static void RequiredPlayerReconnectPause()
{
    var (lobby, service, lifecycle, room, host, guest, game) = CreateLifecycleGame();
    var requiredPlayer = game.CurrentPlayer;
    var requiredSession = requiredPlayer.PlayerId == host.Player.PlayerId ? host : guest;
    var requiredConnectionId = requiredPlayer.PlayerId == host.Player.PlayerId
        ? "lifecycle-host"
        : "lifecycle-guest";
    var versionBeforeDisconnect = game.GameStateVersion;

    lifecycle.Disconnect(requiredConnectionId);
    var pausedGame = service.GetGame(room.RoomCode)!;
    var disconnectedPlayer = lobby.GetRoom(room.RoomCode)!.Players.Single(player => player.PlayerId == requiredPlayer.PlayerId);

    AssertEqual(PlayerConnectionStatus.Reconnecting, disconnectedPlayer.ConnectionStatus, "Disconnect should keep the player in the room during the reconnect window.");
    Assert(pausedGame.Pause?.IsPaused == true, "The match should pause when the player required to act disconnects.");
    AssertEqual(requiredPlayer.PlayerId, pausedGame.Pause!.DisconnectedPlayerId, "The pause should identify the player who must reconnect.");
    Assert(pausedGame.GameStateVersion > versionBeforeDisconnect, "Lifecycle changes should advance the game state version.");
    ExpectRuleError(
        () => service.PlaceSetupCamp(room.RoomCode, requiredPlayer.PlayerId, pausedGame.Board.Vertices.First().VertexId),
        "paused while a player reconnects");

    lifecycle.ResumeRoomSession("lifecycle-required-reconnected", room.RoomCode, requiredPlayer.PlayerId, requiredSession.ReconnectToken);
    var resumedGame = service.GetGame(room.RoomCode)!;
    Assert(resumedGame.Pause is null, "Resuming the required player should continue the match.");
    AssertEqual(PlayerConnectionStatus.Connected, resumedGame.Players.Single(player => player.PlayerId == requiredPlayer.PlayerId).ConnectionStatus, "The game state should expose the restored connection state.");
}

static void NonRequiredDisconnectDoesNotPause()
{
    var (lobby, service, lifecycle, room, host, guest, game) = CreateLifecycleGame();
    var nonRequiredSession = game.CurrentSetupPlayerId == host.Player.PlayerId ? guest : host;
    var nonRequiredConnectionId = nonRequiredSession.Player.PlayerId == host.Player.PlayerId
        ? "lifecycle-host"
        : "lifecycle-guest";
    lifecycle.Disconnect(nonRequiredConnectionId);

    var roomPlayer = lobby.GetRoom(room.RoomCode)!.Players.Single(player => player.PlayerId == nonRequiredSession.Player.PlayerId);
    AssertEqual(PlayerConnectionStatus.Reconnecting, roomPlayer.ConnectionStatus, "The optional player should enter the reconnect window.");
    Assert(service.GetGame(room.RoomCode)!.Pause is null, "A non-required player's disconnect should not pause the current setup action.");
}

static void HostMigrationAfterReconnectGrace()
{
    var options = new RoomLifecycleOptions { ReconnectGracePeriod = TimeSpan.FromSeconds(1) };
    var environment = new TestHostEnvironment("Development");
    var lobby = new LobbyService(options, environment);
    var service = new GameService(new BoardGenerator());
    var lifecycle = new RoomLifecycleService(lobby, service, options, environment);
    var host = lobby.CreateRoomSession("migration-host", "Ari");
    var guest = lobby.JoinRoomSession("migration-guest", host.Room.RoomCode, "Bea");
    lobby.SetReady("migration-host", host.Room.RoomCode, true);
    lobby.SetReady("migration-guest", host.Room.RoomCode, true);
    var room = lobby.StartGame("migration-host", host.Room.RoomCode);
    var game = service.StartGame(room);

    lifecycle.Disconnect("migration-host");
    AssertEqual(host.Player.PlayerId, lobby.GetRoom(room.RoomCode)!.HostPlayerId, "Host ownership should remain stable during the grace period.");

    lobby.ProcessLifecycle(DateTimeOffset.UtcNow + options.ReconnectGracePeriod + TimeSpan.FromMilliseconds(5));
    room = lobby.GetRoom(room.RoomCode)!;
    service.SyncRoomPlayerMetadata(room);

    AssertEqual(guest.Player.PlayerId, room.HostPlayerId, "The next connected player should become host after the grace period expires.");
    AssertEqual(PlayerConnectionStatus.TimedOut, room.Players.Single(player => player.PlayerId == host.Player.PlayerId).ConnectionStatus, "The previous host should be marked timed out.");
    Assert(game.Players.Single(player => player.PlayerId == guest.Player.PlayerId).IsHost, "Host migration should synchronize into game state.");
}

static void ForfeitLifecycle()
{
    var (lobby, service, lifecycle, room, host, guest, game) = CreateLifecycleGame();
    var preservedVertex = game.Board.Vertices.First();
    preservedVertex.OwnerPlayerId = host.Player.PlayerId;
    preservedVertex.StructureType = BoardStructureType.Camp;
    var forfeitingPlayer = game.Players.Single(player => player.PlayerId == host.Player.PlayerId);
    var remainingPlayer = game.Players.Single(player => player.PlayerId == guest.Player.PlayerId);
    forfeitingPlayer.Supplies[ResourceType.Wood] = 2;
    forfeitingPlayer.PlayedKnightCount = 3;
    remainingPlayer.PlayedKnightCount = 4;
    game.LargestArmyPlayerId = forfeitingPlayer.PlayerId;
    game.LargestArmyKnightCount = forfeitingPlayer.PlayedKnightCount;

    lifecycle.ForfeitMatch("lifecycle-host", room.RoomCode);
    var updatedGame = service.GetGame(room.RoomCode)!;
    var updatedRoom = lobby.GetRoom(room.RoomCode)!;
    var forfeitedPlayer = updatedGame.Players.Single(player => player.PlayerId == host.Player.PlayerId);

    Assert(forfeitedPlayer.HasForfeited, "Forfeit should mark the player inactive in the game state.");
    AssertEqual(PlayerConnectionStatus.Forfeited, forfeitedPlayer.ConnectionStatus, "Forfeit should expose the final connection state.");
    AssertEqual(0, forfeitedPlayer.Supplies.Values.Sum(), "Forfeit should remove the player's unspent supplies from circulation.");
    Assert(!updatedGame.PlayerOrder.Contains(host.Player.PlayerId), "A forfeited player should no longer receive setup or turn actions.");
    AssertEqual(host.Player.PlayerId, preservedVertex.OwnerPlayerId, "Placed pieces should remain on the board as neutral abandoned pieces.");
    AssertEqual(guest.Player.PlayerId, updatedRoom.HostPlayerId, "Forfeiting host ownership should transfer to the next eligible player.");
    Assert(updatedGame.Players.Single(player => player.PlayerId == guest.Player.PlayerId).IsHost, "The new host should be reflected in game state.");
    AssertEqual(guest.Player.PlayerId, updatedGame.LargestArmyPlayerId, "Forfeit should recalculate Largest Army for remaining active players.");
}

static void PostGameReturnAndRematch()
{
    var (lobby, service, lifecycle, room, host, guest, game) = CreateLifecycleGame();
    var firstMatchId = game.MatchId;
    ExpectLobbyError(() => lifecycle.ReturnToLobby("lifecycle-host", room.RoomCode), "finished before returning");
    service.FinishAbandonedMatch(room.RoomCode, "Lifecycle test complete.", host.Player.PlayerId);
    lifecycle.MarkPostGame(game);

    var result = lifecycle.ReturnToLobby("lifecycle-host", room.RoomCode);
    var waitingRoom = result.Room!;
    AssertEqual(RoomStatus.Waiting, waitingRoom.Status, "Returning after a match should restore the room lobby.");
    Assert(service.GetGame(room.RoomCode) is null, "Returning to the lobby should remove the completed in-memory match state.");
    AssertEqual(2, waitingRoom.Players.Count, "Connected non-forfeited players should remain together for a rematch.");
    Assert(waitingRoom.Players.All(player => !player.IsReady), "A returned lobby should require fresh readiness confirmation.");

    lobby.SetReady("lifecycle-host", room.RoomCode, true);
    lobby.SetReady("lifecycle-guest", room.RoomCode, true);
    var rematchRoom = lobby.StartGame("lifecycle-host", room.RoomCode);
    var rematch = service.StartGame(rematchRoom);
    Assert(firstMatchId != rematch.MatchId, "A rematch should create a fresh match identity.");
}

static (LobbyService Lobby, GameService Service, GameState Game) CreateGame()
{
    var lobby = new LobbyService();
    var boardGenerator = new BoardGenerator();
    var service = new GameService(boardGenerator);
    var room = lobby.CreateRoom("connection-1", "Ari");
    room = lobby.JoinRoom("connection-2", room.RoomCode, "Bea");
    lobby.SetReady("connection-1", room.RoomCode, true);
    lobby.SetReady("connection-2", room.RoomCode, true);
    room = lobby.StartGame("connection-1", room.RoomCode);
    var game = service.StartGame(room);
    return (lobby, service, game);
}

static (LobbyService Lobby, GameService Service, RoomLifecycleService Lifecycle, Room Room, PlayerSessionResult Host, PlayerSessionResult Guest, GameState Game) CreateLifecycleGame()
{
    var options = new RoomLifecycleOptions { ReconnectGracePeriod = TimeSpan.FromMinutes(2) };
    var environment = new TestHostEnvironment("Development");
    var lobby = new LobbyService(options, environment);
    var service = new GameService(new BoardGenerator());
    var lifecycle = new RoomLifecycleService(lobby, service, options, environment);
    var host = lobby.CreateRoomSession("lifecycle-host", "Ari");
    var guest = lobby.JoinRoomSession("lifecycle-guest", host.Room.RoomCode, "Bea");
    lobby.SetReady("lifecycle-host", host.Room.RoomCode, true);
    lobby.SetReady("lifecycle-guest", host.Room.RoomCode, true);
    var room = lobby.StartGame("lifecycle-host", host.Room.RoomCode);
    var game = service.StartGame(room);
    return (lobby, service, lifecycle, room, host, guest, game);
}

static void DebugBoardInspector()
{
    const int seed = 712_903;
    var (lobby, service, _) = CreateGame();
    var room = lobby.GetRoomForConnection("connection-1")!;
    var host = room.Players.Single(player => player.IsHost);
    var debug = new DebugGameService(service, new TestHostEnvironment("Development"));

    var initialValidation = debug.ValidateBoard(room, host);
    Assert(initialValidation.IsValid, $"The active board should validate in Debug Mode: {string.Join(" | ", initialValidation.Errors)}");

    var movedWardenTile = service.GetGame(room.RoomCode)!.Board.Tiles
        .First(tile => tile.TileId != service.GetGame(room.RoomCode)!.WardenTileId);
    service.MoveWardenForDebug(room.RoomCode, host.PlayerId, movedWardenTile.TileId);
    var movedWardenValidation = debug.ValidateBoard(room, host);
    Assert(movedWardenValidation.IsValid, "The live board inspector should accept a Warden that moved after match start.");

    var regenerated = debug.RegenerateBoard(room, host, seed);
    AssertEqual(seed, regenerated.Board.BoardSeed, "Debug board regeneration should use the requested seed.");
    var regeneratedValidation = debug.ValidateBoard(room, host);
    Assert(regeneratedValidation.IsValid, $"The regenerated board should validate in Debug Mode: {string.Join(" | ", regeneratedValidation.Errors)}");
    AssertSequence(
        BoardSnapshot(new BoardGenerator().Generate(seed)),
        BoardSnapshot(regenerated.Board),
        "Debug seed regeneration should reproduce the complete board topology and assignments.");
}

static void CompleteSetup(GameService service, GameState game)
{
    while (game.Phase == GamePhase.Setup)
    {
        var player = game.CurrentPlayer;
        if (game.SetupStep == SetupStep.PlaceCamp)
        {
            var camp = FindValidSetupVertex(game, preferProduction: game.SetupRound == SetupRound.SecondPlacement);
            service.PlaceSetupCamp(game.RoomCode, player.PlayerId, camp.VertexId);
        }
        else
        {
            service.PlaceSetupTrail(game.RoomCode, player.PlayerId, FindSetupTrail(game, game.LastSetupCampVertexId!).EdgeId);
        }
    }
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

static PlayerDevelopmentCard PlayKnightAndResolveWarden(GameService service, GameState game, PlayerGameState player)
{
    var playerIndex = game.Players.FindIndex(candidate =>
        string.Equals(candidate.PlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase));
    Assert(playerIndex >= 0, "Test player should exist in the game.");

    game.Phase = GamePhase.NormalTurn;
    game.CurrentPlayerIndex = playerIndex;
    game.HasRolledThisTurn = true;
    game.LastDiceRoll ??= 8;
    game.PendingWardenAction = WardenAction.None;
    game.CurrentWardenPlayerId = null;
    game.PendingWardenDiscards.Clear();
    game.WardenVictimOptions.Clear();
    player.HasPlayedDevelopmentCardThisTurn = false;

    var knight = AddCard(player, DevelopmentCardType.Knight, Math.Max(0, game.TurnNumber - 1));
    service.PlayKnight(game.RoomCode, player.PlayerId, knight.CardId, "", null);

    if (game.Status != GameStatus.Finished && game.PendingWardenAction == WardenAction.MoveWarden)
    {
        var targetTile = game.Board.Tiles.First(tile => tile.TileId != game.WardenTileId);
        service.MoveWarden(game.RoomCode, player.PlayerId, targetTile.TileId);
    }

    return knight;
}

static (GameState Game, PlayerGameState Current, PlayerGameState Opponent) CreateTrailScoringGame()
{
    var game = new GameState
    {
        RoomCode = $"trail-{Guid.NewGuid():N}",
        Phase = GamePhase.NormalTurn,
        Status = GameStatus.InProgress,
        CurrentPlayerIndex = 0,
        HasRolledThisTurn = true
    };
    var current = new PlayerGameState
    {
        PlayerId = "player-a",
        PlayerName = "Alex",
        IsHost = true
    };
    var opponent = new PlayerGameState
    {
        PlayerId = "player-b",
        PlayerName = "Maria"
    };

    game.Players.Add(current);
    game.Players.Add(opponent);
    game.PlayerOrder.Add(current.PlayerId);
    game.PlayerOrder.Add(opponent.PlayerId);
    return (game, current, opponent);
}

static void AddTrailPath(GameState game, PlayerGameState player, string prefix, int edgeCount)
{
    for (var index = 0; index <= edgeCount; index++)
    {
        AddTrailVertex(game, $"{prefix}-v{index}");
    }

    for (var index = 0; index < edgeCount; index++)
    {
        AddTrailEdge(game, $"{prefix}-e{index}", $"{prefix}-v{index}", $"{prefix}-v{index + 1}", player.PlayerId);
    }

    player.TrailsBuilt += edgeCount;
}

static void AddTrailVertex(GameState game, string vertexId)
{
    if (game.Board.Vertices.Any(vertex => vertex.VertexId == vertexId))
    {
        return;
    }

    game.Board.Vertices.Add(new BoardVertex
    {
        VertexId = vertexId
    });
}

static BoardEdge AddTrailEdge(GameState game, string edgeId, string startVertexId, string endVertexId, string? ownerPlayerId = null)
{
    AddTrailVertex(game, startVertexId);
    AddTrailVertex(game, endVertexId);
    var edge = new BoardEdge
    {
        EdgeId = edgeId,
        StartVertexId = startVertexId,
        EndVertexId = endVertexId,
        OwnerPlayerId = ownerPlayerId
    };
    game.Board.Edges.Add(edge);
    return edge;
}

static void ClearTrailGraphOwnership(GameState game)
{
    foreach (var edge in game.Board.Edges)
    {
        edge.OwnerPlayerId = null;
    }

    foreach (var vertex in game.Board.Vertices)
    {
        vertex.OwnerPlayerId = null;
        vertex.StructureType = null;
    }

    foreach (var player in game.Players)
    {
        player.TrailsBuilt = 0;
        player.LongestTrailLength = 0;
    }

    game.LongestTrailPlayerId = null;
    game.LongestTrailLength = 0;
}

static void ClearPlayerTrail(GameState game, string playerId)
{
    foreach (var edge in game.Board.Edges.Where(edge => edge.OwnerPlayerId == playerId))
    {
        edge.OwnerPlayerId = null;
    }

    var player = game.Players.Single(candidate => candidate.PlayerId == playerId);
    player.TrailsBuilt = 0;
    player.LongestTrailLength = 0;
}

static IReadOnlyList<BoardEdge> FindConnectedEdgePath(GameState game, int length)
{
    foreach (var edge in game.Board.Edges)
    {
        var path = FindConnectedEdgePathFrom(game, edge, length);
        if (path.Count == length)
        {
            return path;
        }
    }

    throw new InvalidOperationException($"Could not find a connected path of {length} edges.");
}

static IReadOnlyList<BoardEdge> FindConnectedEdgePathFrom(GameState game, BoardEdge firstEdge, int length)
{
    var path = new List<BoardEdge> { firstEdge };
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { firstEdge.EdgeId };

    if (ExtendConnectedPath(game, firstEdge.EndVertexId, length, path, used)
        || ExtendConnectedPath(game, firstEdge.StartVertexId, length, path, used))
    {
        return path;
    }

    return [];
}

static bool ExtendConnectedPath(
    GameState game,
    string vertexId,
    int targetLength,
    List<BoardEdge> path,
    ISet<string> usedEdgeIds)
{
    if (path.Count == targetLength)
    {
        return true;
    }

    foreach (var edge in game.Board.Edges.Where(edge => EdgeTouches(edge, vertexId)).OrderBy(edge => edge.EdgeId))
    {
        if (usedEdgeIds.Contains(edge.EdgeId))
        {
            continue;
        }

        path.Add(edge);
        usedEdgeIds.Add(edge.EdgeId);
        var nextVertexId = edge.StartVertexId == vertexId ? edge.EndVertexId : edge.StartVertexId;
        if (ExtendConnectedPath(game, nextVertexId, targetLength, path, usedEdgeIds))
        {
            return true;
        }

        usedEdgeIds.Remove(edge.EdgeId);
        path.RemoveAt(path.Count - 1);
    }

    return false;
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
        player.LongestTrailLength = 0;
    }

    game.LongestTrailPlayerId = null;
    game.LongestTrailLength = 0;
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

static IReadOnlyDictionary<ResourceType, int> Supplies(params (ResourceType Resource, int Amount)[] entries)
{
    return entries.ToDictionary(entry => entry.Resource, entry => entry.Amount);
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

static (BoardVertex Vertex, BoardEdge Edge) FindBuildableTrailSeed(GameState game)
{
    foreach (var vertex in game.Board.Vertices.OrderBy(vertex => vertex.VertexId))
    {
        var edge = game.Board.Edges
            .Where(edge => edge.OwnerPlayerId is null && EdgeTouches(edge, vertex.VertexId))
            .OrderBy(edge => edge.EdgeId)
            .FirstOrDefault();

        if (edge is not null)
        {
            return (vertex, edge);
        }
    }

    throw new InvalidOperationException("Could not find a buildable Trail seed.");
}

static (BoardVertex Vertex, BoardEdge ExistingEdge, BoardEdge TargetEdge) FindTrailConnector(GameState game)
{
    foreach (var vertex in game.Board.Vertices.OrderBy(vertex => vertex.VertexId))
    {
        var edges = game.Board.Edges
            .Where(edge => edge.OwnerPlayerId is null && EdgeTouches(edge, vertex.VertexId))
            .OrderBy(edge => edge.EdgeId)
            .Take(2)
            .ToList();

        if (edges.Count >= 2)
        {
            return (vertex, edges[0], edges[1]);
        }
    }

    throw new InvalidOperationException("Could not find a Trail connector with two open edges.");
}

static string OtherVertexId(BoardEdge edge, string vertexId)
{
    return string.Equals(edge.StartVertexId, vertexId, StringComparison.OrdinalIgnoreCase)
        ? edge.EndVertexId
        : edge.StartVertexId;
}

static void GiveBuildSupplies(PlayerGameState player)
{
    foreach (var resource in ResourceTypes.All)
    {
        player.Supplies[resource] = 10;
    }
}

static void IncrementStructureCount(PlayerGameState player, BoardStructureType structureType)
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

static (BoardEdge Edge, BoardVertex TargetVertex) FindCampExtensionEdge(GameState game, string playerId)
{
    foreach (var ownedEdge in game.Board.Edges.Where(edge => edge.OwnerPlayerId == playerId).OrderBy(edge => edge.EdgeId))
    {
        foreach (var fromVertexId in new[] { ownedEdge.StartVertexId, ownedEdge.EndVertexId })
        {
            var extension = game.Board.Edges
                .Where(edge => edge.OwnerPlayerId is null && EdgeTouches(edge, fromVertexId))
                .OrderBy(edge => edge.EdgeId)
                .FirstOrDefault(edge =>
                {
                    var targetVertexId = edge.StartVertexId == fromVertexId ? edge.EndVertexId : edge.StartVertexId;
                    var target = game.Board.Vertices.Single(vertex => vertex.VertexId == targetVertexId);
                    return target.OwnerPlayerId is null
                        && target.StructureType is null
                        && !HasAdjacentOwnedStructureForTest(game, target.VertexId);
                });

            if (extension is not null)
            {
                var targetVertexId = extension.StartVertexId == fromVertexId ? extension.EndVertexId : extension.StartVertexId;
                return (extension, game.Board.Vertices.Single(vertex => vertex.VertexId == targetVertexId));
            }
        }
    }

    throw new InvalidOperationException("Could not find an extension edge that creates a buildable Camp target.");
}

static bool HasAdjacentOwnedStructureForTest(GameState game, string vertexId)
{
    return game.Board.Edges
        .Where(edge => EdgeTouches(edge, vertexId))
        .Select(edge => edge.StartVertexId == vertexId ? edge.EndVertexId : edge.StartVertexId)
        .Select(otherVertexId => game.Board.Vertices.First(otherVertex => otherVertex.VertexId == otherVertexId))
        .Any(otherVertex => otherVertex.OwnerPlayerId is not null && otherVertex.StructureType is not null);
}

static void AssertSuppliesEqual(IReadOnlyDictionary<ResourceType, int> expected, PlayerGameState player, string message)
{
    foreach (var resource in ResourceTypes.All)
    {
        AssertEqual(expected[resource], player.Supplies[resource], $"{message} {resource} changed unexpectedly.");
    }
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

static void ExpectLobbyError(Action action, string expectedMessagePart)
{
    try
    {
        action();
    }
    catch (LobbyException ex) when (ex.Message.Contains(expectedMessagePart, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    throw new InvalidOperationException($"Expected lobby error containing '{expectedMessagePart}'.");
}

static GameRuleException CaptureRuleError(Action action)
{
    try
    {
        action();
    }
    catch (GameRuleException ex)
    {
        return ex;
    }

    throw new InvalidOperationException("Expected a game rule error.");
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
