using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Extrusions: closed profiles become solids, open profiles and curves become single-sided ribbons.
/// A non-perpendicular extrusion direction shears the solid. Revolutions, swept disks, and sweeps
/// along a directrix are in <see cref="Revolutions"/>, <see cref="SweptDisks"/>, and
/// <see cref="DirectrixSweeps"/>.
/// </summary>
public static class SweptSolids
{
    /// <summary>
    /// Sweeps an open profile or curve along <see cref="IfcSurfaceOfLinearExtrusion.ExtrudedDirection"/>
    /// by <see cref="IfcSurfaceOfLinearExtrusion.Depth"/>. Meshed as a single-sided ribbon (no end caps).
    /// Arcs and B-splines are uniformly sampled.
    /// </summary>
    public static TriangleMesh3D BuildSurfaceOfLinearExtrusion(MeshingContext ctx, IfcEntity surface)
    {
        ctx.Diagnostics.RecordSupported("IFCSURFACEOFLINEAREXTRUSION");
        var sweptCurve = MeshHelpers.ResolveRequired(ctx, surface, IfcSweptSurface.Instance.SweptCurve);
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, surface, IfcSweptSurface.Instance.Position);
        var direction = Placements.ReadDirection3D(ctx,
            MeshHelpers.ResolveRequired(ctx, surface, IfcSurfaceOfLinearExtrusion.Instance.ExtrudedDirection),
            Vector3.UnitZ).Normalize;
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(surface, IfcSurfaceOfLinearExtrusion.Instance.Depth));
        var extrusion = frame.ToWorldDirection(direction * depth);

        if (TryBuildOpenProfilePath(ctx, sweptCurve, out var openPath))
        {
            ctx.Diagnostics.RecordApproximate("IFCSURFACEOFLINEAREXTRUSION",
                "Open profile path extruded as ribbon surface; arcs uniformly sampled");
            return BuildExtrudedOpenPath(openPath, frame, extrusion);
        }

        if (IsCurveEntity(sweptCurve.GetEntityName()))
        {
            var path2D = CurveEvaluator.Evaluate2D(ctx, sweptCurve, dropClosure: false);
            if (path2D.Count >= 2)
            {
                ctx.Diagnostics.RecordApproximate("IFCSURFACEOFLINEAREXTRUSION",
                    "2D swept curve extruded as ribbon surface; arcs uniformly sampled");
                return BuildExtrudedOpenPath(path2D, frame, extrusion);
            }

            var path3D = CurveEvaluator.Evaluate3D(ctx, sweptCurve);
            if (path3D.Count >= 2)
            {
                ctx.Diagnostics.RecordApproximate("IFCSURFACEOFLINEAREXTRUSION",
                    "3D curve extruded as ribbon surface; arcs uniformly sampled");
                return BuildExtrudedOpenPath3D(path3D, frame, extrusion);
            }
        }

        throw new NotSupportedException(
            $"IFCSURFACEOFLINEAREXTRUSION #{surface.Id} SweptCurve #{sweptCurve.Id} is not an open path");
    }

    public static TriangleMesh3D BuildExtrudedAreaSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordSupported("IFCEXTRUDEDAREASOLID");
        var profileEntity = MeshHelpers.ResolveRequired(ctx, solid, IfcExtrudedAreaSolid.Instance.SweptArea);
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, solid, IfcSweptAreaSolid.Instance.Position);
        var direction = Placements.ReadDirection3D(ctx,
            MeshHelpers.ResolveRequired(ctx, solid, IfcExtrudedAreaSolid.Instance.ExtrudedDirection),
            Vector3.UnitZ).Normalize;
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(solid, IfcExtrudedAreaSolid.Instance.Depth));
        var extrusion = frame.ToWorldDirection(direction * depth);

        if (TryBuildOpenProfilePath(ctx, profileEntity, out var openPath))
        {
            ctx.Diagnostics.RecordApproximate("IFCEXTRUDEDAREASOLID", "Open profile extruded as ribbon surface");
            return BuildExtrudedOpenPath(openPath, frame, extrusion);
        }

        if (profileEntity.GetEntityName() == "IFCCOMPOSITEPROFILEDEF")
            return BuildExtrudedCompositeProfile(ctx, profileEntity, frame, extrusion);

        var profile = ProfileBuilder.Build(ctx, profileEntity);
        return Extrusion.Solid(profile, frame, extrusion);
    }

    static TriangleMesh3D BuildExtrudedCompositeProfile(
        MeshingContext ctx,
        IfcEntity profileEntity,
        Frame3D frame,
        Vector3 extrusion)
    {
        ctx.Diagnostics.RecordSupported("IFCCOMPOSITEPROFILEDEF");
        var profiles = MeshHelpers.ReadIds(profileEntity, IfcCompositeProfileDef.Instance.Profiles)
            .Select(id => ProfileBuilder.Build(ctx, ctx.GetEntity(id)))
            .ToList();
        if (profiles.Count == 0)
            throw new InvalidOperationException($"Empty composite profile #{profileEntity.Id}");

        if (profiles.Count == 1)
            return Extrusion.Solid(profiles[0], frame, extrusion);

        if (TryCombineCompositeAsHoles(profiles, out var combined))
            return Extrusion.Solid(combined, frame, extrusion);

        ctx.Diagnostics.RecordApproximate("IFCCOMPOSITEPROFILEDEF", "Disjoint sub-profiles extruded separately and merged");
        var meshes = profiles
            .Select(p => Extrusion.Solid(p, frame, extrusion))
            .ToList();
        return MeshHelpers.Merge(meshes);
    }

    static bool TryCombineCompositeAsHoles(IReadOnlyList<PolygonWithHoles> profiles, out PolygonWithHoles combined)
    {
        combined = default!;
        var outer = profiles[0];
        var holes = outer.Holes.ToList();
        foreach (var candidate in profiles.Skip(1))
        {
            if (!IsProfileContainedIn(candidate, outer))
                return false;
            holes.Add(candidate.Outer);
        }

        combined = new PolygonWithHoles(outer.Outer, holes);
        return true;
    }

    static bool IsProfileContainedIn(PolygonWithHoles inner, PolygonWithHoles outer)
    {
        foreach (var p in inner.Outer)
        {
            if (!PointInPolygon(p, outer.Outer))
                return false;
            foreach (var hole in outer.Holes)
            {
                if (PointInPolygon(p, hole))
                    return false;
            }
        }
        return true;
    }

    static bool PointInPolygon(Vector2 p, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if ((pi.Y > p.Y) != (pj.Y > p.Y) &&
                p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y + 1e-12f) + pi.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Legacy attribute-driven extrusion: depth on each segment, not on the solid.</summary>
    public static TriangleMesh3D? BuildAttDrivenExtrudedSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordSupported("IFCATTDRIVENEXTRUDEDSOLID");
        var meshes = MeshHelpers.ReadIdsByIndex(solid, 0)
            .Select(id => BuildAttDrivenExtrudedSegment(ctx, ctx.GetEntity(id)))
            .ToList();
        return meshes.Count == 0 ? null : MeshHelpers.Merge(meshes);
    }

    /// <summary>Attribute-driven extrusion with half-space clipping per segment batch.</summary>
    public static TriangleMesh3D? BuildAttDrivenClippedExtrudedSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordApproximate("IFCATTDRIVENCLIPPEDEXTRUDEDSOLID", "Segment depth + half-space clip");
        var mesh = BuildAttDrivenExtrudedSolid(ctx, solid);
        if (mesh is null)
            return null;

        var result = mesh.Value;
        foreach (var clipId in MeshHelpers.ReadIdsByIndex(solid, 1))
        {
            var halfSpace = ctx.GetEntity(clipId);
            if (halfSpace.GetEntityName() == "IFCHALFSPACESOLID")
                result = Booleans.ClipByHalfSpace(ctx, result, halfSpace);
        }
        return result;
    }

    static TriangleMesh3D BuildAttDrivenExtrudedSegment(MeshingContext ctx, IfcEntity segment)
    {
        ctx.Diagnostics.RecordSupported("IFCATTDRIVENEXTRUDEDSEGMENT");
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumberByIndex(segment, 2));
        var placement = MeshHelpers.ReadOptionalIdByIndex(segment, 3) is { } placementId
            ? ctx.GetEntity(placementId)
            : null;
        var profileId = MeshHelpers.ReadOptionalIdByIndex(segment, 4)
            ?? throw new InvalidOperationException($"AttDriven segment #{segment.Id} missing profile");
        var profileEntity = ctx.GetEntity(profileId);

        var frame = placement is null
            ? Placements.IdentityFrame
            : Placements.ReadAxis2Placement3D(ctx, placement);
        var extrusion = frame.ToWorldDirection(Vector3.UnitZ * depth);

        if (TryBuildOpenProfilePath(ctx, profileEntity, out var openPath))
            return BuildExtrudedOpenPath(openPath, frame, extrusion);

        var profile = ProfileBuilder.Build(ctx, profileEntity);
        return Extrusion.Solid(profile, frame, extrusion);
    }

    static bool TryBuildOpenProfilePath(MeshingContext ctx, IfcEntity profileEntity, out IReadOnlyList<Vector2> path)
    {
        if (ProfileBuilder.IsOpenProfileType(profileEntity.GetEntityName()))
        {
            path = ProfileBuilder.BuildOpen(ctx, profileEntity);
            return path.Count >= 2;
        }

        if (profileEntity.GetEntityName() == "IFCARBITRARYPROFILEDEF")
        {
            var curveId = MeshHelpers.ReadOptionalIdByIndex(profileEntity, 2);
            if (curveId is null)
            {
                path = [];
                return false;
            }
            var curve = ctx.GetEntity(curveId.Value);
            if (!ProfileBuilder.IsCurveClosed(ctx, curve))
            {
                path = CurveEvaluator.Evaluate2D(ctx, curve, dropClosure: false);
                return path.Count >= 2;
            }
        }

        path = [];
        return false;
    }

    /// <summary>Extrudes an open 2D polyline in the placement XY plane into a ribbon surface.</summary>
    static TriangleMesh3D BuildExtrudedOpenPath(IReadOnlyList<Vector2> path, Frame3D frame, Vector3 extrusion)
    {
        var bottomRow = path.Select(p => (Point3D)frame.ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
        return Extrusion.Ribbon(bottomRow, extrusion);
    }

    /// <summary>Extrudes an open 3D polyline (local to placement) into a ribbon surface.</summary>
    static TriangleMesh3D BuildExtrudedOpenPath3D(IReadOnlyList<Vector3> path, Frame3D frame, Vector3 extrusion)
    {
        var bottomRow = path.Select(p => (Point3D)frame.ToWorld(p)).ToList();
        return Extrusion.Ribbon(bottomRow, extrusion);
    }

    static bool IsCurveEntity(string entityName)
        => entityName is "IFCPOLYLINE" or "IFCINDEXEDPOLYCURVE" or "IFCCIRCLE" or "IFCELLIPSE"
            or "IFCLINE" or "IFCTRIMMEDCURVE" or "IFCCOMPOSITECURVE" or "IFCBSPLINECURVE"
            or "IFCBSPLINECURVEWITHKNOTS" or "IFCRATIONALBSPLINECURVEWITHKNOTS";
}
