namespace Ara3D.MeshKernel;

/// <summary>Knot vectors of B-spline curves and surfaces.</summary>
public static class BSplineKnots
{
    /// <summary>Repeats each distinct knot by its multiplicity, giving the full knot vector.</summary>
    public static IReadOnlyList<float> Expand(IReadOnlyList<double> knots, IReadOnlyList<double> multiplicities)
    {
        var full = new List<float>();
        for (var i = 0; i < knots.Count && i < multiplicities.Count; i++)
            for (var m = 0; m < (int)multiplicities[i]; m++)
                full.Add((float)knots[i]);
        return full;
    }
}
