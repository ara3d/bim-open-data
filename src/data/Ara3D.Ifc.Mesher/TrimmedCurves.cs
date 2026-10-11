using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.Ifc.Mesher;

/// <summary>IFCTRIMMEDCURVE: a basis conic, line or other curve cut between two trims, each given
/// as a parameter, a Cartesian point, or both.</summary>
internal static class TrimmedCurves
{
    public static List<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity trimmed)
    {
        var basis = MeshHelpers.ResolveRequired(ctx, trimmed, IfcTrimmedCurve.Instance.BasisCurve);
        var sense = MeshHelpers.ReadOptionalBool(trimmed, IfcTrimmedCurve.Instance.SenseAgreement, true);
        return basis.GetEntityName() switch
        {
            "IFCCIRCLE" => TrimCircle2D(ctx, basis, trimmed, sense),
            "IFCELLIPSE" => TrimEllipse2D(ctx, basis, trimmed, sense),
            "IFCLINE" => TrimLine2D(ctx, basis, trimmed, sense),
            _ => TrimSampledPolyline2D(ctx, trimmed, CurveEvaluator.Evaluate2D(ctx, basis), sense),
        };
    }

    public static List<Vector3> Evaluate3D(MeshingContext ctx, IfcEntity trimmed)
    {
        var basis = MeshHelpers.ResolveRequired(ctx, trimmed, IfcTrimmedCurve.Instance.BasisCurve);
        var sense = MeshHelpers.ReadOptionalBool(trimmed, IfcTrimmedCurve.Instance.SenseAgreement, true);
        return basis.GetEntityName() switch
        {
            "IFCCIRCLE" => TrimCircle3D(ctx, basis, trimmed, sense),
            "IFCELLIPSE" => TrimEllipse3D(ctx, basis, trimmed, sense),
            "IFCLINE" => TrimLine3D(ctx, basis, trimmed, sense),
            _ => TrimSampledPolyline3D(ctx, trimmed, CurveEvaluator.Evaluate3D(ctx, basis).ToList(), sense),
        };
    }

    static List<Vector2> TrimCircle2D(MeshingContext ctx, IfcEntity circle, IfcEntity trimmed, bool sense)
    {
        var u1 = ReadTrimParameter(ctx, trimmed, circle, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, circle, IfcTrimmedCurve.Instance.Trim2);
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(circle, IfcCircle.Instance.Radius));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, circle, IfcConic.Instance.Position);
        return Conics.SampleTrimmedConicArc2D(placement, radius, radius, u1, u2, sense, ctx.CircleSegments);
    }

    static List<Vector3> TrimCircle3D(MeshingContext ctx, IfcEntity circle, IfcEntity trimmed, bool sense)
    {
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(circle, IfcCircle.Instance.Radius));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, circle, IfcConic.Instance.Position);
        // .CARTESIAN master representation gives trim endpoints as points, not angle parameters
        // (advanced-BRep cylindrical/curved face rim arcs use this). Derive angles from the points.
        if (PrefersCartesianTrim(trimmed) && TryReadTrimPoints3D(ctx, trimmed, out var cp1, out var cp2))
        {
            var a1 = CircleAngleAtPoint(placement, cp1);
            var a2 = CircleAngleAtPoint(placement, cp2);
            return Conics.SampleTrimmedConicArc3D(placement, radius, radius, a1, a2, sense, ctx.CircleSegments);
        }
        var u1 = ReadTrimParameter(ctx, trimmed, circle, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, circle, IfcTrimmedCurve.Instance.Trim2);
        return Conics.SampleTrimmedConicArc3D(placement, radius, radius, u1, u2, sense, ctx.CircleSegments);
    }

    static bool PrefersCartesianTrim(IfcEntity trimmed)
        => trimmed.GetString(IfcTrimmedCurve.Instance.MasterRepresentation.Index)
            .Contains("CARTESIAN", StringComparison.Ordinal);

    static float CircleAngleAtPoint(Frame3D placement, Vector3 world)
    {
        var local = placement.ToLocal(world);
        return MathF.Atan2(local.Y.Value, local.X.Value);
    }

    static List<Vector2> TrimEllipse2D(MeshingContext ctx, IfcEntity ellipse, IfcEntity trimmed, bool sense)
    {
        var u1 = ReadTrimParameter(ctx, trimmed, ellipse, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, ellipse, IfcTrimmedCurve.Instance.Trim2);
        var a = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis1));
        var b = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis2));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, ellipse, IfcConic.Instance.Position);
        return Conics.SampleTrimmedConicArc2D(placement, a, b, u1, u2, sense, ctx.CircleSegments);
    }

    static List<Vector3> TrimEllipse3D(MeshingContext ctx, IfcEntity ellipse, IfcEntity trimmed, bool sense)
    {
        var u1 = ReadTrimParameter(ctx, trimmed, ellipse, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, ellipse, IfcTrimmedCurve.Instance.Trim2);
        var a = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis1));
        var b = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis2));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, ellipse, IfcConic.Instance.Position);
        return Conics.SampleTrimmedConicArc3D(placement, a, b, u1, u2, sense, ctx.CircleSegments);
    }

    static List<Vector2> TrimLine2D(MeshingContext ctx, IfcEntity line, IfcEntity trimmed, bool sense)
    {
        var origin = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, line, IfcLine.Instance.Pnt));
        var dir = Conics.ReadLineDirection(ctx, line);
        var u1 = ReadTrimParameter(ctx, trimmed, line, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, line, IfcTrimmedCurve.Instance.Trim2);
        var p1 = origin + dir * u1;
        var p2 = origin + dir * u2;
        var pts = new List<Vector2> { new(p1.X, p1.Y), new(p2.X, p2.Y) };
        if (!sense)
            pts.Reverse();
        return pts;
    }

    static List<Vector3> TrimLine3D(MeshingContext ctx, IfcEntity line, IfcEntity trimmed, bool sense)
    {
        var origin = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, line, IfcLine.Instance.Pnt));
        var dir = Conics.ReadLineDirection(ctx, line);
        var u1 = ReadTrimParameter(ctx, trimmed, line, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, line, IfcTrimmedCurve.Instance.Trim2);
        var pts = new List<Vector3> { origin + dir * u1, origin + dir * u2 };
        if (!sense)
            pts.Reverse();
        return pts;
    }

    static List<Vector2> TrimSampledPolyline2D(MeshingContext ctx, IfcEntity trimmed, IReadOnlyList<Vector2> full, bool sense)
    {
        if (full.Count == 0)
            return [];

        if (TryReadTrimPoints2D(ctx, trimmed, out var p1, out var p2))
        {
            var i1 = NearestIndex(full, p1);
            var i2 = NearestIndex(full, p2);
            var list = i1 <= i2
                ? full.Skip(i1).Take(i2 - i1 + 1).ToList()
                : full.Skip(i1).Concat(full.Take(i2 + 1)).ToList();
            if (!sense)
                list.Reverse();
            return list;
        }

        var u1 = ReadTrimParameter(ctx, trimmed, null, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, null, IfcTrimmedCurve.Instance.Trim2);
        var start = (int)(u1 * (full.Count - 1));
        var end = (int)(u2 * (full.Count - 1));
        start = Math.Clamp(start, 0, full.Count - 1);
        end = Math.Clamp(end, 0, full.Count - 1);
        var slice = start <= end ? full.Skip(start).Take(end - start + 1) : full.Skip(start).Concat(full.Take(end + 1));
        var result = slice.ToList();
        if (!sense)
            result.Reverse();
        return result;
    }

    static List<Vector3> TrimSampledPolyline3D(MeshingContext ctx, IfcEntity trimmed, IReadOnlyList<Vector3> full, bool sense)
    {
        if (full.Count == 0)
            return [];

        if (TryReadTrimPoints3D(ctx, trimmed, out var p1, out var p2))
        {
            var i1 = NearestIndex3D(full, p1);
            var i2 = NearestIndex3D(full, p2);
            var list = i1 <= i2
                ? full.Skip(i1).Take(i2 - i1 + 1).ToList()
                : full.Skip(i1).Concat(full.Take(i2 + 1)).ToList();
            if (!sense)
                list.Reverse();
            return list;
        }

        var u1 = ReadTrimParameter(ctx, trimmed, null, IfcTrimmedCurve.Instance.Trim1);
        var u2 = ReadTrimParameter(ctx, trimmed, null, IfcTrimmedCurve.Instance.Trim2);
        var start = (int)(u1 * (full.Count - 1));
        var end = (int)(u2 * (full.Count - 1));
        start = Math.Clamp(start, 0, full.Count - 1);
        end = Math.Clamp(end, 0, full.Count - 1);
        var slice = start <= end ? full.Skip(start).Take(end - start + 1) : full.Skip(start).Concat(full.Take(end + 1));
        var result = slice.ToList();
        if (!sense)
            result.Reverse();
        return result;
    }

    /// <summary>The trim's parameter value: a number, a referenced measure, or a typed PARAMETER
    /// value; 0 when the trim has none (a point-only trim).</summary>
    public static float ReadTrimParameter(MeshingContext ctx, IfcEntity trimmed, IfcEntity? basisCurve, IfcAttribute attribute)
    {
        var token = trimmed.GetValue(attribute.Index);
        if (!token.IsList)
            return 0f;
        foreach (var item in token.AsList(trimmed.Document))
        {
            if (item.IsNumber)
                return ConvertTrimValue(ctx, trimmed, basisCurve, (float)item.AsNumber());
            if (item.IsId)
            {
                var value = ReadTrimScalar(ctx, ctx.GetEntity(item.AsId()));
                if (value.HasValue)
                    return ConvertTrimValue(ctx, trimmed, basisCurve, value.Value);
            }
            if (item.IsEntity)
            {
                var (name, value) = item.AsSimpleEntity(trimmed.Document);
                if (value.IsNumber)
                    return ConvertTrimValue(ctx, trimmed, basisCurve, (float)value.AsNumber());
                if (name.Contains("PARAMETER", StringComparison.Ordinal) && value.IsNumber)
                    return ConvertTrimValue(ctx, trimmed, basisCurve, (float)value.AsNumber());
            }
        }
        return 0f;
    }

    static float? ReadTrimScalar(MeshingContext ctx, IfcEntity entity)
    {
        for (var i = 0; i < entity.Attributes.Count; i++)
        {
            var token = entity.GetValue(i);
            if (token.IsNumber)
                return (float)token.AsNumber();
        }
        return null;
    }

    static float ConvertTrimValue(MeshingContext ctx, IfcEntity trimmed, IfcEntity? basisCurve, float value)
    {
        var master = trimmed.GetString(IfcTrimmedCurve.Instance.MasterRepresentation.Index);
        if (!master.Contains("PARAMETER", StringComparison.Ordinal))
            return value;

        if (basisCurve?.GetEntityName() is "IFCCIRCLE" or "IFCELLIPSE")
        {
            // Exporters disagree: some emit degrees (90, 270), others radians (π/2). Values above π
            // cannot be radians on a single trim, so treat them as degrees (PRIMARK fillet arcs).
            if (MathF.Abs(value) > MathF.PI + 1e-3f)
                return value * MathF.PI / 180f;
            return value;
        }

        return value;
    }

    static bool TryReadTrimPoints2D(MeshingContext ctx, IfcEntity trimmed, out Vector2 p1, out Vector2 p2)
    {
        p1 = default;
        p2 = default;
        var trim1 = trimmed.GetArray(IfcTrimmedCurve.Instance.Trim1.Index);
        var trim2 = trimmed.GetArray(IfcTrimmedCurve.Instance.Trim2.Index);
        if (trim1.Count == 0 || trim2.Count == 0 || !trim1[0].IsId || !trim2[0].IsId)
            return false;
        p1 = Placements.ReadPoint2D(ctx, ctx.GetEntity(trim1[0].AsId()));
        p2 = Placements.ReadPoint2D(ctx, ctx.GetEntity(trim2[0].AsId()));
        return true;
    }

    static bool TryReadTrimPoints3D(MeshingContext ctx, IfcEntity trimmed, out Vector3 p1, out Vector3 p2)
    {
        p1 = default;
        p2 = default;
        var trim1 = trimmed.GetArray(IfcTrimmedCurve.Instance.Trim1.Index);
        var trim2 = trimmed.GetArray(IfcTrimmedCurve.Instance.Trim2.Index);
        if (trim1.Count == 0 || trim2.Count == 0 || !trim1[0].IsId || !trim2[0].IsId)
            return false;
        p1 = Placements.ReadPoint3D(ctx, ctx.GetEntity(trim1[0].AsId()));
        p2 = Placements.ReadPoint3D(ctx, ctx.GetEntity(trim2[0].AsId()));
        return true;
    }

    static int NearestIndex(IReadOnlyList<Vector2> points, Vector2 target)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var d = points[i].DistanceSquared(target);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    static int NearestIndex3D(IReadOnlyList<Vector3> points, Vector3 target)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var d = points[i].DistanceSquared(target);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }
}
