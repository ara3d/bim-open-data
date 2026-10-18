using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Meshes one face whose bounds a reader has already turned into 3D rings and whose surface it has
/// turned into a <see cref="SurfaceMap"/>. The rings are projected into the map's 2D domain, cleaned,
/// densified where their chords stray from the surface (<see cref="RingDensification"/>), triangulated
/// there (<see cref="FaceRingTriangulation"/>) and refined inside (<see cref="FaceRefinement"/>), and
/// every corner is lifted back onto the surface and welded into the solid's mesh. A planar face is
/// neither densified nor refined. Nothing here reads a file.
/// </summary>
public static class FaceMesher
{
    /// <summary>
    /// Adds the face's triangles to <paramref name="mesh"/> and returns null, or adds nothing and
    /// returns why. The outer ring is projected before the holes, because a cylinder or revolution
    /// map takes its angle reference from the first ring it sees. Triangles are counter-clockwise
    /// in the map's domain, so they face along the map's U x V; <paramref name="sameSense"/> false
    /// reverses each one. <paramref name="maxDeviation"/> is how far, in metres, a triangle may lie
    /// from a curved surface.
    /// </summary>
    public static FaceDrop? AddFace(
        this WeldedMeshBuilder mesh,
        SurfaceMap map,
        IReadOnlyList<Vector3> outer,
        IReadOnlyList<IReadOnlyList<Vector3>> holes,
        bool sameSense,
        float maxDeviation = Tolerance.ChordDeviation)
    {
        var maxSag = FaceRefinement.EdgeSag(maxDeviation);
        var outer2 = map.DensifyRing(DedupeConsecutive(map.ProjectRing(outer)), maxSag);
        var holes2 = holes
            .Select(h => DedupeConsecutive(map.ProjectRing(h)))
            .Where(h => h.Count >= 3)
            .Select(h => map.DensifyRing(h, maxSag))
            .ToList();
        if (outer2.Count < 3)
            return new FaceDrop(FaceDropKind.TooFewDistinctPoints);

        if (FaceRingTriangulation.Triangulate(outer2, holes2, out var ringTris) is { } drop)
            return drop;

        foreach (var tri in map.Refine(ringTris, maxDeviation))
        {
            var a = mesh.Index(map.Unproject(tri.A.Vector2));
            var b = mesh.Index(map.Unproject(tri.B.Vector2));
            var c = mesh.Index(map.Unproject(tri.C.Vector2));
            mesh.AddFace(sameSense ? new Integer3(a, b, c) : new Integer3(a, c, b));
        }
        return null;
    }

    /// <summary>Merges consecutive points within <see cref="Tolerance.Eps"/> and drops a closing duplicate.</summary>
    static List<Vector2> DedupeConsecutive(IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
            return points.ToList();

        var epsSq = Tolerance.Eps * Tolerance.Eps;
        var cleaned = new List<Vector2> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].DistanceSquared(cleaned[^1]) > epsSq)
                cleaned.Add(points[i]);
        }

        if (cleaned.Count > 1 && cleaned[0].DistanceSquared(cleaned[^1]) <= epsSq)
            cleaned.RemoveAt(cleaned.Count - 1);
        return cleaned;
    }
}
