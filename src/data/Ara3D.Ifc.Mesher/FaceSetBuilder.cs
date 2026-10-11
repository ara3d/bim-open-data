using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Triangulates a set of IFCFACE entities into one welded mesh. Each face's bounds are read by
/// <see cref="FaceBounds"/>, projected to 2D through the <see cref="SurfaceMap"/> for its surface,
/// triangulated there, and unprojected onto the surface. A face that cannot be triangulated is
/// left out and recorded against the current product.
/// </summary>
internal static class FaceSetBuilder
{
    /// <summary>The largest relative difference between a face ring's area and its triangles'
    /// (<see cref="Earcut.Deviation(IReadOnlyList{Vector2}, IReadOnlyList{IReadOnlyList{Vector2}}, IReadOnlyList{Integer3})"/>)
    /// for the face to be kept.</summary>
    const double MaxFaceAreaDeviation = 0.01;

    const string TriangulationFailed = "triangulation failed";

    public static TriangleMesh3D Build(MeshingContext ctx, IReadOnlyList<int> faceIds)
    {
        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        var pointMap = new Dictionary<(int, int, int), int>();

        foreach (var faceId in faceIds)
        {
            var face = ctx.GetEntity(faceId);
            RecordFaceDiagnostics(ctx, face);
            var (outer, holes, sameSense) = FaceBounds.Read(ctx, face);
            if (outer.Count < 3)
            {
                ctx.RecordDroppedFace(faceId, "fewer than 3 boundary points");
                continue;
            }

            var map = SurfaceMap.Resolve(ctx, face, outer);
            var outer2 = DedupeConsecutive(map.ProjectRing(outer));
            var holes2 = holes
                .Select(h => DedupeConsecutive(map.ProjectRing(h)))
                .Where(h => h.Count >= 3)
                .ToList();
            if (map is CylinderMap cylinderMap)
            {
                outer2 = CylinderMap.DensifyUvRing(outer2, cylinderMap.Radius);
                holes2 = holes2
                    .Select(h => CylinderMap.DensifyUvRing(h, cylinderMap.Radius))
                    .Where(h => h.Count >= 3)
                    .ToList();
            }
            if (outer2.Count < 3)
            {
                ctx.RecordDroppedFace(faceId, "fewer than 3 distinct boundary points");
                continue;
            }

            if (!TryTriangulateFaceRing(outer2, holes2, out var tris, out var dropReason))
            {
                ctx.RecordDroppedFace(faceId, dropReason);
                continue;
            }

            var indexMap = new Dictionary<(int, int, int), int>();
            int GetIndex(Vector3 p3)
            {
                var key = Quantize3(p3);
                if (indexMap.TryGetValue(key, out var idx))
                    return idx;
                if (pointMap.TryGetValue(key, out idx))
                {
                    indexMap[key] = idx;
                    return idx;
                }
                idx = points.Count;
                points.Add(p3);
                pointMap[key] = idx;
                indexMap[key] = idx;
                return idx;
            }

            foreach (var tri in tris)
            {
                var a = GetIndex(map.Unproject(tri.A.Vector2));
                var b = GetIndex(map.Unproject(tri.B.Vector2));
                var c = GetIndex(map.Unproject(tri.C.Vector2));
                if (sameSense)
                    faces.Add(new Integer3(a, b, c));
                else
                    faces.Add(new Integer3(a, c, b));
            }
        }

        return new TriangleMesh3D(points, faces);
    }

    static void RecordFaceDiagnostics(MeshingContext ctx, IfcEntity face)
    {
        var name = face.GetEntityName();
        if (name is "IFCADVANCEDFACE" or "IFCFACESURFACE")
        {
            var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
            if (surface?.GetEntityName() is "IFCPLANE" or "IFCCURVEBOUNDEDPLANE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Planar advanced face via edge-loop bounds");
            }
            else if (surface?.GetEntityName() is "IFCCYLINDRICALSURFACE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Cylindrical advanced face tessellated in surface parameter space");
            }
            else if (surface?.GetEntityName() is "IFCSURFACEOFREVOLUTION")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Surface-of-revolution advanced face tessellated in (angle, meridian) space");
            }
            else if (surface?.GetEntityName() is "IFCBSPLINESURFACEWITHKNOTS" or "IFCRATIONALBSPLINESURFACEWITHKNOTS" or "IFCBSPLINESURFACE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "B-spline advanced face tessellated over its (u,v) control-net parameter grid");
            }
            else
                ctx.Diagnostics.RecordApproximate(name, "Bounds only; curved advanced-face surfaces ignored");
        }
    }

    /// <summary>
    /// Triangulates a projected face boundary. Thin 3- and 4-vertex rings (common on Institute
    /// helix shell facets) are kept as one or two triangles, or dropped when their area is below
    /// <see cref="Tolerance.Eps"/>; other rings go to earcut, and are dropped when its triangles'
    /// area is more than <see cref="MaxFaceAreaDeviation"/> from the ring's. When it returns false,
    /// <paramref name="dropReason"/> says why, for the caller to record.
    /// </summary>
    static bool TryTriangulateFaceRing(
        IReadOnlyList<Vector2> outer2,
        IReadOnlyList<List<Vector2>> holes2,
        out IReadOnlyList<Triangle2D> tris,
        out string dropReason)
    {
        tris = [];
        dropReason = TriangulationFailed;
        if (outer2.Count < 3)
            return false;

        if (holes2.Count == 0)
        {
            var ringArea = MathF.Abs(SignedArea2(outer2[0], outer2[1], outer2[2]));
            if (outer2.Count >= 4)
                ringArea += MathF.Abs(SignedArea2(outer2[0], outer2[2], outer2[3]));

            // Fast path only for near-degenerate Institute-style facets; normal quads use earcut.
            const float thinAreaThreshold = 1e-4f;
            if (ringArea < thinAreaThreshold)
            {
                if (outer2.Count == 3)
                {
                    if (ringArea <= Tolerance.Eps)
                        return false;
                    tris = [new Triangle2D(outer2[0], outer2[1], outer2[2])];
                    return true;
                }

                if (outer2.Count == 4 && TryTriangulateConvexQuad(outer2, out var quadTris))
                {
                    tris = quadTris;
                    return true;
                }
            }
        }

        if (holes2.Count == 1 && PolygonWithHoles.TryTriangulateCongruentRing(outer2, holes2[0], out var ringTris))
        {
            tris = ringTris;
            return true;
        }

        // A projected ring can double back on itself without crossing (a parameter-space ring that
        // folds over); earcut's triangles then overlap and cover more than the face. Leave such a
        // face out rather than emit area it does not have.
        var indices = Earcut.Triangulate(outer2, holes2);
        var deviation = Earcut.Deviation(outer2, holes2, indices);
        if (deviation > MaxFaceAreaDeviation)
        {
            dropReason = $"area off by {deviation * 100:0.#} %";
            return false;
        }
        tris = PolygonWithHoles.ToTriangles(outer2, holes2, indices);
        return tris.Count > 0;
    }

    static bool TryTriangulateConvexQuad(IReadOnlyList<Vector2> quad, out IReadOnlyList<Triangle2D> tris)
    {
        tris = [];
        if (quad.Count != 4)
            return false;

        var area = MathF.Abs(
            SignedArea2(quad[0], quad[1], quad[2]) + SignedArea2(quad[0], quad[2], quad[3]));
        if (area <= Tolerance.Eps)
            return false;

        var diag02 = quad[0].DistanceSquared(quad[2]);
        var diag13 = quad[1].DistanceSquared(quad[3]);
        if (diag02 <= diag13)
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

    static (int, int, int) Quantize3(Vector3 p)
        => ((int)MathF.Round(p.X * 1e5f), (int)MathF.Round(p.Y * 1e5f), (int)MathF.Round(p.Z * 1e5f));
}
