using Ara3D.Geometry;

namespace Ara3D.Ifc.Mesher.Approach1;

/// <summary>2D profile boundary with optional holes; outer CCW, holes CW after normalization.</summary>
public sealed class PolygonWithHoles
{
    public PolygonWithHoles(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>>? holes = null)
    {
        Outer = NormalizeOuter(outer);
        Holes = (holes ?? []).Select(NormalizeHole).Where(h => h.Count >= 3).ToList();
    }

    public IReadOnlyList<Vector2> Outer { get; }
    public IReadOnlyList<IReadOnlyList<Vector2>> Holes { get; }

    public Bounds2D Bounds => Outer.GetBounds();
    public double SignedArea => new SimplePolygon2D(Outer).SignedArea()
        + Holes.Sum(h => new SimplePolygon2D(h).SignedArea());
    public double Area => Math.Abs(SignedArea);

    /// <summary>The profile's triangles. A single hole is joined to the outer ring by a band of
    /// quads (its ring resampled to the outer ring's count) when it lies inside; otherwise earcut
    /// triangulates, adding no points.</summary>
    public IReadOnlyList<Triangle2D> Triangulate()
        => Holes.Count == 1 && TryTriangulateOffsetRing(Outer, Holes[0], out var ringTris)
            ? ringTris
            : EarcutTriangles(Outer, Holes);

    /// <summary><see cref="Earcut"/>'s triangles as positions, each counter-clockwise.</summary>
    public static IReadOnlyList<Triangle2D> EarcutTriangles(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<IReadOnlyList<Vector2>> holes)
        => ToTriangles(outer, holes, Earcut.Triangulate(outer, holes));

    /// <summary>Triangles given as indices into the outer ring followed by each hole, as positions.</summary>
    public static IReadOnlyList<Triangle2D> ToTriangles(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<IReadOnlyList<Vector2>> holes,
        IEnumerable<Integer3> triangles)
    {
        var points = holes.Prepend(outer).SelectMany(ring => ring).ToList();
        return triangles.Select(t => new Triangle2D(points[t.A], points[t.B], points[t.C])).ToList();
    }

    /// <summary>True when two non-adjacent edges of the ring cross (touching does not count).</summary>
    public static bool HasSelfIntersection(IReadOnlyList<Vector2> ring)
        => new SimplePolygon2D(ring).HasSelfIntersectionsSweep(includeTouching: false).HasValue;

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
        return TryTriangulateCongruentRing(outer, innerRing, out triangles);
    }

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

    internal static bool TryTriangulateCongruentRing(
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

    public IReadOnlyList<Point3D> To3DPoints(Frame3D frame)
        => Outer.Select(p => (Point3D)frame.ToWorld(new Vector3(p.X, p.Y, 0))).ToList();

    static List<Vector2> NormalizeOuter(IReadOnlyList<Vector2> points)
    {
        var list = RemoveDuplicateClosure(points.ToList());
        if (list.Count >= 3 && !IsCounterClockwise(list))
            list.Reverse();
        return list;
    }

    static List<Vector2> NormalizeHole(IReadOnlyList<Vector2> points)
    {
        var list = RemoveDuplicateClosure(points.ToList());
        if (list.Count >= 3 && IsCounterClockwise(list))
            list.Reverse();
        return list;
    }

    static bool IsCounterClockwise(IReadOnlyList<Vector2> ring)
        => new SimplePolygon2D(ring).SignedArea() > 0;

    /// <summary>Removes consecutive duplicates and explicit geometric closure for profile rings.</summary>
    public static List<Vector2> CleanRing(IReadOnlyList<Vector2> points, float joinToleranceSquared = 0)
    {
        if (points.Count == 0)
            return [];

        var epsSq = joinToleranceSquared > 0
            ? joinToleranceSquared
            : Tolerance.Eps * Tolerance.Eps;
        var ring = new List<Vector2> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].DistanceSquared(ring[^1]) > epsSq)
                ring.Add(points[i]);
        }

        while (ring.Count >= 2 && ring[0].DistanceSquared(ring[^1]) <= epsSq)
            ring.RemoveAt(ring.Count - 1);

        return ring;
    }

    static List<Vector2> RemoveDuplicateClosure(List<Vector2> points)
        => CleanRing(points);
}
