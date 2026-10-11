using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.Ifc.Mesher;

/// <summary>Polylines and indexed polycurves (point lists with line and arc segment indices).</summary>
internal static class PolyCurves
{
    public static List<Vector2> EvaluatePolyline2D(MeshingContext ctx, IfcEntity polyline)
        => MeshHelpers.ReadIds(polyline, IfcPolyline.Instance.Points)
            .Select(id => Placements.ReadPoint2D(ctx, ctx.GetEntity(id)))
            .ToList();

    public static List<Vector3> EvaluatePolyline3D(MeshingContext ctx, IfcEntity polyline)
        => MeshHelpers.ReadIds(polyline, IfcPolyline.Instance.Points)
            .Select(id => Placements.ReadPoint3D(ctx, ctx.GetEntity(id)))
            .ToList();

    public static List<Vector2> EvaluateIndexedPolyCurve2D(MeshingContext ctx, IfcEntity curve)
    {
        var points = ReadPointList2D(ctx, curve);
        var segmentToken = curve.GetValue(IfcIndexedPolyCurve.Instance.Segments.Index);
        if (!segmentToken.IsList)
            return points;

        var result = new List<Vector2>();
        foreach (var segToken in segmentToken.AsList(curve.Document))
        {
            if (segToken.IsId)
                AppendIndexedPolyCurveSegment(ctx, result, points, ctx.GetEntity(segToken.AsId()));
            else if (segToken.IsEntity)
            {
                var (name, value) = segToken.AsSimpleEntity(curve.Document);
                AppendIndexedPolyCurveSegment(ctx, result, points, name, value, curve.Document);
            }
        }
        return CurveEvaluator.SanitizeRingPoints(result.Count > 0 ? result : points);
    }

    public static List<Vector3> EvaluateIndexedPolyCurve3D(MeshingContext ctx, IfcEntity curve)
    {
        var points3D = ReadPointList3D(ctx, curve);
        if (points3D.Count > 0)
            return points3D;

        return EvaluateIndexedPolyCurve2D(ctx, curve).Select(p => new Vector3(p.X, p.Y, 0)).ToList();
    }

    static void AppendIndexedPolyCurveSegment(
        MeshingContext ctx,
        List<Vector2> result,
        IReadOnlyList<Vector2> points,
        IfcEntity seg)
    {
        if (seg.GetEntityName() == "IFCLINEINDEX")
        {
            ctx.Diagnostics.RecordSupported("IFCLINEINDEX");
            foreach (var idx in ReadOneBasedIndices(seg))
                result.Add(points[idx - 1]);
        }
        else if (seg.GetEntityName() == "IFCARCINDEX")
        {
            var indices = ReadOneBasedIndices(seg);
            if (indices.Count >= 3)
                AppendArc2D(result, points[indices[0] - 1], points[indices[1] - 1], points[indices[2] - 1], ctx.ArcSegments);
        }
    }

    static void AppendIndexedPolyCurveSegment(
        MeshingContext ctx,
        List<Vector2> result,
        IReadOnlyList<Vector2> points,
        string segmentName,
        StepToken value,
        StepDocument doc)
    {
        if (segmentName == "IFCLINEINDEX")
        {
            ctx.Diagnostics.RecordSupported("IFCLINEINDEX");
            foreach (var idx in ReadOneBasedIndices(value, doc))
                result.Add(points[idx - 1]);
        }
        else if (segmentName == "IFCARCINDEX")
        {
            var indices = ReadOneBasedIndices(value, doc);
            if (indices.Count >= 3)
                AppendArc2D(result, points[indices[0] - 1], points[indices[1] - 1], points[indices[2] - 1], ctx.ArcSegments);
        }
    }

    static List<Vector3> ReadPointList3D(MeshingContext ctx, IfcEntity indexed)
    {
        var coordsId = indexed.GetId(IfcIndexedPolyCurve.Instance.Points.Index);
        var list = ctx.GetEntity(coordsId);
        if (list.GetEntityName() != "IFCCARTESIANPOINTLIST3D")
            return [];

        var result = new List<Vector3>();
        var token = list.GetValue(IfcCartesianPointList3D.Instance.CoordList.Index);
        if (!token.IsList)
            return result;
        foreach (var item in token.AsList(list.Document))
        {
            if (!item.IsList)
                continue;
            var nums = item.AsList(list.Document).Where(t => t.IsNumber).Select(t => t.AsNumber()).ToList();
            result.Add(new Vector3(
                ctx.ScaleLength(nums.Count > 0 ? nums[0] : 0),
                ctx.ScaleLength(nums.Count > 1 ? nums[1] : 0),
                ctx.ScaleLength(nums.Count > 2 ? nums[2] : 0)));
        }
        return result;
    }

    static List<Vector2> ReadPointList2D(MeshingContext ctx, IfcEntity indexed)
    {
        var coordsId = indexed.GetId(IfcIndexedPolyCurve.Instance.Points.Index);
        var list = ctx.GetEntity(coordsId);
        if (list.GetEntityName() == "IFCCARTESIANPOINTLIST2D")
        {
            return ReadCoordList2D(ctx, list);
        }
        return MeshHelpers.ReadIds(indexed, IfcIndexedPolyCurve.Instance.Points)
            .Select(id => Placements.ReadPoint2D(ctx, ctx.GetEntity(id)))
            .ToList();
    }

    static List<Vector2> ReadCoordList2D(MeshingContext ctx, IfcEntity list)
    {
        var result = new List<Vector2>();
        var token = list.GetValue(0);
        if (!token.IsList)
            return result;
        foreach (var item in token.AsList(list.Document))
        {
            if (!item.IsList)
                continue;
            var nums = item.AsList(list.Document).Where(t => t.IsNumber).Select(t => t.AsNumber()).ToList();
            result.Add(new Vector2(
                ctx.ScaleLength(nums.Count > 0 ? nums[0] : 0),
                ctx.ScaleLength(nums.Count > 1 ? nums[1] : 0)));
        }
        return result;
    }

    static List<int> ReadOneBasedIndices(IfcEntity seg)
    {
        var token = seg.GetValue(0);
        return ReadOneBasedIndices(token, seg.Document);
    }

    static List<int> ReadOneBasedIndices(StepToken token, StepDocument doc)
    {
        if (!token.IsList)
            return [];
        return token.AsList(doc).Where(t => t.IsNumber).Select(t => (int)t.AsNumber()).ToList();
    }

    /// <summary>Appends the arc through three points as <paramref name="segments"/> chords; collinear
    /// points append the three points themselves.</summary>
    static void AppendArc2D(List<Vector2> dest, Vector2 start, Vector2 mid, Vector2 end, int segments)
    {
        if (!TryArcCenter(start, mid, end, out var center, out var radius, out var a0, out var a1))
        {
            dest.Add(start);
            dest.Add(mid);
            dest.Add(end);
            return;
        }

        var step = (a1 - a0) / segments;
        for (var i = 0; i <= segments; i++)
        {
            var a = a0 + step * i;
            dest.Add(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
        }
    }

    static bool TryArcCenter(Vector2 p0, Vector2 p1, Vector2 p2, out Vector2 center, out float radius, out float a0, out float a1)
    {
        var d = 2 * (p0.X * (p1.Y - p2.Y) + p1.X * (p2.Y - p0.Y) + p2.X * (p0.Y - p1.Y));
        if (MathF.Abs(d) < 1e-12f)
        {
            center = default;
            radius = 0;
            a0 = a1 = 0;
            return false;
        }

        var p0sq = p0.LengthSquared();
        var p1sq = p1.LengthSquared();
        var p2sq = p2.LengthSquared();
        var ux = (p0sq * (p1.Y - p2.Y) + p1sq * (p2.Y - p0.Y) + p2sq * (p0.Y - p1.Y)) / d;
        var uy = (p0sq * (p2.X - p1.X) + p1sq * (p0.X - p2.X) + p2sq * (p1.X - p0.X)) / d;
        center = new Vector2(ux, uy);
        radius = center.Distance(p0);
        a0 = MathF.Atan2(p0.Y - center.Y, p0.X - center.X);
        var am = MathF.Atan2(p1.Y - center.Y, p1.X - center.X);
        a1 = MathF.Atan2(p2.Y - center.Y, p2.X - center.X);
        if ((am - a0) * (a1 - am) < 0)
            a1 += a1 < a0 ? MathF.Tau : -MathF.Tau;
        return true;
    }
}
