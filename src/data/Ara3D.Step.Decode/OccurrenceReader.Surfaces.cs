using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

public sealed partial class OccurrenceReader
{
    /// <summary>A surface: one BrepSurface case per Part 42 surface, placed, lengths in millimetres, angles in radians.</summary>
    bool Surface(StepEntity surface)
        => _surfaces.Reach(surface.Id, () => SurfaceRow(surface), Failure);

    SurfaceIndex SurfaceIndexOf(int id)
        => new(_surfaces.IndexOf(id));

    Func<BrepSurface> SurfaceRow(StepEntity s)
    {
        if (s.Is<ST.IElementarySurface>())
        {
            var row = ElementaryRow(s, Frame(s.Ref(ST.ElementarySurface.Instance.Position)!.Value.Axis2Placement3D(_findings)));
            return () => row;
        }
        if (s.Is<ST.ISurfaceOfRevolution>())
        {
            var profile = s.Ref(ST.SweptSurface.Instance.SweptCurve)!.Value;
            Need(Curve(profile), profile.Id);
            var axis = s.Ref(ST.SurfaceOfRevolution.Instance.AxisPosition)!.Value;
            var line = new InfiniteLine3D(P(axis.PointAt(ST.Axis1Placement.Instance.Location)), D(axis.DirectionAt(ST.Axis1Placement.Instance.Axis) ?? new Vec3(0, 0, 1)));
            return () => BrepSurface.Revolution(CurveIndexOf(profile.Id), line);
        }
        if (s.Is<ST.ISurfaceOfLinearExtrusion>())
        {
            var profile = s.Ref(ST.SweptSurface.Instance.SweptCurve)!.Value;
            Need(Curve(profile), profile.Id);
            var extrusion = V(s.Ref(ST.SurfaceOfLinearExtrusion.Instance.ExtrusionAxis)!.Value.VectorOf());
            return () => BrepSurface.LinearExtrusion(CurveIndexOf(profile.Id), extrusion);
        }
        if (s.Is<ST.IBSplineSurface>())
        {
            var row = BrepSurface.BSpline(SplineSurface(s));
            return () => row;
        }
        if (s.Is<ST.IRectangularTrimmedSurface>())
            return RectangularTrimmedRow(s);
        if (s.Is<ST.IOffsetSurface>())
        {
            var basis = s.Ref(ST.OffsetSurface.Instance.BasisSurface)!.Value;
            Need(Surface(basis), basis.Id);
            var distance = Length(s.Number(ST.OffsetSurface.Instance.Distance) ?? 0);
            return () => BrepSurface.Offset(SurfaceIndexOf(basis.Id), distance);
        }
        throw Unsupported(s);
    }

    BrepSurface ElementaryRow(StepEntity s, Frame3D frame)
    {
        if (s.Is<ST.IPlane>())
            return BrepSurface.Plane(frame);
        if (s.Is<ST.ICylindricalSurface>())
            return BrepSurface.Cylinder(frame, Length(s.Number(ST.CylindricalSurface.Instance.Radius) ?? 0));
        if (s.Is<ST.IConicalSurface>())
            return BrepSurface.Cone(frame, Length(s.Number(ST.ConicalSurface.Instance.Radius) ?? 0),
                new Angle(Radians(s.Number(ST.ConicalSurface.Instance.SemiAngle) ?? 0)));
        if (s.Is<ST.ISphericalSurface>())
            return BrepSurface.Sphere(frame, Length(s.Number(ST.SphericalSurface.Instance.Radius) ?? 0));
        if (s.Is<ST.IToroidalSurface>())
        {
            var part = !s.Is<ST.IDegenerateToroidalSurface>() ? TorusPart.Full()
                : s.Boolean(ST.DegenerateToroidalSurface.Instance.SelectOuter) == true ? TorusPart.Apple() : TorusPart.Lemon();
            return BrepSurface.Torus(frame, Length(s.Number(ST.ToroidalSurface.Instance.MajorRadius) ?? 0),
                Length(s.Number(ST.ToroidalSurface.Instance.MinorRadius) ?? 0), part);
        }
        throw Unsupported(s);
    }

    /// <summary>The control net transposed into the kernel's row-major Array2D: STEP lists it u-major
    /// (P_00, P_01, ..., P_0m, P_10, ...), the kernel holds P_ij at column i, row j.</summary>
    BSplineSurfaceData SplineSurface(StepEntity s)
    {
        var b = ST.BSplineSurface.Instance;
        var uDegree = (int)(s.Integer(b.UDegree) ?? 0);
        var vDegree = (int)(s.Integer(b.VDegree) ?? 0);
        var net = s.Refs(b.ControlPointsList) ?? [];
        var nu = net.Count;
        var nv = nu > 0 ? net[0].Count : 0;
        var weights = s.SurfaceWeights();
        var points = new Point3D[nu * nv];
        var w = new Number[nu * nv];
        for (var i = 0; i < nu; i++)
        {
            if (net[i].Count != nv)
                throw Unsupported(s);
            for (var j = 0; j < nv; j++)
            {
                points[j * nu + i] = P(net[i][j].PointOf());
                w[j * nu + i] = weights?[i][j] ?? 1.0;
            }
        }
        return new BSplineSurfaceData(SplineKnots.Basis(uDegree, s.SurfaceKnots(true, uDegree, nu)), SplineKnots.Basis(vDegree, s.SurfaceKnots(false, vDegree, nv)),
            new Array2D<Point3D>(points, nu, nv), new Array2D<Number>(w, nu, nv));
    }

    Func<BrepSurface> RectangularTrimmedRow(StepEntity s)
    {
        var r = ST.RectangularTrimmedSurface.Instance;
        var basis = s.Ref(r.BasisSurface)!.Value;
        Need(Surface(basis), basis.Id);
        var (fu, fv) = ParameterFactors(basis);
        var uRange = new NumberInterval((s.Number(r.U1) ?? 0) * fu, (s.Number(r.U2) ?? 0) * fu);
        var vRange = new NumberInterval((s.Number(r.V1) ?? 0) * fv, (s.Number(r.V2) ?? 0) * fv);
        var uSense = s.Boolean(r.Usense) ?? true;
        var vSense = s.Boolean(r.Vsense) ?? true;
        return () => BrepSurface.RectangularTrimmed(SurfaceIndexOf(basis.Id), uRange, vRange, uSense, vSense);
    }

    /// <summary>What a surface's u and v are multiplied by: radians per angle unit along an angle (u of every
    /// placement-based surface but the plane, v of the sphere and torus, u of a revolution), the length scale along a
    /// length (both of the plane, v of the cylinder and cone), the profile's factor along a swept curve, 1 for
    /// a B-spline's knot parameters.</summary>
    (double U, double V) ParameterFactors(StepEntity s)
        => s.Is<ST.IPlane>() ? (_lengthScale, _lengthScale)
            : s.Is<ST.ICylindricalSurface>() || s.Is<ST.IConicalSurface>() ? (_radiansPerUnit, _lengthScale)
            : s.Is<ST.ISphericalSurface>() || s.Is<ST.IToroidalSurface>() ? (_radiansPerUnit, _radiansPerUnit)
            : s.Is<ST.ISurfaceOfRevolution>() ? (_radiansPerUnit, ParameterFactor(s.Ref(ST.SweptSurface.Instance.SweptCurve)!.Value))
            : s.Is<ST.ISurfaceOfLinearExtrusion>() ? (ParameterFactor(s.Ref(ST.SweptSurface.Instance.SweptCurve)!.Value), 1)
            : s.Is<ST.IRectangularTrimmedSurface>() ? ParameterFactors(s.Ref(ST.RectangularTrimmedSurface.Instance.BasisSurface)!.Value)
            : s.Is<ST.IOffsetSurface>() ? ParameterFactors(s.Ref(ST.OffsetSurface.Instance.BasisSurface)!.Value)
            : (1, 1);
}
