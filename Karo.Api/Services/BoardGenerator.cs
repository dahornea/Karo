using System.Security.Cryptography;
using Karo.Api.Models;

namespace Karo.Api.Services;

public sealed class BoardGenerator
{
    private static readonly int[] HarborSlotBoundaryEdgeIndexes = [0, 3, 7, 10, 13, 17, 20, 23, 27];

    private readonly BoardIntegrityValidator _validator;

    public BoardGenerator(BoardIntegrityValidator? validator = null)
    {
        _validator = validator ?? new BoardIntegrityValidator();
    }

    public BoardState Generate()
    {
        return Generate(RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue));
    }

    public BoardState Generate(int boardSeed)
    {
        var random = new Random(boardSeed);
        var board = new BoardState { BoardSeed = boardSeed };
        var resources = CreateResourceBag();
        Shuffle(resources, random);

        var resourceIndex = 0;
        var tileIndex = 1;
        foreach (var (q, r) in BoardGeometry.CreateAxialCoordinates())
        {
            board.Tiles.Add(new HexTile
            {
                TileId = $"tile-{tileIndex++:00}",
                Q = q,
                R = r,
                ResourceType = resources[resourceIndex++],
                NumberToken = null,
                IsBlocked = false
            });
        }

        BuildTileAdjacency(board);
        AssignNumberTokens(board, random, boardSeed);
        BuildVerticesEdgesAndHarbors(board, random);

        var validation = _validator.Validate(board);
        if (!validation.IsValid)
        {
            throw new BoardGenerationException(boardSeed, validation.Errors);
        }

        return board;
    }

    public static BoardValidationResult Validate(
        BoardState board,
        string? wardenTileId = null,
        bool requireWardenOnDesert = false)
    {
        return new BoardIntegrityValidator().Validate(board, wardenTileId, requireWardenOnDesert);
    }

    public static void ValidateHarborSlots(BoardState board)
    {
        ThrowIfInvalid(board, "harbor slot validation");
    }

    public static void ValidatePorts(BoardState board)
    {
        ThrowIfInvalid(board, "port validation");
    }

    private static void ThrowIfInvalid(BoardState board, string context)
    {
        var validation = Validate(board);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Board {context} failed: {string.Join(" | ", validation.Errors)}");
        }
    }

    private static void BuildTileAdjacency(BoardState board)
    {
        var tilesByCoordinate = board.Tiles.ToDictionary(tile => (tile.Q, tile.R));
        foreach (var tile in board.Tiles)
        {
            foreach (var direction in BoardGeometry.NeighborDirections)
            {
                if (tilesByCoordinate.TryGetValue((tile.Q + direction.Q, tile.R + direction.R), out var adjacent))
                {
                    tile.AdjacentTileIds.Add(adjacent.TileId);
                }
            }
        }
    }

    private static void AssignNumberTokens(BoardState board, Random random, int boardSeed)
    {
        var productiveTiles = board.Tiles
            .Where(tile => tile.ResourceType != TileResourceType.None)
            .OrderBy(tile => tile.R)
            .ThenBy(tile => tile.Q)
            .ToList();
        var availableTiles = productiveTiles.ToList();
        Shuffle(availableTiles, random);
        var placedHighProbabilityTiles = new List<HexTile>();
        var highProbabilityTokens = new[] { 6, 6, 8, 8 };

        if (!PlaceHighProbabilityTokens(0, highProbabilityTokens, availableTiles, placedHighProbabilityTiles, random))
        {
            throw new BoardGenerationException(boardSeed, ["Unable to place non-adjacent 6 and 8 number tokens."]);
        }

        var remainingNumbers = CreateNumberBag()
            .Where(number => number is not 6 and not 8)
            .ToList();
        Shuffle(remainingNumbers, random);

        if (availableTiles.Count != remainingNumbers.Count)
        {
            throw new BoardGenerationException(boardSeed, ["Number token assignment did not leave the expected productive tile count."]);
        }

        for (var index = 0; index < availableTiles.Count; index++)
        {
            availableTiles[index].NumberToken = remainingNumbers[index];
        }
    }

    private static bool PlaceHighProbabilityTokens(
        int tokenIndex,
        IReadOnlyList<int> highProbabilityTokens,
        List<HexTile> availableTiles,
        List<HexTile> placedTiles,
        Random random)
    {
        if (tokenIndex == highProbabilityTokens.Count)
        {
            return true;
        }

        var candidates = availableTiles
            .Where(candidate => placedTiles.All(placed => !placed.AdjacentTileIds.Contains(candidate.TileId, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        Shuffle(candidates, random);

        foreach (var candidate in candidates)
        {
            candidate.NumberToken = highProbabilityTokens[tokenIndex];
            availableTiles.Remove(candidate);
            placedTiles.Add(candidate);

            if (PlaceHighProbabilityTokens(tokenIndex + 1, highProbabilityTokens, availableTiles, placedTiles, random))
            {
                return true;
            }

            placedTiles.Remove(candidate);
            availableTiles.Add(candidate);
            candidate.NumberToken = null;
        }

        return false;
    }

    private static void BuildVerticesEdgesAndHarbors(BoardState board, Random random)
    {
        var verticesByPosition = new Dictionary<string, BoardVertex>(StringComparer.OrdinalIgnoreCase);
        var vertexIdsByTileCorner = new Dictionary<(int Q, int R, int Corner), string>();

        foreach (var tile in board.Tiles)
        {
            var center = BoardGeometry.AxialToPixel(tile.Q, tile.R);
            for (var corner = 0; corner < 6; corner++)
            {
                var point = BoardGeometry.HexCorner(center.X, center.Y, corner);
                var key = BoardGeometry.PointKey(point.X, point.Y);
                if (!verticesByPosition.TryGetValue(key, out var vertex))
                {
                    vertex = new BoardVertex
                    {
                        VertexId = $"vertex-{verticesByPosition.Count + 1:00}",
                        X = Math.Round(point.X, 3),
                        Y = Math.Round(point.Y, 3)
                    };
                    verticesByPosition[key] = vertex;
                    board.Vertices.Add(vertex);
                }

                vertexIdsByTileCorner[(tile.Q, tile.R, corner)] = vertex.VertexId;
                vertex.AdjacentTileIds.Add(tile.TileId);
            }
        }

        var perimeterEdges = BuildEdges(board, vertexIdsByTileCorner);
        PopulateVertexConnectivity(board);
        BuildHarborSlotsAndPorts(board, perimeterEdges, random);
    }

    private static IReadOnlyList<BoundaryEdge> BuildEdges(
        BoardState board,
        IReadOnlyDictionary<(int Q, int R, int Corner), string> vertexIdsByTileCorner)
    {
        var tilesByCoordinate = board.Tiles.ToDictionary(tile => (tile.Q, tile.R));
        var edgesByPair = new Dictionary<string, BoardEdge>(StringComparer.OrdinalIgnoreCase);
        var perimeterEdges = new List<BoundaryEdge>();
        var nextInteriorEdgeId = 1;

        foreach (var tile in board.Tiles)
        {
            var center = BoardGeometry.AxialToPixel(tile.Q, tile.R);
            for (var edgeIndex = 0; edgeIndex < BoardGeometry.NeighborDirections.Length; edgeIndex++)
            {
                var startCorner = (5 - edgeIndex + 6) % 6;
                var endCorner = (startCorner + 1) % 6;
                var startVertexId = vertexIdsByTileCorner[(tile.Q, tile.R, startCorner)];
                var endVertexId = vertexIdsByTileCorner[(tile.Q, tile.R, endCorner)];
                var edgeKey = BoardGeometry.VertexPairKey(startVertexId, endVertexId);

                if (!edgesByPair.TryGetValue(edgeKey, out var edge))
                {
                    var direction = BoardGeometry.NeighborDirections[edgeIndex];
                    var isCoastal = !tilesByCoordinate.ContainsKey((tile.Q + direction.Q, tile.R + direction.R));
                    edge = new BoardEdge
                    {
                        EdgeId = isCoastal
                            ? CoastalEdgeId(tile.Q, tile.R, edgeIndex)
                            : $"edge-{nextInteriorEdgeId++:00}",
                        StartVertexId = startVertexId,
                        EndVertexId = endVertexId
                    };
                    edgesByPair[edgeKey] = edge;
                    board.Edges.Add(edge);

                    if (isCoastal)
                    {
                        var start = BoardGeometry.HexCorner(center.X, center.Y, startCorner);
                        var end = BoardGeometry.HexCorner(center.X, center.Y, endCorner);
                        var midpoint = ((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                        var outward = Normalize(midpoint.Item1 - center.X, midpoint.Item2 - center.Y);
                        perimeterEdges.Add(new BoundaryEdge(
                            edge.EdgeId,
                            tile.Q,
                            tile.R,
                            edgeIndex,
                            startVertexId,
                            endVertexId,
                            midpoint.Item1 + outward.X * 118,
                            midpoint.Item2 + outward.Y * 118,
                            NormalizeDegrees(Math.Atan2(outward.Y, outward.X) * 180 / Math.PI),
                            Math.Atan2(midpoint.Item2, midpoint.Item1)));
                    }
                }

                edge.AdjacentTileIds.Add(tile.TileId);
            }
        }

        return perimeterEdges;
    }

    private static void PopulateVertexConnectivity(BoardState board)
    {
        var verticesById = board.Vertices.ToDictionary(vertex => vertex.VertexId, StringComparer.OrdinalIgnoreCase);
        foreach (var edge in board.Edges)
        {
            var start = verticesById[edge.StartVertexId];
            var end = verticesById[edge.EndVertexId];
            start.AdjacentEdgeIds.Add(edge.EdgeId);
            end.AdjacentEdgeIds.Add(edge.EdgeId);
            start.AdjacentVertexIds.Add(end.VertexId);
            end.AdjacentVertexIds.Add(start.VertexId);

            if (edge.AdjacentTileIds.Count == 1)
            {
                start.IsCoastal = true;
                end.IsCoastal = true;
            }
        }
    }

    private static void BuildHarborSlotsAndPorts(BoardState board, IReadOnlyList<BoundaryEdge> perimeterEdges, Random random)
    {
        if (perimeterEdges.Count != 30 || HarborSlotBoundaryEdgeIndexes.Max() >= perimeterEdges.Count)
        {
            throw new BoardGenerationException(board.BoardSeed, ["The compact land board did not produce the expected 30 coastal edges."]);
        }

        var harborEdges = perimeterEdges
            .OrderBy(edge => edge.Angle)
            .ThenBy(edge => edge.AnchorX)
            .ThenBy(edge => edge.AnchorY)
            .ToList();
        var harborTypes = CreateHarborTypeBag();
        Shuffle(harborTypes, random);

        for (var index = 0; index < HarborSlotBoundaryEdgeIndexes.Length; index++)
        {
            var edge = harborEdges[HarborSlotBoundaryEdgeIndexes[index]];
            var harborType = harborTypes[index];
            var slot = new HarborSlot
            {
                HarborSlotId = $"harbor-slot-{index + 1:00}",
                AdjacentEdgeId = edge.EdgeId,
                TileQ = edge.TileQ,
                TileR = edge.TileR,
                EdgeIndex = edge.EdgeIndex,
                RenderX = Math.Round(edge.AnchorX, 2),
                RenderY = Math.Round(edge.AnchorY, 2),
                OrientationDegrees = Math.Round(edge.OrientationDegrees, 2),
                HarborType = harborType,
                TradeRate = harborType == HarborType.Generic ? 3 : 2
            };
            slot.AdjacentVertexIds.Add(edge.StartVertexId);
            slot.AdjacentVertexIds.Add(edge.EndVertexId);
            board.HarborSlots.Add(slot);
            board.Ports.Add(new Port
            {
                Id = $"port-{index + 1:00}",
                Type = harborType == HarborType.Generic ? PortType.Generic3To1 : PortType.Specific2To1,
                ResourceType = ToResourceType(harborType),
                TileQ = edge.TileQ,
                TileR = edge.TileR,
                EdgeIndex = edge.EdgeIndex,
                AdjacentVertexIds = { edge.StartVertexId, edge.EndVertexId }
            });
        }
    }

    private static List<TileResourceType> CreateResourceBag()
    {
        return
        [
            TileResourceType.Wood, TileResourceType.Wood, TileResourceType.Wood, TileResourceType.Wood,
            TileResourceType.Clay, TileResourceType.Clay, TileResourceType.Clay,
            TileResourceType.Wool, TileResourceType.Wool, TileResourceType.Wool, TileResourceType.Wool,
            TileResourceType.Grain, TileResourceType.Grain, TileResourceType.Grain, TileResourceType.Grain,
            TileResourceType.Stone, TileResourceType.Stone, TileResourceType.Stone,
            TileResourceType.None
        ];
    }

    private static List<int> CreateNumberBag()
    {
        return [2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];
    }

    private static List<HarborType> CreateHarborTypeBag()
    {
        return
        [
            HarborType.Wood,
            HarborType.Clay,
            HarborType.Wool,
            HarborType.Grain,
            HarborType.Stone,
            HarborType.Generic,
            HarborType.Generic,
            HarborType.Generic,
            HarborType.Generic
        ];
    }

    private static ResourceType? ToResourceType(HarborType harborType)
    {
        return harborType switch
        {
            HarborType.Wood => ResourceType.Wood,
            HarborType.Clay => ResourceType.Clay,
            HarborType.Wool => ResourceType.Wool,
            HarborType.Grain => ResourceType.Grain,
            HarborType.Stone => ResourceType.Stone,
            HarborType.Generic => null,
            _ => throw new ArgumentOutOfRangeException(nameof(harborType), harborType, null)
        };
    }

    private static string CoastalEdgeId(int q, int r, int edgeIndex)
    {
        return $"coast-q{q}-r{r}-e{edgeIndex}";
    }

    private static (double X, double Y) Normalize(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length == 0 ? (0, -1) : (x / length, y / length);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private sealed record BoundaryEdge(
        string EdgeId,
        int TileQ,
        int TileR,
        int EdgeIndex,
        string StartVertexId,
        string EndVertexId,
        double AnchorX,
        double AnchorY,
        double OrientationDegrees,
        double Angle);
}
