using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>IFCBSPLINECURVE, IFCBSPLINECURVEWITHKNOTS and IFCRATIONALBSPLINECURVEWITHKNOTS, evaluated
/// from degree, knots, multiplicities and weights. A curve without knots (or with inconsistent ones)
/// gets clamped uniform knots.</summary>
public static class BSplineCurves
{
    public static List<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity bspline)
        => Evaluate3D(ctx, bspline).Select(p => new Vector2(p.X, p.Y)).ToList();

    public static List<Vector3> Evaluate3D(MeshingContext ctx, IfcEntity bspline)
    {
        var points = MeshHelpers.ReadIds(bspline, IfcBSplineCurve.Instance.ControlPointsList)
            .Select(id => Placements.ReadPoint3D(ctx, ctx.GetEntity(id)))
            .ToList();
        if (points.Count <= 1)
            return points;

        var curve = ReadDefinition(bspline, points);
        return BSplineEvaluator.Sample(curve, SampleCountFor(ctx, curve));
    }

    static SplineDefinition ReadDefinition(IfcEntity bspline, IReadOnlyList<Vector3> points)
    {
        var name = bspline.GetEntityName();
        var degree = (int)MeshHelpers.ReadNumber(bspline, IfcBSplineCurve.Instance.Degree);
        var weights = name == "IFCRATIONALBSPLINECURVEWITHKNOTS"
            ? MeshHelpers.ReadNumbers(bspline, IfcRationalBSplineCurveWithKnots.Instance.WeightsData)
            : null;
        var knots = name == "IFCBSPLINECURVE"
            ? []
            : SplineDefinition.ExpandKnots(
                MeshHelpers.ReadNumbers(bspline, IfcBSplineCurveWithKnots.Instance.Knots),
                MeshHelpers.ReadNumbers(bspline, IfcBSplineCurveWithKnots.Instance.KnotMultiplicities)
                    .Select(m => (int)m).ToList());

        var declared = new SplineDefinition(degree, knots, points, weights);
        if (declared.IsValid)
            return declared;
        var usable = weights?.Count == points.Count ? weights : null;
        var uniform = SplineDefinition.UniformClamped(degree, points, usable);
        return uniform.IsValid ? uniform : SplineDefinition.UniformClamped(1, points);
    }

    /// <summary>Same density rule as <c>Conics.ConicArcSampleCount</c>: <c>ctx.CircleSegments</c> per full
    /// turn, measured here as the control polygon's total turning angle (an upper bound on the curve's
    /// own), so a gentle curve gets few segments and a tight bend as many as an arc of that sweep.</summary>
    static SampleCount SampleCountFor(MeshingContext ctx, SplineDefinition curve)
        => SampleCount.FromTurning(TurningAngle(curve.ControlPoints), ctx.CircleSegments,
            BSplineEvaluator.SpanCount(curve), curve.Degree);

    static float TurningAngle(IReadOnlyList<Vector3> points)
    {
        var total = 0f;
        for (var i = 1; i + 1 < points.Count; i++)
        {
            var a = points[i] - points[i - 1];
            var b = points[i + 1] - points[i];
            var denominator = MathF.Sqrt(a.LengthSquared() * b.LengthSquared());
            if (denominator < 1e-12f)
                continue;
            total += MathF.Acos(Math.Clamp(Vector3.Dot(a, b) / denominator, -1f, 1f));
        }
        return total;
    }
}
