using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Reads an IFCFACE's bounds as 3D rings: the outer ring, the holes, and whether the face's
/// orientation agrees with its outer bound. Loops are polyloops (point lists) or edge loops
/// (oriented edges whose curves are evaluated); a curve-bounded plane supplies bounds a face
/// omitted.
/// </summary>
internal static class FaceBounds
{
    public static (List<Vector3> outer, List<List<Vector3>> holes, bool sameSense) Read(MeshingContext ctx, IfcEntity face)
    {
        var bounds = MeshHelpers.ReadIds(face, IfcFace.Instance.Bounds);
        var outer = new List<Vector3>();
        var holes = new List<List<Vector3>>();
        var sameSense = true;

        foreach (var boundId in bounds)
        {
            var bound = ctx.GetEntity(boundId);
            var loop = MeshHelpers.ResolveRequired(ctx, bound, IfcFaceBound.Instance.Bound);
            var pts = ReadLoop(ctx, loop);
            var isOuter = bound.GetEntityName() == "IFCFACEOUTERBOUND";
            if (isOuter)
                outer = pts;
            else
                holes.Add(pts);
            if (bound.GetEntityName() == "IFCFACEBOUND" && bound.GetString(1).Contains(".F."))
                sameSense = false;
        }

        if (outer.Count < 3 && IsFaceSurfaceEntity(face))
            TryAppendCurveBoundedPlaneBounds(ctx, face, ref outer, holes);

        return (outer, holes, sameSense);
    }

    public static bool IsFaceSurfaceEntity(IfcEntity face)
        => face.GetEntityName() is "IFCADVANCEDFACE" or "IFCFACESURFACE";

    static void TryAppendCurveBoundedPlaneBounds(
        MeshingContext ctx,
        IfcEntity face,
        ref List<Vector3> outer,
        List<List<Vector3>> holes)
    {
        var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
        if (surface?.GetEntityName() != "IFCCURVEBOUNDEDPLANE")
            return;

        ctx.Diagnostics.RecordSupported("IFCCURVEBOUNDEDPLANE");
        var outerCurve = MeshHelpers.ResolveOptional(ctx, surface, IfcCurveBoundedPlane.Instance.OuterBoundary);
        if (outerCurve != null && outer.Count < 3)
            outer = EvaluateBoundaryCurve3D(ctx, outerCurve);

        foreach (var innerId in MeshHelpers.ReadIds(surface, IfcCurveBoundedPlane.Instance.InnerBoundaries))
            holes.Add(EvaluateBoundaryCurve3D(ctx, ctx.GetEntity(innerId)));
    }

    static List<Vector3> ReadLoop(MeshingContext ctx, IfcEntity loop)
        => loop.GetEntityName() switch
        {
            "IFCPOLYLOOP" => ReadPolyLoop(ctx, loop),
            "IFCEDGELOOP" => ReadEdgeLoop(ctx, loop),
            _ => throw new NotSupportedException($"Unsupported loop {loop.GetEntityName()}"),
        };

    static List<Vector3> ReadPolyLoop(MeshingContext ctx, IfcEntity loop)
    {
        var points = MeshHelpers.ReadIds(loop, IfcPolyLoop.Instance.Polygon)
            .Select(id => Placements.ReadPoint3D(ctx, ctx.GetEntity(id)))
            .ToList();
        return DedupeConsecutive3D(points);
    }

    static List<Vector3> ReadEdgeLoop(MeshingContext ctx, IfcEntity loop)
    {
        ctx.Diagnostics.RecordSupported("IFCEDGELOOP");
        var joinTolSq = CurveEvaluator.JoinToleranceSquaredFor(ctx);
        var result = new List<Vector3>();
        foreach (var edgeId in MeshHelpers.ReadIds(loop, IfcEdgeLoop.Instance.EdgeList))
        {
            var pts = EvaluateOrientedEdgePoints(ctx, ctx.GetEntity(edgeId));
            if (result.Count > 0 && pts.Count > 0)
            {
                if ((result[^1] - pts[0]).LengthSquared() <= joinTolSq)
                    pts = pts.Skip(1).ToList();
            }
            result.AddRange(pts);
        }
        return DedupeConsecutive3D(result);
    }

    static List<Vector3> EvaluateOrientedEdgePoints(MeshingContext ctx, IfcEntity orientedEdge)
    {
        var edgeEntity = MeshHelpers.ResolveRequired(ctx, orientedEdge, IfcOrientedEdge.Instance.EdgeElement);
        var orientation = MeshHelpers.ReadOptionalBool(orientedEdge, IfcOrientedEdge.Instance.Orientation, true);
        var pts = edgeEntity.GetEntityName() switch
        {
            "IFCEDGECURVE" => EvaluateEdgeCurvePoints(ctx, edgeEntity),
            "IFCORIENTEDEDGE" => EvaluateOrientedEdgePoints(ctx, edgeEntity),
            "IFCEDGE" => EvaluatePlainEdgePoints(ctx, edgeEntity),
            _ => throw new NotSupportedException($"Unsupported edge {edgeEntity.GetEntityName()}"),
        };
        if (!orientation)
            pts.Reverse();
        return pts;
    }

    static List<Vector3> EvaluateEdgeCurvePoints(MeshingContext ctx, IfcEntity edgeCurve)
    {
        ctx.Diagnostics.RecordSupported("IFCEDGECURVE");
        var curve = MeshHelpers.ResolveRequired(ctx, edgeCurve, IfcEdgeCurve.Instance.EdgeGeometry);
        var sameSense = MeshHelpers.ReadOptionalBool(edgeCurve, IfcEdgeCurve.Instance.SameSense, true);
        var pts = CurveEvaluator.Evaluate3D(ctx, curve).ToList();
        if (!sameSense)
            pts.Reverse();
        return pts;
    }

    static List<Vector3> EvaluatePlainEdgePoints(MeshingContext ctx, IfcEntity edge)
    {
        var pts = new List<Vector3>(2);
        var start = MeshHelpers.ResolveOptional(ctx, edge, IfcEdge.Instance.EdgeStart);
        var end = MeshHelpers.ResolveOptional(ctx, edge, IfcEdge.Instance.EdgeEnd);
        if (start != null)
            pts.Add(ReadVertexPoint(ctx, start));
        if (end != null)
            pts.Add(ReadVertexPoint(ctx, end));
        return pts;
    }

    static Vector3 ReadVertexPoint(MeshingContext ctx, IfcEntity vertex)
    {
        if (vertex.GetEntityName() != "IFCVERTEXPOINT")
            throw new NotSupportedException($"Expected IFCVERTEXPOINT, got {vertex.GetEntityName()}");
        var point = MeshHelpers.ResolveRequired(ctx, vertex, IfcVertexPoint.Instance.VertexGeometry);
        return Placements.ReadPoint3D(ctx, point);
    }

    static List<Vector3> EvaluateBoundaryCurve3D(MeshingContext ctx, IfcEntity curve)
        => DedupeConsecutive3D(CurveEvaluator.Evaluate3D(ctx, curve).ToList());

    /// <summary>Drops points within 1e-6 of the one before them, and a last point that repeats the first.</summary>
    public static List<Vector3> DedupeConsecutive3D(IReadOnlyList<Vector3> points)
    {
        if (points.Count < 2)
            return points.ToList();

        const float epsSq = 1e-12f;
        var cleaned = new List<Vector3> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if ((points[i] - cleaned[^1]).LengthSquared() > epsSq)
                cleaned.Add(points[i]);
        }

        if (cleaned.Count > 1 && (cleaned[0] - cleaned[^1]).LengthSquared() <= epsSq)
            cleaned.RemoveAt(cleaned.Count - 1);
        return cleaned;
    }
}
