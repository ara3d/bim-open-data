using Ara3D.Geometry;
using Ara3D.IfcLoader;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Evaluates IFC curves to polylines, dispatching by entity name to <see cref="PolyCurves"/>,
/// <see cref="Conics"/>, <see cref="TrimmedCurves"/>, <see cref="CompositeCurves"/> and
/// <see cref="BSplineCurves"/>. Arcs and B-splines are uniformly sampled (approximate).
/// </summary>
public static class CurveEvaluator
{
    static float DefaultJoinToleranceSquared => Tolerance.Eps * Tolerance.Eps;

    /// <summary>Two consecutive points of a composite curve closer than 5 % of a model unit join.</summary>
    internal static float CompositeJoinToleranceSquared(MeshingContext ctx)
    {
        var tol = Math.Max(Tolerance.Eps, MathF.Abs((float)ctx.LengthScale) * 0.05f);
        return tol * tol;
    }

    public static float JoinToleranceSquaredFor(MeshingContext ctx) => CompositeJoinToleranceSquared(ctx);

    public static IReadOnlyList<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity curve)
        => Evaluate2D(ctx, curve, false);

    public static IReadOnlyList<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity curve, bool dropClosure)
    {
        var points = curve.GetEntityName() switch
        {
            "IFCPOLYLINE" => PolyCurves.EvaluatePolyline2D(ctx, curve),
            "IFCINDEXEDPOLYCURVE" => PolyCurves.EvaluateIndexedPolyCurve2D(ctx, curve),
            "IFCCIRCLE" => Conics.EvaluateCircle2D(ctx, curve, full: true),
            "IFCELLIPSE" => Conics.EvaluateEllipse2D(ctx, curve, full: true),
            "IFCLINE" => Conics.EvaluateLine2D(ctx, curve),
            "IFCTRIMMEDCURVE" => TrimmedCurves.Evaluate2D(ctx, curve),
            "IFCCOMPOSITECURVE" => CompositeCurves.Evaluate2D(ctx, curve),
            "IFCBSPLINECURVEWITHKNOTS" or "IFCRATIONALBSPLINECURVEWITHKNOTS" or "IFCBSPLINECURVE" => BSplineCurves.Evaluate2D(ctx, curve),
            _ => throw new NotSupportedException($"Unsupported 2D curve {curve.GetEntityName()} #{curve.Id}"),
        };

        ctx.Diagnostics.RecordSupported(curve.GetEntityName());
        var list = dropClosure ? SanitizeRingPoints(points) : SanitizeOpenPathPoints(points);
        if (dropClosure)
            RemoveDuplicateClosure(list);
        return list;
    }

    public static IReadOnlyList<Vector3> Evaluate3D(MeshingContext ctx, IfcEntity curve)
    {
        var points = curve.GetEntityName() switch
        {
            "IFCPOLYLINE" => PolyCurves.EvaluatePolyline3D(ctx, curve),
            "IFCINDEXEDPOLYCURVE" => PolyCurves.EvaluateIndexedPolyCurve3D(ctx, curve),
            "IFCCIRCLE" => Conics.EvaluateCircle3D(ctx, curve, full: true),
            "IFCELLIPSE" => Conics.EvaluateEllipse3D(ctx, curve, full: true),
            "IFCLINE" => Conics.EvaluateLine3D(ctx, curve),
            "IFCTRIMMEDCURVE" => TrimmedCurves.Evaluate3D(ctx, curve),
            "IFCCOMPOSITECURVE" => CompositeCurves.Evaluate3D(ctx, curve),
            "IFCBSPLINECURVEWITHKNOTS" or "IFCRATIONALBSPLINECURVEWITHKNOTS" or "IFCBSPLINECURVE" => BSplineCurves.Evaluate3D(ctx, curve),
            _ => throw new NotSupportedException($"Unsupported 3D curve {curve.GetEntityName()} #{curve.Id}"),
        };
        ctx.Diagnostics.RecordSupported(curve.GetEntityName());
        return points;
    }

    /// <summary>Deduplicates consecutive points and drops a last point that closes back on the first.</summary>
    internal static List<Vector2> SanitizeRingPoints(IReadOnlyList<Vector2> points, float joinToleranceSquared = 0)
    {
        var result = SanitizeOpenPathPoints(points, joinToleranceSquared);
        if (joinToleranceSquared <= 0)
            joinToleranceSquared = DefaultJoinToleranceSquared;
        if (result.Count >= 2 && result[0].DistanceSquared(result[^1]) <= joinToleranceSquared)
            result.RemoveAt(result.Count - 1);
        return result;
    }

    /// <summary>Deduplicates consecutive points without treating near-coincident endpoints as closure.</summary>
    internal static List<Vector2> SanitizeOpenPathPoints(IReadOnlyList<Vector2> points, float joinToleranceSquared = 0)
    {
        if (joinToleranceSquared <= 0)
            joinToleranceSquared = DefaultJoinToleranceSquared;
        var result = new List<Vector2>(points.Count);
        foreach (var p in points)
        {
            if (result.Count == 0 || result[^1].DistanceSquared(p) > joinToleranceSquared)
                result.Add(p);
        }
        return result;
    }

    static void RemoveDuplicateClosure(List<Vector2> points, float joinToleranceSquared = 0)
    {
        var cleaned = PolygonWithHoles.CleanRing(points, joinToleranceSquared);
        points.Clear();
        points.AddRange(cleaned);
    }
}

internal static class Vector3Extensions
{
    public static Vector2 Vector2(this Vector3 v) => new(v.X, v.Y);
}
