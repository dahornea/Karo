using Karo.Api.Models;

namespace Karo.Api.Services;

public sealed class BoardGenerator
{
    private const int Radius = 2;
    private const double LayoutHexSize = 100;

    private static readonly (int Q, int R)[] NeighborDirections =
    [
        (1, 0),
        (1, -1),
        (0, -1),
        (-1, 0),
        (-1, 1),
        (0, 1)
    ];

    private static readonly int[] HarborSlotBoundaryEdgeIndexes = [0, 3, 7, 10, 13, 17, 20, 23, 27];

    public BoardState Generate()
    {
        var resources = CreateResourceBag();
        var numbers = CreateNumberBag();
        Shuffle(resources);
        Shuffle(numbers);

        var board = new BoardState();
        var resourceIndex = 0;
        var numberIndex = 0;
        var tileIndex = 1;

        foreach (var (q, r) in CreateAxialCoordinates())
        {
            var resource = resources[resourceIndex++];
            var produces = resource != TileResourceType.None;

            board.Tiles.Add(new HexTile
            {
                TileId = $"tile-{tileIndex++:00}",
                Q = q,
                R = r,
                ResourceType = resource,
                NumberToken = produces ? numbers[numberIndex++] : null,
                IsBlocked = false
            });
        }

        BuildVerticesAndHarbors(board);
        ValidateHarborSlots(board);
        ValidatePorts(board);
        return board;
    }

    public static void ValidateHarborSlots(BoardState board)
    {
        if (board.Tiles.Count != 19)
        {
            throw new InvalidOperationException("A compact Karo board must contain exactly 19 land tiles.");
        }

        if (board.HarborSlots.Count != 9)
        {
            throw new InvalidOperationException("A Karo board must contain exactly 9 harbor slots.");
        }

        var assignedHarbors = board.HarborSlots.Where(slot => slot.HarborType is not null).ToList();
        if (assignedHarbors.Count != 9)
        {
            throw new InvalidOperationException("A generated Karo board must assign all 9 harbor slots.");
        }

        var genericHarbors = assignedHarbors.Count(slot => slot.HarborType == HarborType.Generic && slot.TradeRate == 3);
        if (genericHarbors != 4)
        {
            throw new InvalidOperationException("A generated Karo board must contain exactly 4 Generic 3:1 harbors.");
        }

        foreach (var resource in ResourceTypes.All)
        {
            var harborType = ToHarborType(resource);
            var count = assignedHarbors.Count(slot => slot.HarborType == harborType && slot.TradeRate == 2);
            if (count != 1)
            {
                throw new InvalidOperationException($"A generated Karo board must contain exactly 1 {resource} 2:1 harbor.");
            }
        }

        var duplicateSlotId = board.HarborSlots
            .GroupBy(slot => slot.HarborSlotId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSlotId is not null)
        {
            throw new InvalidOperationException("Harbor slot IDs must be unique.");
        }

        var duplicateEdgeId = board.HarborSlots
            .GroupBy(slot => slot.AdjacentEdgeId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateEdgeId is not null)
        {
            throw new InvalidOperationException("A coastal edge can contain at most 1 harbor slot.");
        }

        var verticesById = board.Vertices.ToDictionary(vertex => vertex.VertexId);
        var edgesById = board.Edges.ToDictionary(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase);
        foreach (var slot in board.HarborSlots)
        {
            if (string.IsNullOrWhiteSpace(slot.AdjacentEdgeId))
            {
                throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} must reference a coastal edge.");
            }

            if (!edgesById.TryGetValue(slot.AdjacentEdgeId, out var coastalEdge))
            {
                throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} references unknown edge {slot.AdjacentEdgeId}.");
            }

            if (slot.AdjacentVertexIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2)
            {
                throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} must connect to exactly 2 coastal vertices.");
            }

            foreach (var vertexId in slot.AdjacentVertexIds)
            {
                if (!verticesById.TryGetValue(vertexId, out var vertex))
                {
                    throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} references unknown vertex {vertexId}.");
                }

                if (!vertex.IsCoastal)
                {
                    throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} references a non-coastal vertex.");
                }
            }

            var edgeVertexIds = new[] { coastalEdge.StartVertexId, coastalEdge.EndVertexId }
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!slot.AdjacentVertexIds.All(edgeVertexIds.Contains))
            {
                throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} must use the referenced edge vertices.");
            }

            if (slot.HarborType is null || slot.TradeRate is null)
            {
                throw new InvalidOperationException("Harbor slot trade assignments must be set during board generation.");
            }

            var expectedTradeRate = slot.HarborType == HarborType.Generic ? 3 : 2;
            if (slot.TradeRate != expectedTradeRate)
            {
                throw new InvalidOperationException($"Harbor slot {slot.HarborSlotId} has an invalid trade rate.");
            }
        }
    }

    public static void ValidatePorts(BoardState board)
    {
        if (board.Ports.Count != 9)
        {
            throw new InvalidOperationException("A Karo board must contain exactly 9 ports.");
        }

        var genericPorts = board.Ports.Count(port => port.Type == PortType.Generic3To1);
        if (genericPorts != 4)
        {
            throw new InvalidOperationException("A Karo board must contain exactly 4 generic 3:1 ports.");
        }

        var specificPorts = board.Ports.Where(port => port.Type == PortType.Specific2To1).ToList();
        if (specificPorts.Count != 5)
        {
            throw new InvalidOperationException("A Karo board must contain exactly 5 specific 2:1 ports.");
        }

        foreach (var resource in ResourceTypes.All)
        {
            var count = specificPorts.Count(port => port.ResourceType == resource);
            if (count != 1)
            {
                throw new InvalidOperationException($"A Karo board must contain exactly 1 {resource} 2:1 port.");
            }
        }

        var duplicateEdges = board.Ports
            .GroupBy(port => (port.TileQ, port.TileR, port.EdgeIndex))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateEdges is not null)
        {
            throw new InvalidOperationException("A Karo board cannot place more than 1 port on the same coastal edge.");
        }

        var verticesById = board.Vertices.ToDictionary(vertex => vertex.VertexId);
        foreach (var port in board.Ports)
        {
            if (port.AdjacentVertexIds.Count != 2)
            {
                throw new InvalidOperationException($"Port {port.Id} must connect to exactly 2 coastal vertices.");
            }

            foreach (var vertexId in port.AdjacentVertexIds)
            {
                if (!verticesById.TryGetValue(vertexId, out var vertex))
                {
                    throw new InvalidOperationException($"Port {port.Id} references unknown vertex {vertexId}.");
                }

                if (!vertex.IsCoastal)
                {
                    throw new InvalidOperationException($"Port {port.Id} references non-coastal vertex {vertexId}.");
                }
            }
        }
    }

    private static IReadOnlyList<(int Q, int R)> CreateAxialCoordinates()
    {
        var coordinates = new List<(int Q, int R)>();

        for (var q = -Radius; q <= Radius; q++)
        {
            for (var r = -Radius; r <= Radius; r++)
            {
                var s = -q - r;
                if (Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(s))) <= Radius)
                {
                    coordinates.Add((q, r));
                }
            }
        }

        return coordinates
            .OrderBy(item => item.R)
            .ThenBy(item => item.Q)
            .ToList();
    }

    private static void BuildVerticesAndHarbors(BoardState board)
    {
        var tileLookup = board.Tiles.ToDictionary(tile => (tile.Q, tile.R));
        var verticesByPosition = new Dictionary<string, BoardVertex>();
        var vertexIdsByTileCorner = new Dictionary<(int Q, int R, int Corner), string>();

        foreach (var tile in board.Tiles)
        {
            var center = AxialToPixel(tile.Q, tile.R);

            for (var corner = 0; corner < 6; corner++)
            {
                var point = HexCorner(center.X, center.Y, corner);
                var key = VertexPositionKey(point.X, point.Y);

                if (!verticesByPosition.TryGetValue(key, out var vertex))
                {
                    vertex = new BoardVertex
                    {
                        VertexId = $"vertex-{verticesByPosition.Count + 1:00}",
                        X = Math.Round(point.X, 2),
                        Y = Math.Round(point.Y, 2)
                    };
                    verticesByPosition[key] = vertex;
                    board.Vertices.Add(vertex);
                }

                vertexIdsByTileCorner[(tile.Q, tile.R, corner)] = vertex.VertexId;
                if (!vertex.AdjacentTileIds.Contains(tile.TileId, StringComparer.OrdinalIgnoreCase))
                {
                    vertex.AdjacentTileIds.Add(tile.TileId);
                }
            }
        }

        BuildEdges(board, tileLookup, vertexIdsByTileCorner);
        var perimeterEdges = CreateBoundaryEdges(board.Tiles, tileLookup, vertexIdsByTileCorner);

        foreach (var edge in perimeterEdges)
        {
            board.Vertices.Single(vertex => vertex.VertexId == edge.StartVertexId).IsCoastal = true;
            board.Vertices.Single(vertex => vertex.VertexId == edge.EndVertexId).IsCoastal = true;
        }

        var harborEdges = perimeterEdges
            .OrderBy(edge => edge.Angle)
            .ThenBy(edge => edge.MidX)
            .ThenBy(edge => edge.MidY)
            .ToList();
        var harborTypes = CreateHarborTypeBag();
        Shuffle(harborTypes);

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
                TradeRate = GetHarborTradeRate(harborType)
            };

            slot.AdjacentVertexIds.Add(edge.StartVertexId);
            slot.AdjacentVertexIds.Add(edge.EndVertexId);
            board.HarborSlots.Add(slot);
        }

        for (var index = 0; index < board.HarborSlots.Count; index++)
        {
            var slot = board.HarborSlots[index];
            var port = new Port
            {
                Id = $"port-{index + 1:00}",
                Type = slot.HarborType == HarborType.Generic ? PortType.Generic3To1 : PortType.Specific2To1,
                ResourceType = slot.HarborType is null ? null : ToResourceType(slot.HarborType.Value),
                TileQ = slot.TileQ,
                TileR = slot.TileR,
                EdgeIndex = slot.EdgeIndex
            };

            port.AdjacentVertexIds.AddRange(slot.AdjacentVertexIds);
            board.Ports.Add(port);
        }
    }

    private static void BuildEdges(
        BoardState board,
        IReadOnlyDictionary<(int Q, int R), HexTile> tileLookup,
        IReadOnlyDictionary<(int Q, int R, int Corner), string> vertexIdsByTileCorner)
    {
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var interiorEdgeIndex = 1;

        foreach (var tile in board.Tiles)
        {
            for (var edgeIndex = 0; edgeIndex < NeighborDirections.Length; edgeIndex++)
            {
                var startCorner = (5 - edgeIndex + 6) % 6;
                var endCorner = (startCorner + 1) % 6;
                var startVertexId = vertexIdsByTileCorner[(tile.Q, tile.R, startCorner)];
                var endVertexId = vertexIdsByTileCorner[(tile.Q, tile.R, endCorner)];
                var edgeKey = VertexPairKey(startVertexId, endVertexId);

                if (!edgeKeys.Add(edgeKey))
                {
                    continue;
                }

                var direction = NeighborDirections[edgeIndex];
                var isCoastal = !tileLookup.ContainsKey((tile.Q + direction.Q, tile.R + direction.R));

                board.Edges.Add(new BoardEdge
                {
                    EdgeId = isCoastal
                        ? CoastalEdgeId(tile.Q, tile.R, edgeIndex)
                        : $"edge-{interiorEdgeIndex++:00}",
                    StartVertexId = startVertexId,
                    EndVertexId = endVertexId
                });
            }
        }
    }

    private static IReadOnlyList<BoundaryEdge> CreateBoundaryEdges(
        IReadOnlyList<HexTile> tiles,
        IReadOnlyDictionary<(int Q, int R), HexTile> tileLookup,
        IReadOnlyDictionary<(int Q, int R, int Corner), string> vertexIdsByTileCorner)
    {
        var edges = new List<BoundaryEdge>();

        foreach (var tile in tiles)
        {
            var center = AxialToPixel(tile.Q, tile.R);

            for (var edgeIndex = 0; edgeIndex < NeighborDirections.Length; edgeIndex++)
            {
                var direction = NeighborDirections[edgeIndex];
                if (tileLookup.ContainsKey((tile.Q + direction.Q, tile.R + direction.R)))
                {
                    continue;
                }

                var startCorner = (5 - edgeIndex + 6) % 6;
                var endCorner = (startCorner + 1) % 6;
                var start = HexCorner(center.X, center.Y, startCorner);
                var end = HexCorner(center.X, center.Y, endCorner);
                var midX = (start.X + end.X) / 2;
                var midY = (start.Y + end.Y) / 2;
                var outward = Normalize(midX - center.X, midY - center.Y);

                edges.Add(new BoundaryEdge(
                    CoastalEdgeId(tile.Q, tile.R, edgeIndex),
                    tile.Q,
                    tile.R,
                    edgeIndex,
                    vertexIdsByTileCorner[(tile.Q, tile.R, startCorner)],
                    vertexIdsByTileCorner[(tile.Q, tile.R, endCorner)],
                    midX,
                    midY,
                    midX + outward.X * 118,
                    midY + outward.Y * 118,
                    NormalizeDegrees(Math.Atan2(outward.Y, outward.X) * 180 / Math.PI),
                    Math.Atan2(midY, midX)));
            }
        }

        return edges;
    }

    private static (double X, double Y) AxialToPixel(int q, int r)
    {
        return (
            LayoutHexSize * Math.Sqrt(3) * (q + r / 2d),
            LayoutHexSize * 1.5 * r);
    }

    private static (double X, double Y) HexCorner(double centerX, double centerY, int corner)
    {
        var angle = Math.PI / 180 * (30 + 60 * corner);
        return (
            centerX + LayoutHexSize * Math.Cos(angle),
            centerY + LayoutHexSize * Math.Sin(angle));
    }

    private static string VertexPositionKey(double x, double y)
    {
        return $"{Math.Round(x * 1000):0}:{Math.Round(y * 1000):0}";
    }

    private static string CoastalEdgeId(int q, int r, int edgeIndex)
    {
        return $"coast-q{q}-r{r}-e{edgeIndex}";
    }

    private static string VertexPairKey(string firstVertexId, string secondVertexId)
    {
        return string.Compare(firstVertexId, secondVertexId, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{firstVertexId}:{secondVertexId}"
            : $"{secondVertexId}:{firstVertexId}";
    }

    private static (double X, double Y) Normalize(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length == 0
            ? (0, -1)
            : (x / length, y / length);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static List<TileResourceType> CreateResourceBag()
    {
        return new List<TileResourceType>
        {
            TileResourceType.Wood,
            TileResourceType.Wood,
            TileResourceType.Wood,
            TileResourceType.Wood,
            TileResourceType.Clay,
            TileResourceType.Clay,
            TileResourceType.Clay,
            TileResourceType.Wool,
            TileResourceType.Wool,
            TileResourceType.Wool,
            TileResourceType.Wool,
            TileResourceType.Grain,
            TileResourceType.Grain,
            TileResourceType.Grain,
            TileResourceType.Grain,
            TileResourceType.Stone,
            TileResourceType.Stone,
            TileResourceType.Stone,
            TileResourceType.None
        };
    }

    private static List<int> CreateNumberBag()
    {
        return new List<int>
        {
            2,
            3,
            3,
            4,
            4,
            5,
            5,
            6,
            6,
            8,
            8,
            9,
            9,
            10,
            10,
            11,
            11,
            12
        };
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

    private static int GetHarborTradeRate(HarborType harborType)
    {
        return harborType == HarborType.Generic ? 3 : 2;
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

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
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
        double MidX,
        double MidY,
        double AnchorX,
        double AnchorY,
        double OrientationDegrees,
        double Angle);
}
