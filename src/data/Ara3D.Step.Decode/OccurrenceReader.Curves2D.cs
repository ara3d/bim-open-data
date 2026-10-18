using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

public sealed partial class OccurrenceReader
{
    /// <summary>A pcurve: its surface, and the 2D curve its definitional_representation holds, in the surface's
    /// natural parameters (Plato's BrepCurve2D: radians along an angle, millimetres along a length).</summary>
    bool Pcurve(StepEntity pcurve)
        => _pcurves.Reach(pcurve.Id, () =>
        {
            var p = ST.Pcurve.Instance;
            var surface = pcurve.Ref(p.BasisSurface)!.Value;
            Need(Surface(surface), surface.Id);
            var definition = pcurve.Ref(p.ReferenceToCurve)!.Value;
            var items = definition.Refs(ST.Representation.Instance.Items) ?? [];
            if (items.Count == 0)
                throw Unsupported(definition);
            var curve = items[0];
            var (fu, fv) = ParameterFactors(surface);
            Need(Curve2D(curve, fu, fv), curve.Id);
            return () => new Pcurve(SurfaceIndexOf(surface.Id), new Curve2DIndex(_curves2D.IndexOf(curve.Id)));
        }, Failure);

    /// <summary>A parameter-plane curve with its u coordinates times <paramref name="fu"/> and v times
    /// <paramref name="fv"/>. A conic needs the two equal (a circle scaled unevenly is no circle).</summary>
    bool Curve2D(StepEntity curve, double fu, double fv)
        => _curves2D.Reach(curve.Id, () => Curve2DRow(curve, fu, fv), Failure);

    Func<BrepCurve2D> Curve2DRow(StepEntity c, double fu, double fv)
    {
        UvCoordinate Uv(Vec3 p) => new(p.X * fu, p.Y * fv);
        if (c.Is<ST.ILine>())
        {
            var v = c.Ref(ST.Line.Instance.Dir)!.Value.VectorOf();
            var row = BrepCurve2D.Line(Uv(c.PointAt(ST.Line.Instance.Pnt)), new Vector2D(v.X * fu, v.Y * fv));
            return () => row;
        }
        if (c.Is<ST.IConic>())
        {
            if (fu != fv)
                throw Unsupported(c);
            var f = c.Ref(ST.Conic.Instance.Position)!.Value.Axis2Placement2D();
            var frame = new Frame2D(new Point2D(f.Origin.X * fu, f.Origin.Y * fu), new Direction2D(new Vector2D(f.X.X, f.X.Y)), new Direction2D(new Vector2D(f.Y.X, f.Y.Y)));
            var row = Conic2D(c, frame, fu);
            return () => row;
        }
        if (c.Is<ST.IPolyline>())
        {
            var row = BrepCurve2D.Polyline((c.Refs(ST.Polyline.Instance.Points) ?? []).Select(p => Uv(p.PointOf())).ToArray());
            return () => row;
        }
        if (c.Is<ST.IBSplineCurve>())
        {
            var b = ST.BSplineCurve.Instance;
            var degree = (int)(c.Integer(b.Degree) ?? 0);
            var points = (c.Refs(b.ControlPointsList) ?? []).Select(p => Uv(p.PointOf())).ToArray();
            var weights = c.CurveWeights(points.Length).Select(w => (Number)w).ToArray();
            var row = BrepCurve2D.BSpline(new BSplineCurve2DData(SplineKnots.Basis(degree, c.CurveKnots(degree, points.Length)), points, weights));
            return () => row;
        }
        if (c.Is<ST.ITrimmedCurve>())
        {
            var basis = c.Ref(ST.TrimmedCurve.Instance.BasisCurve)!.Value;
            Need(Curve2D(basis, fu, fv), basis.Id);
            var range = TrimRange(c, basis);
            var sense = c.Boolean(ST.TrimmedCurve.Instance.SenseAgreement) ?? true;
            return () => BrepCurve2D.Trimmed(new Curve2DIndex(_curves2D.IndexOf(basis.Id)), range, sense);
        }
        if (c.Is<ST.ICompositeCurve>())
        {
            var segments = Segments(c, s => Need(Curve2D(s, fu, fv), s.Id));
            return () => BrepCurve2D.Composite(segments.Select(s => new CompositeSegment2D(new Curve2DIndex(_curves2D.IndexOf(s.Curve)), s.SameSense)).ToArray());
        }
        if (c.Is<ST.IOffsetCurve2d>() && fu == fv)
        {
            var basis = c.Ref(ST.OffsetCurve2d.Instance.BasisCurve)!.Value;
            Need(Curve2D(basis, fu, fv), basis.Id);
            var distance = (c.Number(ST.OffsetCurve2d.Instance.Distance) ?? 0) * fu;
            return () => BrepCurve2D.Offset(new Curve2DIndex(_curves2D.IndexOf(basis.Id)), distance);
        }
        throw Unsupported(c);
    }

    static BrepCurve2D Conic2D(StepEntity c, Frame2D frame, double scale)
    {
        if (c.Is<ST.ICircle>())
            return BrepCurve2D.Circle(frame, (c.Number(ST.Circle.Instance.Radius) ?? 0) * scale);
        if (c.Is<ST.IEllipse>())
            return BrepCurve2D.Ellipse(frame, (c.Number(ST.Ellipse.Instance.SemiAxis1) ?? 0) * scale, (c.Number(ST.Ellipse.Instance.SemiAxis2) ?? 0) * scale);
        if (c.Is<ST.IHyperbola>())
            return BrepCurve2D.Hyperbola(frame, (c.Number(ST.Hyperbola.Instance.SemiAxis) ?? 0) * scale, (c.Number(ST.Hyperbola.Instance.SemiImagAxis) ?? 0) * scale);
        if (c.Is<ST.IParabola>())
            return BrepCurve2D.Parabola(frame, (c.Number(ST.Parabola.Instance.FocalDist) ?? 0) * scale);
        throw Unsupported(c);
    }
}
