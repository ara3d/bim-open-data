using Ara3D.Geometry;

namespace Ara3D.Ifc.Mesher;

/// <summary>A B-spline (or NURBS, when weights are present) curve: degree, the full knot vector with
/// repeats written out, control points, and optional weights, one per control point.</summary>
public readonly record struct SplineDefinition(
    int Degree,
    IReadOnlyList<double> Knots,
    IReadOnlyList<Vector3> ControlPoints,
    IReadOnlyList<double>? Weights)
{
    /// <summary>A valid curve has degree at least 1, more control points than the degree, exactly
    /// control points + degree + 1 non-decreasing knots, and a domain of positive length.</summary>
    public bool IsValid =>
        Degree >= 1
        && ControlPoints.Count > Degree
        && Knots.Count == ControlPoints.Count + Degree + 1
        && (Weights is null || Weights.Count == ControlPoints.Count)
        && Knots.Zip(Knots.Skip(1), (a, b) => a <= b).All(ok => ok)
        && Knots[Degree] < Knots[ControlPoints.Count];

    public double DomainStart => Knots[Degree];
    public double DomainEnd => Knots[ControlPoints.Count];

    /// <summary>Knots from distinct values and their multiplicities: values (0, 1) with multiplicities
    /// (3, 3) give 0 0 0 1 1 1.</summary>
    public static List<double> ExpandKnots(IReadOnlyList<double> values, IReadOnlyList<int> multiplicities)
    {
        var result = new List<double>();
        for (var i = 0; i < values.Count && i < multiplicities.Count; i++)
            result.AddRange(Enumerable.Repeat(values[i], Math.Max(0, multiplicities[i])));
        return result;
    }

    /// <summary>Clamped uniform knots: the curve starts and ends at the first and last control point.
    /// The degree is lowered when there are too few control points for it.</summary>
    public static SplineDefinition UniformClamped(int degree, IReadOnlyList<Vector3> points, IReadOnlyList<double>? weights = null)
    {
        degree = Math.Clamp(degree, 1, Math.Max(1, points.Count - 1));
        var interior = Math.Max(0, points.Count - degree - 1);
        var knots = new List<double>();
        knots.AddRange(Enumerable.Repeat(0.0, degree + 1));
        for (var i = 1; i <= interior; i++)
            knots.Add(i);
        knots.AddRange(Enumerable.Repeat((double)interior + 1, degree + 1));
        return new SplineDefinition(degree, knots, points, weights);
    }
}

/// <summary>How many equal parameter steps a curve is cut into.</summary>
public readonly record struct SampleCount(int Segments)
{
    /// <summary>Segments for a curve that turns through <paramref name="turningRadians"/>:
    /// the larger of the per-quadrant rule (<paramref name="segmentsPerCircle"/> / 4 segments per
    /// quarter turn, at least 2) and 2 segments per degree per span, capped at 256.</summary>
    public static SampleCount FromTurning(float turningRadians, int segmentsPerCircle, int spans, int degree)
    {
        var perQuadrant = Math.Max(2, segmentsPerCircle / 4);
        var fromTurning = (int)MathF.Ceiling(MathF.Abs(turningRadians) / (MathF.PI / 2f) * perQuadrant);
        var fromSpans = Math.Max(1, spans) * Math.Max(2, degree) * 2;
        return new SampleCount(Math.Clamp(Math.Max(fromTurning, fromSpans), 2, 256));
    }
}

/// <summary>Cox-de Boor evaluation of B-spline and rational B-spline curves. No IFC types.</summary>
public static class BSplineEvaluator
{
    /// <summary>The point at parameter <paramref name="t"/>, clamped to the curve's domain.</summary>
    public static Vector3 Evaluate(SplineDefinition curve, double t)
    {
        t = Math.Clamp(t, curve.DomainStart, curve.DomainEnd);
        var span = FindSpan(curve, t);
        var basis = BasisFunctions(curve, span, t);
        double x = 0, y = 0, z = 0, w = 0;
        for (var j = 0; j <= curve.Degree; j++)
        {
            var index = span - curve.Degree + j;
            var weight = basis[j] * (curve.Weights?[index] ?? 1.0);
            var p = curve.ControlPoints[index];
            x += weight * p.X.Value;
            y += weight * p.Y.Value;
            z += weight * p.Z.Value;
            w += weight;
        }
        return new Vector3((float)(x / w), (float)(y / w), (float)(z / w));
    }

    /// <summary>Segments + 1 points at equal parameter steps, both ends included.</summary>
    public static List<Vector3> Sample(SplineDefinition curve, SampleCount count)
    {
        var result = new List<Vector3>(count.Segments + 1);
        for (var i = 0; i <= count.Segments; i++)
        {
            var t = curve.DomainStart + (curve.DomainEnd - curve.DomainStart) * i / count.Segments;
            result.Add(Evaluate(curve, t));
        }
        return result;
    }

    /// <summary>Number of non-empty knot spans in the curve's domain.</summary>
    public static int SpanCount(SplineDefinition curve)
    {
        var spans = 0;
        for (var i = curve.Degree; i < curve.ControlPoints.Count; i++)
            if (curve.Knots[i + 1] > curve.Knots[i])
                spans++;
        return spans;
    }

    /// <summary>The index i with knots[i] &lt;= t &lt; knots[i + 1]; at the domain end, the last non-empty span.</summary>
    static int FindSpan(SplineDefinition curve, double t)
    {
        var last = curve.ControlPoints.Count - 1;
        if (t >= curve.Knots[last + 1])
        {
            var i = last;
            while (i > curve.Degree && curve.Knots[i] >= curve.Knots[last + 1])
                i--;
            return i;
        }
        var span = curve.Degree;
        while (span < last && t >= curve.Knots[span + 1])
            span++;
        return span;
    }

    static double[] BasisFunctions(SplineDefinition curve, int span, double t)
    {
        var p = curve.Degree;
        var knots = curve.Knots;
        var basis = new double[p + 1];
        var left = new double[p + 1];
        var right = new double[p + 1];
        basis[0] = 1.0;
        for (var j = 1; j <= p; j++)
        {
            left[j] = t - knots[span + 1 - j];
            right[j] = knots[span + j] - t;
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                var temp = basis[r] / (right[r + 1] + left[j - r]);
                basis[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            basis[j] = saved;
        }
        return basis;
    }
}
