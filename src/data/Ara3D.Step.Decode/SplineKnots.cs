using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>The expanded knot vectors and weights of STEP's B-spline families (ISO 10303-42 4.5.34 to 4.5.40):
/// distinct knots repeated by their multiplicities, or the knots the uniform, quasi-uniform and Bezier forms
/// imply, copied as written (no repair); weights from the rational part, all 1 otherwise.</summary>
public static class SplineKnots
{
    public static SplineBasis Basis(int degree, IReadOnlyList<double> knots)
        => new(degree, new KnotVector(knots.Select(k => (Number)k).ToArray()));

    /// <summary>The knots of a curve with <paramref name="count"/> control points.</summary>
    public static IReadOnlyList<double> CurveKnots(this StepEntity curve, int degree, int count)
    {
        if (curve.Is<ST.IBSplineCurveWithKnots>())
            return Expand(curve.Numbers(ST.BSplineCurveWithKnots.Instance.Knots), curve.Integers(ST.BSplineCurveWithKnots.Instance.KnotMultiplicities), curve.Id);
        return Implied(curve.Is<ST.IUniformCurve>(), curve.Is<ST.IQuasiUniformCurve>(), curve.Is<ST.IBezierCurve>(), degree, count)
            ?? throw new DecodeException(StepDiagnosticCode.InstanceSkipped(), curve.Id);
    }

    public static IReadOnlyList<double> CurveWeights(this StepEntity curve, int count)
        => curve.Is<ST.IRationalBSplineCurve>() && curve.Numbers(ST.RationalBSplineCurve.Instance.WeightsData) is { } w
            ? w
            : Enumerable.Repeat(1.0, count).ToArray();

    /// <summary>The u (<paramref name="u"/> true) or v knots of a surface with <paramref name="count"/> control
    /// points along that parameter.</summary>
    public static IReadOnlyList<double> SurfaceKnots(this StepEntity surface, bool u, int degree, int count)
    {
        var k = ST.BSplineSurfaceWithKnots.Instance;
        if (surface.Is<ST.IBSplineSurfaceWithKnots>())
            return Expand(surface.Numbers(u ? k.UKnots : k.VKnots), surface.Integers(u ? k.UMultiplicities : k.VMultiplicities), surface.Id);
        return Implied(surface.Is<ST.IUniformSurface>(), surface.Is<ST.IQuasiUniformSurface>(), surface.Is<ST.IBezierSurface>(), degree, count)
            ?? throw new DecodeException(StepDiagnosticCode.InstanceSkipped(), surface.Id);
    }

    /// <summary>The weights of a surface, [u index][v index], all 1 when it is not rational.</summary>
    public static IReadOnlyList<IReadOnlyList<double>>? SurfaceWeights(this StepEntity surface)
        => surface.Is<ST.IRationalBSplineSurface>() ? surface.Numbers(ST.RationalBSplineSurface.Instance.WeightsData) : null;

    static IReadOnlyList<double> Expand(IReadOnlyList<double>? knots, IReadOnlyList<long>? multiplicities, int id)
    {
        if (knots is null || multiplicities is null || knots.Count != multiplicities.Count)
            throw new DecodeException(StepDiagnosticCode.InstanceSkipped(), id);
        var expanded = new List<double>();
        for (var i = 0; i < knots.Count; i++)
            for (var m = 0; m < multiplicities[i]; m++)
                expanded.Add(knots[i]);
        return expanded;
    }

    /// <summary>The default knots of ISO 10303-42 for n control points of degree d: uniform -d, -d + 1, ...;
    /// quasi-uniform 0 repeated d + 1 times, 1, 2, ..., then n - d repeated d + 1 times; Bezier 0 and the
    /// segment count repeated d + 1 times, the interior segment ends d times.</summary>
    static IReadOnlyList<double>? Implied(bool uniform, bool quasiUniform, bool bezier, int d, int n)
    {
        if (uniform)
            return Enumerable.Range(0, n + d + 1).Select(i => (double)(i - d)).ToArray();
        if (quasiUniform)
            return Enumerable.Repeat(0.0, d + 1).Concat(Enumerable.Range(1, Math.Max(0, n - d - 1)).Select(i => (double)i))
                .Concat(Enumerable.Repeat((double)(n - d), d + 1)).ToArray();
        if (bezier && d > 0)
        {
            var segments = (n - 1) / d;
            return Enumerable.Repeat(0.0, d + 1).Concat(Enumerable.Range(1, Math.Max(0, segments - 1)).SelectMany(s => Enumerable.Repeat((double)s, d)))
                .Concat(Enumerable.Repeat((double)segments, d + 1)).ToArray();
        }
        return null;
    }
}
