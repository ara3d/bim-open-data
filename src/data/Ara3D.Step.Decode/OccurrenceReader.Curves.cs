using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

public sealed partial class OccurrenceReader
{
    /// <summary>A space curve: one BrepCurve case per Part 42 curve, placed, lengths in millimetres, conic trims in
    /// radians. Composite segments are consumed by their composite; a surface curve's pcurves become Pcurve rows.</summary>
    bool Curve(StepEntity curve)
        => _curves.Reach(curve.Id, () => CurveRow(curve), Failure);

    CurveIndex CurveIndexOf(int id)
        => new(_curves.IndexOf(id));

    Func<BrepCurve> CurveRow(StepEntity c)
    {
        if (c.Is<ST.ILine>())
        {
            var row = BrepCurve.Line(P(c.PointAt(ST.Line.Instance.Pnt)), V(c.Ref(ST.Line.Instance.Dir)!.Value.VectorOf()));
            return () => row;
        }
        if (c.Is<ST.IConic>())
            return ConicRow(c);
        if (c.Is<ST.IPolyline>())
        {
            var row = BrepCurve.Polyline((c.Refs(ST.Polyline.Instance.Points) ?? []).Select(p => P(p.PointOf())).ToArray());
            return () => row;
        }
        if (c.Is<ST.IBSplineCurve>())
        {
            var row = BrepCurve.BSpline(SplineCurve(c));
            return () => row;
        }
        if (c.Is<ST.ITrimmedCurve>())
            return TrimmedRow(c);
        if (c.Is<ST.ICompositeCurve>())
        {
            var segments = Segments(c, s => Need(Curve(s), s.Id));
            return () => BrepCurve.Composite(segments.Select(s => new CompositeSegment(CurveIndexOf(s.Curve), s.SameSense)).ToArray());
        }
        if (c.Is<ST.IOffsetCurve3d>())
        {
            var o = ST.OffsetCurve3d.Instance;
            var basis = c.Ref(o.BasisCurve)!.Value;
            Need(Curve(basis), basis.Id);
            var distance = Length(c.Number(o.Distance) ?? 0);
            var reference = D(c.Ref(o.RefDirection)!.Value.DirectionOf());
            return () => BrepCurve.Offset(CurveIndexOf(basis.Id), distance, reference);
        }
        if (c.Is<ST.ISurfaceCurve>())
            return SurfaceCurveRow(c);
        throw Unsupported(c);
    }

    Func<BrepCurve> ConicRow(StepEntity c)
    {
        var frame = Frame(c.Ref(ST.Conic.Instance.Position)!.Value.Axis2Placement3D(_findings));
        BrepCurve row;
        if (c.Is<ST.ICircle>())
            row = BrepCurve.Circle(frame, Length(c.Number(ST.Circle.Instance.Radius) ?? 0));
        else if (c.Is<ST.IEllipse>())
            row = BrepCurve.Ellipse(frame, Length(c.Number(ST.Ellipse.Instance.SemiAxis1) ?? 0), Length(c.Number(ST.Ellipse.Instance.SemiAxis2) ?? 0));
        else if (c.Is<ST.IHyperbola>())
            row = BrepCurve.Hyperbola(frame, Length(c.Number(ST.Hyperbola.Instance.SemiAxis) ?? 0), Length(c.Number(ST.Hyperbola.Instance.SemiImagAxis) ?? 0));
        else if (c.Is<ST.IParabola>())
            row = BrepCurve.Parabola(frame, Length(c.Number(ST.Parabola.Instance.FocalDist) ?? 0));
        else
            throw Unsupported(c);
        return () => row;
    }

    BSplineCurveData SplineCurve(StepEntity c)
    {
        var b = ST.BSplineCurve.Instance;
        var degree = (int)(c.Integer(b.Degree) ?? 0);
        var points = (c.Refs(b.ControlPointsList) ?? []).Select(p => P(p.PointOf())).ToArray();
        var weights = c.CurveWeights(points.Length).Select(w => (Number)w).ToArray();
        return new BSplineCurveData(SplineKnots.Basis(degree, c.CurveKnots(degree, points.Length)), points, weights);
    }

    Func<BrepCurve> TrimmedRow(StepEntity c)
    {
        var t = ST.TrimmedCurve.Instance;
        var basis = c.Ref(t.BasisCurve)!.Value;
        Need(Curve(basis), basis.Id);
        var range = TrimRange(c, basis);
        var sense = c.Boolean(t.SenseAgreement) ?? true;
        return () => BrepCurve.Trimmed(CurveIndexOf(basis.Id), range, sense);
    }

    /// <summary>The parameter range of a trimmed curve from its parameter_value trims, in radians on a conic;
    /// a trim given only by points stops the row (finding its parameter is a projection, which is geometry).</summary>
    NumberInterval TrimRange(StepEntity trimmed, StepEntity basis)
    {
        var t = ST.TrimmedCurve.Instance;
        if (trimmed.TrimParameter(t.Trim1) is not { } start || trimmed.TrimParameter(t.Trim2) is not { } end)
            throw Unsupported(trimmed);
        var factor = ParameterFactor(basis);
        return new NumberInterval(start * factor, end * factor);
    }

    /// <summary>What a curve's parameter is multiplied by: radians per angle unit for a circle or ellipse (and a
    /// trimmed or surface curve on one), 1 for every other curve, whose parameter is dimensionless.</summary>
    double ParameterFactor(StepEntity curve)
        => curve.Is<ST.ICircle>() || curve.Is<ST.IEllipse>() ? _radiansPerUnit
            : curve.Is<ST.ITrimmedCurve>() ? ParameterFactor(curve.Ref(ST.TrimmedCurve.Instance.BasisCurve)!.Value)
            : curve.Is<ST.ISurfaceCurve>() && curve.Ref(ST.SurfaceCurve.Instance.Curve3d) is { } c3 ? ParameterFactor(c3)
            : 1;

    static IReadOnlyList<(int Curve, bool SameSense)> Segments(StepEntity composite, Action<StepEntity> need)
        => (composite.Refs(ST.CompositeCurve.Instance.Segments) ?? []).Select(s =>
        {
            var parent = s.Ref(ST.CompositeCurveSegment.Instance.ParentCurve)!.Value;
            need(parent);
            return (parent.Id, s.Boolean(ST.CompositeCurveSegment.Instance.SameSense) ?? true);
        }).ToArray();

    /// <summary>surface_curve, seam_curve, intersection_curve, bounded_surface_curve: the space curve and its one
    /// or two supports, a pcurve or a bare surface each.</summary>
    Func<BrepCurve> SurfaceCurveRow(StepEntity c)
    {
        var s = ST.SurfaceCurve.Instance;
        var curve3D = c.Ref(s.Curve3d) ?? throw new DecodeException(StepDiagnosticCode.MissingCurve3D(), c.Id);
        Need(Curve(curve3D), curve3D.Id);
        var supports = new List<(bool IsPcurve, int Id)>();
        foreach (var g in c.Refs(s.AssociatedGeometry) ?? [])
        {
            var isPcurve = g.Is<ST.IPcurve>();
            Need(isPcurve ? Pcurve(g) : Surface(g), g.Id);
            supports.Add((isPcurve, g.Id));
        }
        var master = c.Enum(s.MasterRepresentation) switch
        {
            ST.PreferredSurfaceCurveRepresentation.PcurveS1 => PreferredRepresentation.FirstPcurve(),
            ST.PreferredSurfaceCurveRepresentation.PcurveS2 => PreferredRepresentation.SecondPcurve(),
            _ => PreferredRepresentation.SpaceCurve(),
        };
        return () => BrepCurve.OnSurface(CurveIndexOf(curve3D.Id), supports.Select(g => g.IsPcurve
            ? CurveSupport.Parametric(new PcurveIndex(_pcurves.IndexOf(g.Id)))
            : CurveSupport.SurfaceOnly(new SurfaceIndex(_surfaces.IndexOf(g.Id)))).ToArray(), master);
    }
}
