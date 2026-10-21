using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>Names of the B-rep curve and surface kinds (the discriminants of BrepCurve and BrepSurface), and the
/// faces that cite each edge, for the timing tables.</summary>
public static class Kinds
{
    static readonly string[] CurveNames =
        ["line", "circle", "ellipse", "hyperbola", "parabola", "polyline", "bspline", "trimmed", "composite", "offset", "on-surface"];

    static readonly string[] SurfaceNames =
        ["plane", "cylinder", "cone", "sphere", "torus", "revolution", "extrusion", "bspline", "rect-trimmed", "offset"];

    public static string Name(this BrepCurve c)
        => c.Kind < CurveNames.Length ? CurveNames[c.Kind] : $"curve{c.Kind}";

    public static string Name(this BrepSurface s)
        => s.Kind < SurfaceNames.Length ? SurfaceNames[s.Kind] : $"surface{s.Kind}";

    public static bool IsLine(this BrepModel m, BrepEdgeIndex e)
        => m.Geometry.Curves[m.Edges[e.Value].Curve.Value].Kind == BrepCurve.Kind_Line;

    public static string CurveName(this BrepModel m, BrepEdgeIndex e)
        => m.Geometry.Curves[m.Edges[e.Value].Curve.Value].Name();

    public static string SurfaceName(this BrepModel m, BrepFaceIndex f)
        => m.Geometry.Surfaces[m.Faces[f.Value].Surface.Value].Name();

    /// <summary>The edges of every face, computed once.</summary>
    public static IReadOnlyList<BrepEdgeIndex>[] EdgesByFace(this BrepModel m)
    {
        var edges = new IReadOnlyList<BrepEdgeIndex>[m.Faces.Count];
        for (var f = 0; f < edges.Length; f++)
            edges[f] = m.FaceEdges(new BrepFaceIndex(f));
        return edges;
    }

    /// <summary>For each edge, the sorted surface kinds of the faces that cite it, joined by '+'.</summary>
    public static string[] AdjacentSurfaces(this BrepModel m, IReadOnlyList<BrepEdgeIndex>[] edgesByFace)
    {
        var kinds = new List<string>[m.Edges.Count];
        for (var f = 0; f < edgesByFace.Length; f++)
        {
            var name = m.SurfaceName(new BrepFaceIndex(f));
            foreach (var e in edgesByFace[f].Select(e => e.Value).Distinct())
                (kinds[e] ??= []).Add(name);
        }
        return kinds.Select(k => k is null ? "none" : string.Join('+', k.Order())).ToArray();
    }
}
