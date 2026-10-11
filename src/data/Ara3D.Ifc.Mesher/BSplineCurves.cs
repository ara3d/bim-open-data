using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>B-spline curves, approximated by sampling their control polygon; knots and weights are
/// not read (see GeometryCoverage).</summary>
internal static class BSplineCurves
{
    public static List<Vector2> Evaluate2D(MeshingContext ctx, IfcEntity bspline)
    {
        var points = MeshHelpers.ReadIds(bspline, IfcBSplineCurve.Instance.ControlPointsList)
            .Select(id => Placements.ReadPoint2D(ctx, ctx.GetEntity(id)))
            .ToList();
        return UniformSamplePolyline(points, Math.Max(points.Count * 2, 8));
    }

    public static List<Vector3> Evaluate3D(MeshingContext ctx, IfcEntity bspline)
    {
        var points = MeshHelpers.ReadIds(bspline, IfcBSplineCurve.Instance.ControlPointsList)
            .Select(id => Placements.ReadPoint3D(ctx, ctx.GetEntity(id)))
            .ToList();
        return UniformSamplePolyline3D(points, Math.Max(points.Count * 2, 8));
    }

    static List<Vector3> UniformSamplePolyline3D(IReadOnlyList<Vector3> control, int samples)
    {
        if (control.Count <= 1)
            return control.ToList();
        var result = new List<Vector3>(samples);
        for (var i = 0; i < samples; i++)
        {
            var t = (float)i / (samples - 1) * (control.Count - 1);
            var idx = (int)t;
            var frac = t - idx;
            var a = control[Math.Min(idx, control.Count - 1)];
            var b = control[Math.Min(idx + 1, control.Count - 1)];
            result.Add(a + (b - a) * frac);
        }
        return result;
    }

    static List<Vector2> UniformSamplePolyline(IReadOnlyList<Vector2> control, int samples)
    {
        if (control.Count <= 1)
            return control.ToList();
        var result = new List<Vector2>(samples);
        for (var i = 0; i < samples; i++)
        {
            var t = (float)i / (samples - 1) * (control.Count - 1);
            var idx = (int)t;
            var frac = t - idx;
            var a = control[Math.Min(idx, control.Count - 1)];
            var b = control[Math.Min(idx + 1, control.Count - 1)];
            result.Add(a + (b - a) * frac);
        }
        return result;
    }
}
