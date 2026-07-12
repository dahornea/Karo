namespace Karo.Api.Services;

internal static class BoardGeometry
{
    public const int Radius = 2;
    public const double LayoutHexSize = 100;

    public static readonly (int Q, int R)[] NeighborDirections =
    [
        (1, 0),
        (1, -1),
        (0, -1),
        (-1, 0),
        (-1, 1),
        (0, 1)
    ];

    public static IReadOnlyList<(int Q, int R)> CreateAxialCoordinates()
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

    public static (double X, double Y) AxialToPixel(int q, int r)
    {
        return (
            LayoutHexSize * Math.Sqrt(3) * (q + r / 2d),
            LayoutHexSize * 1.5 * r);
    }

    public static (double X, double Y) HexCorner(double centerX, double centerY, int corner)
    {
        var angle = Math.PI / 180 * (30 + 60 * corner);
        return (
            centerX + LayoutHexSize * Math.Cos(angle),
            centerY + LayoutHexSize * Math.Sin(angle));
    }

    public static string PointKey(double x, double y)
    {
        // Casting the rounded values normalizes negative zero before it becomes a dictionary key.
        return $"{(long)Math.Round(x * 1000)}:{(long)Math.Round(y * 1000)}";
    }

    public static string VertexPairKey(string firstVertexId, string secondVertexId)
    {
        return string.Compare(firstVertexId, secondVertexId, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{firstVertexId}:{secondVertexId}"
            : $"{secondVertexId}:{firstVertexId}";
    }

    public static bool AreNeighbors((int Q, int R) first, (int Q, int R) second)
    {
        var delta = (second.Q - first.Q, second.R - first.R);
        return NeighborDirections.Contains(delta);
    }
}
