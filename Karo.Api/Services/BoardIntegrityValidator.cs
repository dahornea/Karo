using Karo.Api.Models;

namespace Karo.Api.Services;

public sealed class BoardIntegrityValidator
{
    private static readonly IReadOnlyDictionary<TileResourceType, int> ExpectedTerrain = new Dictionary<TileResourceType, int>
    {
        [TileResourceType.Wood] = 4,
        [TileResourceType.Clay] = 3,
        [TileResourceType.Wool] = 4,
        [TileResourceType.Grain] = 4,
        [TileResourceType.Stone] = 3,
        [TileResourceType.None] = 1
    };

    private static readonly IReadOnlyDictionary<int, int> ExpectedNumberTokens = new Dictionary<int, int>
    {
        [2] = 1,
        [3] = 2,
        [4] = 2,
        [5] = 2,
        [6] = 2,
        [8] = 2,
        [9] = 2,
        [10] = 2,
        [11] = 2,
        [12] = 1
    };

    public BoardValidationResult Validate(
        BoardState board,
        string? wardenTileId = null,
        bool requireWardenOnDesert = false)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var tilesById = board.Tiles
            .GroupBy(tile => tile.TileId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var tilesByCoordinate = board.Tiles
            .GroupBy(tile => (tile.Q, tile.R))
            .ToDictionary(group => group.Key, group => group.ToList());

        ValidateTiles(board, tilesById, tilesByCoordinate, errors);
        ValidateTileAdjacency(board, tilesById, tilesByCoordinate, errors);
        ValidateVerticesAndEdges(board, tilesById, errors);
        ValidateHarbors(board, errors);
        ValidatePorts(board, errors, warnings);
        ValidateWarden(board, wardenTileId, requireWardenOnDesert, errors);

        return new BoardValidationResult(board.BoardSeed, errors, warnings);
    }

    private static void ValidateTiles(
        BoardState board,
        IReadOnlyDictionary<string, HexTile> tilesById,
        IReadOnlyDictionary<(int Q, int R), List<HexTile>> tilesByCoordinate,
        List<string> errors)
    {
        if (board.Tiles.Count != 19)
        {
            errors.Add($"Expected 19 tiles, found {board.Tiles.Count}.");
        }

        if (tilesById.Count != board.Tiles.Count)
        {
            errors.Add("Tile IDs are not unique.");
        }

        if (tilesByCoordinate.Any(pair => pair.Value.Count != 1))
        {
            errors.Add("Axial tile coordinates are not unique.");
        }

        var expectedCoordinates = BoardGeometry.CreateAxialCoordinates().ToHashSet();
        var actualCoordinates = board.Tiles.Select(tile => (tile.Q, tile.R)).ToHashSet();
        if (!actualCoordinates.SetEquals(expectedCoordinates))
        {
            errors.Add("Tiles do not form the required compact radius-2 axial landmass.");
        }

        foreach (var expected in ExpectedTerrain)
        {
            var actual = board.Tiles.Count(tile => tile.ResourceType == expected.Key);
            if (actual != expected.Value)
            {
                errors.Add($"Terrain distribution mismatch for {expected.Key}: expected {expected.Value}, found {actual}.");
            }
        }

        var productiveTiles = board.Tiles.Where(tile => tile.ResourceType != TileResourceType.None).ToList();
        if (productiveTiles.Count != 18)
        {
            errors.Add($"Expected 18 productive tiles, found {productiveTiles.Count}.");
        }

        if (board.Tiles.Any(tile => tile.ResourceType == TileResourceType.None && tile.NumberToken is not null))
        {
            errors.Add("Desert tiles cannot receive a number token.");
        }

        if (productiveTiles.Any(tile => tile.NumberToken is null))
        {
            errors.Add("Every productive tile must receive exactly one number token.");
        }

        var actualNumbers = productiveTiles
            .Where(tile => tile.NumberToken is not null)
            .GroupBy(tile => tile.NumberToken!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        if (actualNumbers.Values.Sum() != 18)
        {
            errors.Add($"Expected 18 number tokens, found {actualNumbers.Values.Sum()}.");
        }

        if (actualNumbers.ContainsKey(7))
        {
            errors.Add("Number token 7 is not valid on the land board.");
        }

        foreach (var expected in ExpectedNumberTokens)
        {
            var actual = actualNumbers.GetValueOrDefault(expected.Key);
            if (actual != expected.Value)
            {
                errors.Add($"Number token distribution mismatch for {expected.Key}: expected {expected.Value}, found {actual}.");
            }
        }

        if (actualNumbers.Keys.Any(number => !ExpectedNumberTokens.ContainsKey(number)))
        {
            errors.Add("Board contains an unsupported number token.");
        }
    }

    private static void ValidateTileAdjacency(
        BoardState board,
        IReadOnlyDictionary<string, HexTile> tilesById,
        IReadOnlyDictionary<(int Q, int R), List<HexTile>> tilesByCoordinate,
        List<string> errors)
    {
        foreach (var tile in board.Tiles)
        {
            var expectedNeighborIds = BoardGeometry.NeighborDirections
                .Select(direction => tilesByCoordinate.GetValueOrDefault((tile.Q + direction.Q, tile.R + direction.R)))
                .Where(neighbors => neighbors is { Count: 1 })
                .Select(neighbors => neighbors![0].TileId)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var actualNeighborIds = tile.AdjacentTileIds
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tile.AdjacentTileIds.Count != tile.AdjacentTileIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                errors.Add($"Tile {tile.TileId} has duplicate adjacent tile IDs.");
            }

            if (tile.AdjacentTileIds.Any(id => string.Equals(id, tile.TileId, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Tile {tile.TileId} cannot list itself as adjacent.");
            }

            if (tile.AdjacentTileIds.Any(id => !tilesById.ContainsKey(id)))
            {
                errors.Add($"Tile {tile.TileId} references an unknown adjacent tile.");
            }

            if (!actualNeighborIds.SequenceEqual(expectedNeighborIds, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Tile adjacency mismatch for {tile.TileId} at ({tile.Q},{tile.R}).");
            }

            foreach (var adjacentId in tile.AdjacentTileIds)
            {
                if (tilesById.TryGetValue(adjacentId, out var adjacent)
                    && !adjacent.AdjacentTileIds.Contains(tile.TileId, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Tile adjacency is not symmetric between {tile.TileId} and {adjacentId}.");
                }
            }
        }

        var centerTiles = board.Tiles.Where(tile => tile.Q == 0 && tile.R == 0).ToList();
        if (centerTiles.Count != 1 || centerTiles[0].AdjacentTileIds.Count != 6)
        {
            errors.Add("The center tile must have exactly 6 neighbors.");
        }

        if (!IsTileGraphConnected(board.Tiles, tilesById))
        {
            errors.Add("The land tile graph is not connected.");
        }

        foreach (var tile in board.Tiles.Where(tile => tile.NumberToken is 6 or 8))
        {
            if (tile.AdjacentTileIds
                .Where(tilesById.ContainsKey)
                .Select(id => tilesById[id])
                .Any(adjacent => adjacent.NumberToken is 6 or 8))
            {
                errors.Add($"High-probability number token at {tile.TileId} touches another 6 or 8.");
            }
        }
    }

    private static void ValidateVerticesAndEdges(
        BoardState board,
        IReadOnlyDictionary<string, HexTile> tilesById,
        List<string> errors)
    {
        var expectedVertexTiles = BuildExpectedVertexTiles(board.Tiles);
        var verticesById = board.Vertices
            .GroupBy(vertex => vertex.VertexId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var verticesByPosition = board.Vertices
            .GroupBy(vertex => BoardGeometry.PointKey(vertex.X, vertex.Y))
            .ToDictionary(group => group.Key, group => group.ToList());

        if (verticesById.Count != board.Vertices.Count)
        {
            errors.Add("Vertex IDs are not unique.");
        }

        if (verticesByPosition.Any(pair => pair.Value.Count != 1))
        {
            errors.Add("Duplicate physical vertices share the same board position.");
        }

        if (board.Vertices.Count != expectedVertexTiles.Count)
        {
            errors.Add($"Expected {expectedVertexTiles.Count} board vertices, found {board.Vertices.Count}.");
        }

        foreach (var expected in expectedVertexTiles)
        {
            if (!verticesByPosition.TryGetValue(expected.Key, out var matches) || matches.Count != 1)
            {
                errors.Add($"Missing generated vertex at {expected.Key}.");
                continue;
            }

            var vertex = matches[0];
            ValidateUniqueKnownIds(vertex.AdjacentTileIds, tilesById.Keys, $"Vertex {vertex.VertexId} adjacent tile", errors);
            if (!vertex.AdjacentTileIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(expected.Value.OrderBy(id => id, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Vertex {vertex.VertexId} has incorrect touching tile IDs.");
            }

            if (vertex.AdjacentTileIds.Count is < 1 or > 3)
            {
                errors.Add($"Vertex {vertex.VertexId} must touch between 1 and 3 land tiles.");
            }
        }

        var expectedEdges = BuildExpectedEdges(board.Tiles, verticesByPosition, errors);
        var edgesById = board.Edges
            .GroupBy(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (edgesById.Count != board.Edges.Count)
        {
            errors.Add("Edge IDs are not unique.");
        }

        var edgePairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in board.Edges)
        {
            if (string.Equals(edge.StartVertexId, edge.EndVertexId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Edge {edge.EdgeId} cannot use the same endpoint twice.");
                continue;
            }

            if (!verticesById.ContainsKey(edge.StartVertexId) || !verticesById.ContainsKey(edge.EndVertexId))
            {
                errors.Add($"Edge {edge.EdgeId} references a missing vertex.");
                continue;
            }

            var pairKey = BoardGeometry.VertexPairKey(edge.StartVertexId, edge.EndVertexId);
            if (!edgePairs.Add(pairKey))
            {
                errors.Add($"Duplicate physical edge {pairKey}.");
            }

            ValidateUniqueKnownIds(edge.AdjacentTileIds, tilesById.Keys, $"Edge {edge.EdgeId} adjacent tile", errors);
            if (edge.AdjacentTileIds.Count is < 1 or > 2)
            {
                errors.Add($"Edge {edge.EdgeId} must touch one coastal or two interior tiles.");
            }

            if (!expectedEdges.TryGetValue(pairKey, out var expectedTileIds))
            {
                errors.Add($"Edge {edge.EdgeId} does not match any tile boundary.");
            }
            else if (!edge.AdjacentTileIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(expectedTileIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Edge {edge.EdgeId} has incorrect touching tile IDs.");
            }
        }

        if (board.Edges.Count != expectedEdges.Count)
        {
            errors.Add($"Expected {expectedEdges.Count} board edges, found {board.Edges.Count}.");
        }

        foreach (var vertex in board.Vertices)
        {
            var expectedEdgeIds = board.Edges
                .Where(edge => string.Equals(edge.StartVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(edge.EndVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase))
                .Select(edge => edge.EdgeId)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var expectedAdjacentVertexIds = board.Edges
                .Where(edge => string.Equals(edge.StartVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(edge.EndVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase))
                .Select(edge => string.Equals(edge.StartVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase)
                    ? edge.EndVertexId
                    : edge.StartVertexId)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ValidateUniqueKnownIds(vertex.AdjacentEdgeIds, edgesById.Keys, $"Vertex {vertex.VertexId} adjacent edge", errors);
            ValidateUniqueKnownIds(vertex.AdjacentVertexIds, verticesById.Keys, $"Vertex {vertex.VertexId} adjacent vertex", errors);
            if (vertex.AdjacentVertexIds.Contains(vertex.VertexId, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Vertex {vertex.VertexId} cannot list itself as adjacent.");
            }

            if (!vertex.AdjacentEdgeIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(expectedEdgeIds, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Vertex {vertex.VertexId} has incorrect adjacent edge IDs.");
            }

            if (!vertex.AdjacentVertexIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(expectedAdjacentVertexIds, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Vertex {vertex.VertexId} has incorrect connected node IDs.");
            }

            if (vertex.AdjacentVertexIds.Count is < 2 or > 3)
            {
                errors.Add($"Vertex {vertex.VertexId} must have degree 2 or 3, found {vertex.AdjacentVertexIds.Count}.");
            }

            var expectedCoastal = board.Edges.Any(edge => edge.AdjacentTileIds.Count == 1
                && (string.Equals(edge.StartVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(edge.EndVertexId, vertex.VertexId, StringComparison.OrdinalIgnoreCase)));
            if (vertex.IsCoastal != expectedCoastal)
            {
                errors.Add($"Vertex {vertex.VertexId} has incorrect coastal status.");
            }
        }
    }

    private static void ValidateHarbors(BoardState board, List<string> errors)
    {
        if (board.HarborSlots.Count != 9)
        {
            errors.Add($"Expected 9 harbor slots, found {board.HarborSlots.Count}.");
        }

        var verticesById = board.Vertices
            .GroupBy(vertex => vertex.VertexId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var edgesById = board.Edges
            .GroupBy(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (board.HarborSlots.Select(slot => slot.HarborSlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != board.HarborSlots.Count)
        {
            errors.Add("Harbor slot IDs are not unique.");
        }

        if (board.HarborSlots.Select(slot => slot.AdjacentEdgeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != board.HarborSlots.Count)
        {
            errors.Add("Harbor slots reuse a coastal edge.");
        }

        foreach (var slot in board.HarborSlots)
        {
            if (!edgesById.TryGetValue(slot.AdjacentEdgeId, out var edge))
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} references unknown edge {slot.AdjacentEdgeId}.");
                continue;
            }

            if (edge.AdjacentTileIds.Count != 1)
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} must attach to a coastal edge.");
            }

            if (slot.AdjacentVertexIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2
                || slot.AdjacentVertexIds.Count != 2)
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} must attach to exactly two distinct nodes.");
            }

            var edgeVertices = new[] { edge.StartVertexId, edge.EndVertexId }.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var vertexId in slot.AdjacentVertexIds)
            {
                if (!verticesById.TryGetValue(vertexId, out var vertex))
                {
                    errors.Add($"Harbor slot {slot.HarborSlotId} references unknown node {vertexId}.");
                }
                else if (!vertex.IsCoastal)
                {
                    errors.Add($"Harbor slot {slot.HarborSlotId} references interior node {vertexId}.");
                }

                if (!edgeVertices.Contains(vertexId))
                {
                    errors.Add($"Harbor slot {slot.HarborSlotId} nodes do not match its coastal edge.");
                }
            }

            if (slot.HarborType is null || slot.TradeRate is null)
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} is missing its type assignment.");
                continue;
            }

            var expectedRate = slot.HarborType == HarborType.Generic ? 3 : 2;
            if (slot.TradeRate != expectedRate)
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} has invalid trade rate {slot.TradeRate}.");
            }
        }

        if (board.HarborSlots.Count(slot => slot.HarborType == HarborType.Generic && slot.TradeRate == 3) != 4)
        {
            errors.Add("Expected four Generic 3:1 harbor slots.");
        }

        foreach (var resource in ResourceTypes.All)
        {
            var expectedType = resource switch
            {
                ResourceType.Wood => HarborType.Wood,
                ResourceType.Clay => HarborType.Clay,
                ResourceType.Wool => HarborType.Wool,
                ResourceType.Grain => HarborType.Grain,
                ResourceType.Stone => HarborType.Stone,
                _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
            };
            if (board.HarborSlots.Count(slot => slot.HarborType == expectedType && slot.TradeRate == 2) != 1)
            {
                errors.Add($"Expected one {resource} 2:1 harbor slot.");
            }
        }
    }

    private static void ValidatePorts(BoardState board, List<string> errors, List<string> warnings)
    {
        if (board.Ports.Count != board.HarborSlots.Count)
        {
            errors.Add("Port data must mirror all harbor slots.");
            return;
        }

        foreach (var slot in board.HarborSlots)
        {
            var matchingPorts = board.Ports.Where(port => port.TileQ == slot.TileQ
                && port.TileR == slot.TileR
                && port.EdgeIndex == slot.EdgeIndex)
                .ToList();
            if (matchingPorts.Count != 1)
            {
                errors.Add($"Harbor slot {slot.HarborSlotId} must have exactly one matching legacy port record.");
                continue;
            }

            var matchingPort = matchingPorts[0];

            if (!matchingPort.AdjacentVertexIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(slot.AdjacentVertexIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Port {matchingPort.Id} does not match harbor slot {slot.HarborSlotId} node data.");
            }
        }

        if (board.Ports.Select(port => (port.TileQ, port.TileR, port.EdgeIndex)).Distinct().Count() != board.Ports.Count)
        {
            errors.Add("Legacy port data has duplicate coastal attachments.");
        }

        if (board.Ports.Count == 0)
        {
            warnings.Add("No legacy port records are present.");
        }
    }

    private static void ValidateWarden(
        BoardState board,
        string? wardenTileId,
        bool requireWardenOnDesert,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(wardenTileId))
        {
            return;
        }

        var deserts = board.Tiles.Where(tile => tile.ResourceType == TileResourceType.None).ToList();
        var desert = deserts.Count == 1 ? deserts[0] : null;
        var wardenTile = board.Tiles.FirstOrDefault(tile => string.Equals(tile.TileId, wardenTileId, StringComparison.OrdinalIgnoreCase));
        if (wardenTile is null)
        {
            errors.Add("The Warden references an unknown tile.");
        }

        if (requireWardenOnDesert
            && (desert is null || wardenTile is null || !string.Equals(desert.TileId, wardenTile.TileId, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("The Warden must start on the unique Desert tile.");
        }

        if (board.Tiles.Count(tile => tile.IsBlocked) != 1
            || wardenTile is null
            || !wardenTile.IsBlocked)
        {
            errors.Add("The Warden start state must block exactly the Desert tile.");
        }
    }

    private static Dictionary<string, HashSet<string>> BuildExpectedVertexTiles(IEnumerable<HexTile> tiles)
    {
        var expected = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in tiles)
        {
            var center = BoardGeometry.AxialToPixel(tile.Q, tile.R);
            for (var corner = 0; corner < 6; corner++)
            {
                var point = BoardGeometry.HexCorner(center.X, center.Y, corner);
                var key = BoardGeometry.PointKey(point.X, point.Y);
                if (!expected.TryGetValue(key, out var touchingTiles))
                {
                    touchingTiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    expected[key] = touchingTiles;
                }

                touchingTiles.Add(tile.TileId);
            }
        }

        return expected;
    }

    private static Dictionary<string, HashSet<string>> BuildExpectedEdges(
        IEnumerable<HexTile> tiles,
        IReadOnlyDictionary<string, List<BoardVertex>> verticesByPosition,
        List<string> errors)
    {
        var expected = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in tiles)
        {
            var center = BoardGeometry.AxialToPixel(tile.Q, tile.R);
            for (var edgeIndex = 0; edgeIndex < BoardGeometry.NeighborDirections.Length; edgeIndex++)
            {
                var startCorner = (5 - edgeIndex + 6) % 6;
                var endCorner = (startCorner + 1) % 6;
                var startPoint = BoardGeometry.HexCorner(center.X, center.Y, startCorner);
                var endPoint = BoardGeometry.HexCorner(center.X, center.Y, endCorner);
                if (!verticesByPosition.TryGetValue(BoardGeometry.PointKey(startPoint.X, startPoint.Y), out var startMatches)
                    || !verticesByPosition.TryGetValue(BoardGeometry.PointKey(endPoint.X, endPoint.Y), out var endMatches)
                    || startMatches.Count != 1
                    || endMatches.Count != 1)
                {
                    errors.Add($"Unable to resolve generated edge for tile {tile.TileId}.");
                    continue;
                }

                var key = BoardGeometry.VertexPairKey(startMatches[0].VertexId, endMatches[0].VertexId);
                if (!expected.TryGetValue(key, out var touchingTiles))
                {
                    touchingTiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    expected[key] = touchingTiles;
                }

                touchingTiles.Add(tile.TileId);
            }
        }

        return expected;
    }

    private static void ValidateUniqueKnownIds(
        IReadOnlyCollection<string> ids,
        IEnumerable<string> knownIds,
        string label,
        List<string> errors)
    {
        var known = knownIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Count != ids.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add($"{label} IDs contain duplicates.");
        }

        if (ids.Any(id => !known.Contains(id)))
        {
            errors.Add($"{label} IDs reference missing data.");
        }
    }

    private static bool IsTileGraphConnected(
        IReadOnlyList<HexTile> tiles,
        IReadOnlyDictionary<string, HexTile> tilesById)
    {
        if (tiles.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(tiles[0].TileId);
        visited.Add(tiles[0].TileId);

        while (pending.TryDequeue(out var tileId))
        {
            foreach (var adjacentId in tilesById[tileId].AdjacentTileIds)
            {
                if (tilesById.ContainsKey(adjacentId) && visited.Add(adjacentId))
                {
                    pending.Enqueue(adjacentId);
                }
            }
        }

        return visited.Count == tiles.Count;
    }
}

public sealed record BoardValidationResult(
    int BoardSeed,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class BoardGenerationException : Exception
{
    public BoardGenerationException(int boardSeed, IReadOnlyList<string> errors)
        : base($"Board generation failed for seed {boardSeed}: {string.Join(" | ", errors)}")
    {
        BoardSeed = boardSeed;
        Errors = errors;
    }

    public int BoardSeed { get; }
    public IReadOnlyList<string> Errors { get; }
}
