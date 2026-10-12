using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>The band-of-quads triangulation of a polygon with one hole, and ring resampling.</summary>
public sealed partial class PolygonWithHoles
{
    static bool TryTriangulateOffsetRing(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<Vector2> inner,
        out IReadOnlyList<Triangle2D> triangles)
    {
        triangles = [];
        if (outer.Count < 3 || inner.Count < 3)
            return false;
        if (HasSelfIntersection(outer) || HasSelfIntersection(inner))
            return false;

        var outerPolygon = new SimplePolygon2D(outer);
        if (!inner.All(outerPolygon.ContainsStrict))
            return false;

        var innerRing = inner.Count == outer.Count ? inner : ResampleClosedRing(inner, outer.Count);
        if (TryTriangulateCongruentRing(outer, innerRing, out var band) && CoversRing(band, outer, inner))
        {
            triangles = band;
            return true;
        }
        return false;
    }

    /// <summary>True when the band's triangle areas add up to the area between the rings. Matching
    /// vertices by index or by resampling twists the band when the rings differ in shape (an
    /// outer and inner boundary made of arcs and lines), and the twisted band overlaps itself.</summary>
    static bool CoversRing(IReadOnlyList<Triangle2D> band, IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner)
    {
        var expected = Math.Abs(new SimplePolygon2D(outer).SignedArea()) - Math.Abs(new SimplePolygon2D(inner).SignedArea());
        var sum = band.Sum(t => Math.Abs(TriangleSignedArea(t)));
        return Math.Abs(sum - expected) <= 1e-3 * Math.Abs(expected);
    }

    static double TriangleSignedArea(Triangle2D t)
        => ((t.B.X.Value - t.A.X.Value) * (t.C.Y.Value - t.A.Y.Value)
            - (t.C.X.Value - t.A.X.Value) * (t.B.Y.Value - t.A.Y.Value)) / 2.0;

    /// <summary>Resamples a closed ring to <paramref name="targetCount"/> vertices (arc-length).</summary>
    public static List<Vector2> ResampleClosedRing(IReadOnlyList<Vector2> ring, int targetCount)
    {
        if (ring.Count == targetCount)
            return ring.ToList();
        if (ring.Count < 3 || targetCount < 3)
            return ring.ToList();

        var segLen = new float[ring.Count];
        var total = 0f;
        for (var i = 0; i < ring.Count; i++)
        {
            var d = ring[i].Distance(ring[(i + 1) % ring.Count]);
            segLen[i] = d;
            total += d;
        }
        if (total <= Tolerance.Eps)
            return ring.ToList();

        var result = new List<Vector2>(targetCount);
        var step = total / targetCount;
        var seg = 0;
        var segStart = 0f;
        for (var k = 0; k < targetCount; k++)
        {
            var target = k * step;
            while (seg < ring.Count - 1 && segStart + segLen[seg] < target - Tolerance.Eps)
            {
                segStart += segLen[seg];
                seg++;
            }

            var a = ring[seg];
            var b = ring[(seg + 1) % ring.Count];
            var len = segLen[seg];
            var t = len <= Tolerance.Eps ? 0f : (target - segStart) / len;
            result.Add(new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        }
        return result;
    }

    public static bool TryTriangulateCongruentRing(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<Vector2> inner,
        out IReadOnlyList<Triangle2D> triangles)
    {
        triangles = [];
        if (outer.Count != inner.Count || outer.Count < 3)
            return false;

        var tris = new List<Triangle2D>(outer.Count * 2);
        for (var i = 0; i < outer.Count; i++)
        {
            var j = (i + 1) % outer.Count;
            tris.Add(new Triangle2D(outer[i], outer[j], inner[j]));
            tris.Add(new Triangle2D(outer[i], inner[j], inner[i]));
        }
        triangles = tris;
        return true;
    }
}
