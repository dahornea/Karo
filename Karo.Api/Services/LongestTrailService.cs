using Karo.Api.Models;

namespace Karo.Api.Services;

public static class LongestTrailService
{
    public const int MinimumTrailLength = 5;

    public static int CalculateLongestTrail(GameState game, string playerId)
    {
        var playerEdges = game.Board.Edges
            .Where(edge => string.Equals(edge.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase);

        if (playerEdges.Count == 0)
        {
            return 0;
        }

        var edgesByVertex = new Dictionary<string, List<BoardEdge>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in playerEdges.Values)
        {
            AddEdge(edgesByVertex, edge.StartVertexId, edge);
            AddEdge(edgesByVertex, edge.EndVertexId, edge);
        }

        var verticesById = game.Board.Vertices.ToDictionary(vertex => vertex.VertexId, StringComparer.OrdinalIgnoreCase);
        var maxLength = 0;

        foreach (var vertexId in edgesByVertex.Keys)
        {
            maxLength = Math.Max(maxLength, Explore(vertexId, arrivedViaEdgeId: null, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }

        return maxLength;

        int Explore(string vertexId, string? arrivedViaEdgeId, HashSet<string> usedEdgeIds)
        {
            if (arrivedViaEdgeId is not null && IsOpponentStructure(verticesById, vertexId, playerId))
            {
                return 0;
            }

            if (!edgesByVertex.TryGetValue(vertexId, out var candidateEdges))
            {
                return 0;
            }

            var best = 0;
            foreach (var edge in candidateEdges)
            {
                if (usedEdgeIds.Contains(edge.EdgeId))
                {
                    continue;
                }

                usedEdgeIds.Add(edge.EdgeId);
                var nextVertexId = string.Equals(edge.StartVertexId, vertexId, StringComparison.OrdinalIgnoreCase)
                    ? edge.EndVertexId
                    : edge.StartVertexId;
                best = Math.Max(best, 1 + Explore(nextVertexId, edge.EdgeId, usedEdgeIds));
                usedEdgeIds.Remove(edge.EdgeId);
            }

            return best;
        }
    }

    private static void AddEdge(IDictionary<string, List<BoardEdge>> edgesByVertex, string vertexId, BoardEdge edge)
    {
        if (!edgesByVertex.TryGetValue(vertexId, out var edges))
        {
            edges = new List<BoardEdge>();
            edgesByVertex[vertexId] = edges;
        }

        edges.Add(edge);
    }

    private static bool IsOpponentStructure(IReadOnlyDictionary<string, BoardVertex> verticesById, string vertexId, string playerId)
    {
        return verticesById.TryGetValue(vertexId, out var vertex)
            && vertex.OwnerPlayerId is not null
            && vertex.StructureType is not null
            && !string.Equals(vertex.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
    }
}
