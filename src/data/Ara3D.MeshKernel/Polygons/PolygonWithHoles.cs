using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>2D profile boundary with optional holes; outer CCW, holes CW after normalization.</summary>
public sealed partial class PolygonWithHoles
{
    public PolygonWithHoles(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>>? holes = null)
    {
        Outer = NormalizeOuter(outer);
        Holes = MatchSingleHoleToOuter(Outer, (holes ?? []).Select(NormalizeHole).Where(h => h.Count >= 3).ToList());
    }

    public IReadOnlyList<Vector2> Outer { get; }
    public IReadOnlyList<IReadOnlyList<Vector2>> Holes { get; }

    public Bounds2D Bounds => Outer.GetBounds();
    public double SignedArea => new SimplePolygon2D(Outer).SignedArea()
        + Holes.Sum(h => new SimplePolygon2D(h).SignedArea());
    public double Area => Math.Abs(SignedArea);

    /// <summary>A lone hole with a different vertex count than the outer ring is resampled to the
    /// outer ring's count (when neither ring crosses itself), so the cap triangles and the extrusion
    /// walls built from <see cref="Holes"/> share their vertices whichever triangulation the cap uses.</summary>
    static IReadOnlyList<IReadOnlyList<Vector2>> MatchSingleHoleToOuter(
        IReadOnlyList<Vector2> outer, List<List<Vector2>> holes)
    {
        if (holes.Count != 1 || holes[0].Count == outer.Count || outer.Count < 3)
            return holes.Cast<IReadOnlyList<Vector2>>().ToList();
        if (HasSelfIntersection(outer) || HasSelfIntersection(holes[0]))
            return holes.Cast<IReadOnlyList<Vector2>>().ToList();
        return [ResampleClosedRing(holes[0], outer.Count)];
    }

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
