using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Splits the segments of a face ring in its surface's domain until each chord between consecutive
/// surface points strays from the surface by at most <c>maxSag</c> (<see cref="FaceRefinement.EdgeSag"/>
/// of the face's bound). A plane's chords never stray, so a planar ring comes back with the same points.
/// </summary>
public static class RingDensification
{
    /// <summary>The most pieces one segment is cut into at each level, and the levels of re-cutting.</summary>
    const int MaxPieces = 64;
    const int MaxDepth = 3;

    public static List<Vector2> DensifyRing(this SurfaceMap map, IReadOnlyList<Vector2> ring, float maxSag)
    {
        var result = new List<Vector2>(ring.Count);
        for (var i = 0; i < ring.Count; i++)
        {
            result.Add(ring[i]);
            map.AddInterior(ring[i], ring[(i + 1) % ring.Count], maxSag, MaxDepth, result);
        }
        return result;
    }

    /// <summary>Appends the points strictly between <paramref name="a"/> and <paramref name="b"/>. A
    /// chord's sag grows about with the square of its length, so a segment of sag s is cut into
    /// sqrt(s / maxSag) equal pieces, one more while the first piece still sags too far (the square
    /// law understates a wide arc's sag), and each piece is checked again.</summary>
    static void AddInterior(this SurfaceMap map, Vector2 a, Vector2 b, float maxSag, int depth, List<Vector2> result)
    {
        var sag = map.Sag(a, b);
        if (sag <= maxSag || depth == 0)
            return;
        var pieces = Math.Clamp((int)MathF.Ceiling(MathF.Sqrt(sag / maxSag)), 2, MaxPieces);
        while (pieces < MaxPieces && map.Sag(a, a + (b - a) / pieces) > maxSag)
            pieces++;
        for (var k = 0; k < pieces; k++)
        {
            var start = a + (b - a) * (k / (float)pieces);
            var end = a + (b - a) * ((k + 1) / (float)pieces);
            if (k > 0)
                result.Add(start);
            map.AddInterior(start, end, maxSag, depth - 1, result);
        }
    }
}
