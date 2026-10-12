using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Reads a face's surface entity and returns the kernel <see cref="SurfaceMap"/> for it.</summary>
internal static class FaceSurfaceMaps
{
    /// <summary>The map for a face: its declared surface when it is one of the four kinds
    /// (plane, cylinder, surface of revolution, B-spline surface) and the ring lies on it, else
    /// the plane of the ring itself.</summary>
    public static SurfaceMap Resolve(MeshingContext ctx, IfcEntity face, IReadOnlyList<Vector3> outer)
    {
        if (FaceBounds.IsFaceSurfaceEntity(face))
        {
            var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
            var surfaceName = surface?.GetEntityName();
            if (surfaceName == "IFCCYLINDRICALSURFACE"
                && TryBuildCylinder(ctx, surface!, out var cyl))
                return cyl;
            if (surfaceName == "IFCSURFACEOFREVOLUTION"
                && TryBuildRevolution(ctx, surface!, out var rev))
                return rev;
            if (surfaceName is "IFCBSPLINESURFACEWITHKNOTS" or "IFCRATIONALBSPLINESURFACEWITHKNOTS" or "IFCBSPLINESURFACE"
                && TryBuildBSpline(ctx, surface!, out var bsp))
                return bsp;
            if (TryFromFaceSurface(ctx, face, outer, out var facePlane))
                return new PlanarMap(facePlane);
        }
        return new PlanarMap(FacePlane.OfRing(outer));
    }

    static bool TryBuildCylinder(MeshingContext ctx, IfcEntity surface, out CylinderMap map)
    {
        map = null!;
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(surface, IfcCylindricalSurface.Instance.Radius));
        if (radius <= 1e-7f)
            return false;
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, surface, IfcElementarySurface.Instance.Position);
        map = new CylinderMap(frame, radius);
        return true;
    }

    /// <summary>Reads the surface's axis and swept profile; false when either is missing, degenerate,
    /// or fails to read, so the caller falls back to a flat projection.</summary>
    static bool TryBuildRevolution(MeshingContext ctx, IfcEntity surface, out RevolutionMap map)
    {
        map = null!;
        try
        {
            var axisEntity = MeshHelpers.ResolveOptional(ctx, surface, IfcSurfaceOfRevolution.Instance.AxisPosition);
            if (axisEntity is null)
                return false;
            var position = Placements.ReadOptionalAxis2Placement3D(ctx, surface, IfcSweptSurface.Instance.Position);

            var axisLocal = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, axisEntity, IfcPlacement.Instance.Location));
            var axisDirLocal = MeshHelpers.ResolveOptional(ctx, axisEntity, IfcAxis1Placement.Instance.Axis) is { } ax
                ? Placements.ReadDirection3D(ctx, ax, Vector3.UnitZ)
                : Vector3.UnitZ;

            var axisPoint = position.ToWorld(axisLocal);
            var axisDir = position.ToWorldDirection(axisDirLocal);

            var sweptCurve = MeshHelpers.ResolveRequired(ctx, surface, IfcSweptSurface.Instance.SweptCurve);
            var meridian = EvaluateMeridianCurve3D(ctx, sweptCurve)
                .Select(p => position.ToWorld(p))
                .ToList();

            var built = RevolutionMap.Build(
                new Ray3D(new Point3D(axisPoint.X, axisPoint.Y, axisPoint.Z), axisDir), meridian);
            if (built is null)
                return false;
            map = built;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Evaluates a swept-surface profile (curve or arbitrary profile wrapping a curve) to 3D points.</summary>
    static List<Vector3> EvaluateMeridianCurve3D(MeshingContext ctx, IfcEntity sweptCurve)
    {
        var curve = sweptCurve.GetEntityName() switch
        {
            "IFCARBITRARYOPENPROFILEDEF" or "IFCCENTERLINEPROFILEDEF"
                => MeshHelpers.ResolveOptional(ctx, sweptCurve, IfcArbitraryOpenProfileDef.Instance.Curve),
            "IFCARBITRARYCLOSEDPROFILEDEF"
                => MeshHelpers.ResolveOptional(ctx, sweptCurve, IfcArbitraryClosedProfileDef.Instance.OuterCurve),
            _ => sweptCurve,
        };
        return curve is null ? [] : FaceBounds.DedupeConsecutive3D(CurveEvaluator.Evaluate3D(ctx, curve).ToList());
    }

    /// <summary>False when the control net, degrees or knots do not describe a surface, or reading
    /// them throws, so the caller falls back to a flat projection.</summary>
    static bool TryBuildBSpline(MeshingContext ctx, IfcEntity surface, out BSplineSurfaceMap map)
    {
        map = null!;
        try
        {
            var built = BSplineSurfaceMap.Build(BSplineSurfaceReader.Read(ctx, surface));
            if (built is null)
                return false;
            map = built;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The face's declared IFCPLANE or IFCCURVEBOUNDEDPLANE, when the ring lies in it.
    /// A shared IFCPLANE (the golden box, say) need not contain every face's boundary; then the
    /// caller falls back to the ring's own plane.</summary>
    static bool TryFromFaceSurface(MeshingContext ctx, IfcEntity face, IReadOnlyList<Vector3> outer, out FacePlane plane)
    {
        plane = default;
        var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
        if (surface == null)
            return false;

        var ok = surface.GetEntityName() switch
        {
            "IFCPLANE" => TryGetPlaneSurface(ctx, surface, out plane),
            "IFCCURVEBOUNDEDPLANE" => TryGetCurveBoundedPlaneSurface(ctx, surface, out plane),
            _ => false,
        };
        if (!ok || outer.Count < 3)
            return ok;

        var maxDist = 0f;
        foreach (var p in outer)
            maxDist = MathF.Max(maxDist, MathF.Abs(Vector3.Dot(p - plane.Origin, plane.Normal)));
        return maxDist <= PlaneContainmentTolerance(ctx);
    }

    static float PlaneContainmentTolerance(MeshingContext ctx)
        => MathF.Max(Tolerance.Eps, MathF.Abs((float)ctx.LengthScale) * 1e-4f);

    static bool TryGetPlaneSurface(MeshingContext ctx, IfcEntity planeEntity, out FacePlane plane)
    {
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, planeEntity, IfcElementarySurface.Instance.Position);
        plane = FacePlane.FromFrame(frame);
        return true;
    }

    static bool TryGetCurveBoundedPlaneSurface(MeshingContext ctx, IfcEntity bounded, out FacePlane plane)
    {
        var basis = MeshHelpers.ResolveRequired(ctx, bounded, IfcCurveBoundedPlane.Instance.BasisSurface);
        return TryGetPlaneSurface(ctx, basis, out plane);
    }
}
