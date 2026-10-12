using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>IFCCOMPOSITECURVE: segments evaluated in turn and joined, each oriented by its SameSense
/// and its transition so that an arc meets the segment before it.</summary>
internal static class CompositeCurves
{
    public static List<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity composite)
    {
        var joinTolSq = CurveEvaluator.CompositeJoinToleranceSquared(ctx);
        var result = new List<Vector2>();
        foreach (var segId in MeshHelpers.ReadIds(composite, IfcCompositeCurve.Instance.Segments))
        {
            var seg = ctx.GetEntity(segId);
            var segEntity = MeshHelpers.ResolveRequired(ctx, seg, IfcCompositeCurveSegment.Instance.ParentCurve);
            var sameSense = MeshHelpers.ReadOptionalBool(seg, IfcCompositeCurveSegment.Instance.SameSense, true);
            var transition = seg.GetString(IfcCompositeCurveSegment.Instance.Transition.Index);
            var pts = EvaluateSegment2D(ctx, segEntity, sameSense);
            if (result.Count > 0 && pts.Count > 0)
            {
                if (segEntity.GetEntityName() == "IFCTRIMMEDCURVE"
                    && TryGetTrimmedCircleCenter(ctx, segEntity, out var arcCenter))
                {
                    if (result[^1].DistanceSquared(arcCenter) <= joinTolSq * 4f)
                        result.RemoveAt(result.Count - 1);
                }

                var continuous = !transition.Contains("DISCONTINUOUS", StringComparison.Ordinal);
                if (continuous)
                {
                    if (segEntity.GetEntityName() == "IFCTRIMMEDCURVE")
                        pts = OrientTrimmedArcForContinuousJoin(ctx, segEntity, result[^1], pts, joinTolSq);
                    if (result[^1].DistanceSquared(pts[0]) <= joinTolSq)
                        pts = pts.Skip(1).ToList();
                }
                else if (pts.Count > 1 && PointsContainNear(result, pts[0], joinTolSq))
                {
                    // DISCONTINUOUS closing chords often restart at an earlier vertex; omit the duplicate.
                    pts = pts.Skip(1).ToList();
                }
            }
            result.AddRange(pts);
        }
        return CurveEvaluator.SanitizeOpenPathPoints(result, joinTolSq);
    }

    public static List<Vector3> Evaluate3D(MeshingContext ctx, IfcEntity composite)
    {
        var joinTolSq = CurveEvaluator.CompositeJoinToleranceSquared(ctx);
        var result = new List<Vector3>();
        foreach (var segId in MeshHelpers.ReadIds(composite, IfcCompositeCurve.Instance.Segments))
        {
            var seg = ctx.GetEntity(segId);
            var segEntity = MeshHelpers.ResolveRequired(ctx, seg, IfcCompositeCurveSegment.Instance.ParentCurve);
            var sameSense = MeshHelpers.ReadOptionalBool(seg, IfcCompositeCurveSegment.Instance.SameSense, true);
            var transition = seg.GetString(IfcCompositeCurveSegment.Instance.Transition.Index);
            var pts = EvaluateSegment3D(ctx, segEntity, sameSense);
            if (result.Count > 0 && pts.Count > 0)
            {
                var continuous = !transition.Contains("DISCONTINUOUS", StringComparison.Ordinal);
                if (continuous && result[^1].DistanceSquared(pts[0]) <= joinTolSq)
                    pts = pts.Skip(1).ToList();
            }
            result.AddRange(pts);
        }
        return SanitizeOpenPathPoints3D(result, joinTolSq);
    }

    static List<Vector2> EvaluateSegment2D(MeshingContext ctx, IfcEntity segEntity, bool sameSense)
    {
        var pts = segEntity.GetEntityName() == "IFCTRIMMEDCURVE"
            ? TrimmedCurves.Evaluate2D(ctx, segEntity).ToList()
            : CurveEvaluator.Evaluate2D(ctx, segEntity, dropClosure: true).ToList();
        if (!sameSense)
            pts.Reverse();
        return pts;
    }

    static List<Vector3> EvaluateSegment3D(MeshingContext ctx, IfcEntity segEntity, bool sameSense)
    {
        var pts = segEntity.GetEntityName() == "IFCTRIMMEDCURVE"
            ? TrimmedCurves.Evaluate3D(ctx, segEntity)
            : CurveEvaluator.Evaluate3D(ctx, segEntity).ToList();
        if (!sameSense)
            pts.Reverse();
        return pts;
    }

    static bool TryGetTrimmedCircleCenter(MeshingContext ctx, IfcEntity trimmed, out Vector2 center)
    {
        center = default;
        var basis = MeshHelpers.ResolveRequired(ctx, trimmed, IfcTrimmedCurve.Instance.BasisCurve);
        if (basis.GetEntityName() != "IFCCIRCLE")
            return false;
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, basis, IfcConic.Instance.Position);
        var w = placement.Origin;
        center = new Vector2(w.X, w.Y);
        return true;
    }

    static bool PointsContainNear(IReadOnlyList<Vector2> points, Vector2 target, float tolSq)
    {
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].DistanceSquared(target) <= tolSq)
                return true;
        }
        return false;
    }

    static List<Vector2> OrientTrimmedArcForContinuousJoin(
        MeshingContext ctx,
        IfcEntity trimmed,
        Vector2 join,
        List<Vector2> pts,
        float joinTolSq)
    {
        if (pts.Count < 2)
            return pts;

        static bool Near(Vector2 a, Vector2 b, float tolSq) => a.DistanceSquared(b) <= tolSq;

        if (Near(join, pts[0], joinTolSq))
            return pts;
        if (Near(join, pts[^1], joinTolSq))
        {
            pts.Reverse();
            return pts;
        }

        if (TrySampleAlternateLongTrimmedArc2D(ctx, trimmed, out var alt) && alt.Count >= 2)
        {
            if (Near(join, alt[0], joinTolSq))
                return alt;
            if (Near(join, alt[^1], joinTolSq))
            {
                alt.Reverse();
                return alt;
            }
        }

        if (join.DistanceSquared(pts[^1]) < join.DistanceSquared(pts[0]))
            pts.Reverse();
        return pts;
    }

    /// <summary>For a SenseAgreement=.F. arc over more than half a turn, the arc swept the other
    /// way round from the first trim, in case that is the one that meets the previous segment.</summary>
    static bool TrySampleAlternateLongTrimmedArc2D(MeshingContext ctx, IfcEntity trimmed, out List<Vector2> arc)
    {
        arc = [];
        var sense = MeshHelpers.ReadOptionalBool(trimmed, IfcTrimmedCurve.Instance.SenseAgreement, true);
        if (sense)
            return false;

        var basis = MeshHelpers.ResolveRequired(ctx, trimmed, IfcTrimmedCurve.Instance.BasisCurve);
        var u1 = TrimmedCurves.ReadTrimParameter(ctx, trimmed, basis, IfcTrimmedCurve.Instance.Trim1);
        var u2 = TrimmedCurves.ReadTrimParameter(ctx, trimmed, basis, IfcTrimmedCurve.Instance.Trim2);
        var raw = u2 - u1;
        if (MathF.Abs(raw) <= MathF.PI + 1e-3f)
            return false;

        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, basis, IfcConic.Instance.Position);
        arc = basis.GetEntityName() switch
        {
            "IFCCIRCLE" => Conics.SampleConicArc2D(placement, CircleArc(ctx, basis, u1, raw), ctx.CircleSegments),
            "IFCELLIPSE" => Conics.SampleConicArc2D(placement, EllipseArc(ctx, basis, u1, raw), ctx.CircleSegments),
            _ => [],
        };
        return arc.Count >= 2;
    }

    static ConicArc CircleArc(MeshingContext ctx, IfcEntity circle, float start, float sweep)
    {
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(circle, IfcCircle.Instance.Radius));
        return new ConicArc(radius, radius, start, sweep);
    }

    static ConicArc EllipseArc(MeshingContext ctx, IfcEntity ellipse, float start, float sweep)
        => new((float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis1)),
            (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis2)),
            start, sweep);

    static List<Vector3> SanitizeOpenPathPoints3D(IReadOnlyList<Vector3> points, float joinToleranceSquared)
    {
        if (points.Count == 0)
            return [];
        var result = new List<Vector3>(points.Count) { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if (result[^1].DistanceSquared(points[i]) > joinToleranceSquared)
                result.Add(points[i]);
        }
        return result;
    }
}
