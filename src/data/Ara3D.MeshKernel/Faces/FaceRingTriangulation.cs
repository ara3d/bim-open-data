using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Triangulates a face's rings in its surface's 2D domain. Thin 3- and 4-vertex rings (common on
/// helix shell facets) are kept as one or two triangles, or dropped when their doubled area is
/// below <see cref="Tolerance.Eps"/>; one hole with the outer ring's vertex count is joined by a
/// band of quads; other rings go to <see cref="Earcut"/>, and are dropped when its triangles' area
/// is more than <see cref="MaxAreaDeviation"/> from the rings'.
/// </summary>
public static class FaceRingTriangulation
{
    /// <summary>The largest relative difference between a face ring's area and its triangles'
    /// (<see cref="Earcut.Deviation(IReadOnlyList{Vector2}, IReadOnlyList{IReadOnlyList{Vector2}}, IReadOnlyList{Integer3})"/>)
    /// for the face to be kept.</summary>
    public const double MaxAreaDeviation = 0.01;

    /// <summary>Doubled area below which a hole-free 3- or 4-vertex ring skips earcut.</summary>
    const float ThinAreaThreshold = 1e-4f;

    /// <summary>Null with the triangles, each counter-clockwise, or the reason there are none.</summary>
    public static FaceDrop? Triangulate(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<IReadOnlyList<Vector2>> holes,
        out IReadOnlyList<Triangle2D> tris)
    {
        tris = [];
        var failed = new FaceDrop(FaceDropKind.TriangulationFailed);
        if (outer.Count < 3)
            return failed;

        if (holes.Count == 0 && TryTriangulateThin(outer, out var thinTris) is { } thin)
        {
            tris = thinTris;
            return thin ? null : failed;
        }

        if (holes.Count == 1 && PolygonWithHoles.TryTriangulateCongruentRing(outer, holes[0], out var ringTris))
        {
            tris = ringTris;
            return null;
        }

        // A projected ring can double back on itself without crossing (a parameter-space ring that
        // folds over); earcut's triangles then overlap and cover more than the face. Leave such a
        // face out rather than emit area it does not have.
        var indices = Earcut.Triangulate(outer, holes);
        var deviation = Earcut.Deviation(outer, holes, indices);
        if (deviation > MaxAreaDeviation)
            return new FaceDrop(FaceDropKind.AreaOff, deviation);
        tris = PolygonWithHoles.ToTriangles(outer, holes, indices);
        return tris.Count > 0 ? null : failed;
    }

    /// <summary>True with the triangles of a thin ring, false when a thin triangle is too small to
    /// keep, null when the ring is not thin or is a quad that is not convex (earcut decides).</summary>
    static bool? TryTriangulateThin(IReadOnlyList<Vector2> outer, out IReadOnlyList<Triangle2D> tris)
    {
        tris = [];
        var ringArea = MathF.Abs(SignedArea2(outer[0], outer[1], outer[2]));
        if (outer.Count >= 4)
            ringArea += MathF.Abs(SignedArea2(outer[0], outer[2], outer[3]));
        if (ringArea < ThinAreaThreshold)
        {
            if (outer.Count == 3)
            {
                if (ringArea <= Tolerance.Eps)
                    return false;
                tris = [new Triangle2D(outer[0], outer[1], outer[2])];
                return true;
            }

            if (outer.Count == 4 && TryTriangulateConvexQuad(outer, out var quadTris))
            {
                tris = quadTris;
                return true;
            }
        }
        return null;
    }

    static bool TryTriangulateConvexQuad(IReadOnlyList<Vector2> quad, out IReadOnlyList<Triangle2D> tris)
    {
        tris = [];
        var area = MathF.Abs(SignedArea2(quad[0], quad[1], quad[2]) + SignedArea2(quad[0], quad[2], quad[3]));
        if (area <= Tolerance.Eps)
            return false;

        if (quad[0].DistanceSquared(quad[2]) <= quad[1].DistanceSquared(quad[3]))
        {
            if (SignedArea2(quad[0], quad[1], quad[2]) <= Tolerance.Eps
                || SignedArea2(quad[0], quad[2], quad[3]) <= Tolerance.Eps)
                return false;
            tris = [new Triangle2D(quad[0], quad[1], quad[2]), new Triangle2D(quad[0], quad[2], quad[3])];
        }
        else
        {
            if (SignedArea2(quad[0], quad[1], quad[3]) <= Tolerance.Eps
                || SignedArea2(quad[1], quad[2], quad[3]) <= Tolerance.Eps)
                return false;
            tris = [new Triangle2D(quad[0], quad[1], quad[3]), new Triangle2D(quad[1], quad[2], quad[3])];
        }
        return true;
    }

    static float SignedArea2(Vector2 a, Vector2 b, Vector2 c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}
