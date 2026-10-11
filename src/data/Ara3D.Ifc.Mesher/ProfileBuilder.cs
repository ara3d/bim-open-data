using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Builds 2D profiles as <see cref="PolygonWithHoles"/>: dispatch by entity type and cache, and the
/// profiles defined by curves (arbitrary, with voids, composite, derived). The parameterized shapes
/// are in <see cref="BasicProfiles"/> and <see cref="SteelProfiles"/>.
/// </summary>
public static class ProfileBuilder
{
    public static PolygonWithHoles Build(MeshingContext ctx, IfcEntity profile)
    {
        if (ctx.ProfileCache.TryGetValue(profile.Id, out var cached))
            return cached;

        var result = profile.GetEntityName() switch
        {
            "IFCRECTANGLEPROFILEDEF" => BasicProfiles.BuildRectangle(ctx, profile),
            "IFCRECTANGLEHOLLOWPROFILEDEF" => BasicProfiles.BuildRectangleHollow(ctx, profile),
            "IFCROUNDEDRECTANGLEPROFILEDEF" => BasicProfiles.BuildRoundedRectangle(ctx, profile),
            "IFCCIRCLEPROFILEDEF" => BasicProfiles.BuildCircle(ctx, profile),
            "IFCCIRCLEHOLLOWPROFILEDEF" => BasicProfiles.BuildCircleHollow(ctx, profile),
            "IFCELLIPSEPROFILEDEF" => BasicProfiles.BuildEllipse(ctx, profile),
            "IFCISHAPEPROFILEDEF" => SteelProfiles.BuildIShape(ctx, profile),
            "IFCLSHAPEPROFILEDEF" => SteelProfiles.BuildLShape(ctx, profile),
            "IFCUSHAPEPROFILEDEF" => SteelProfiles.BuildUShape(ctx, profile),
            "IFCTSHAPEPROFILEDEF" => SteelProfiles.BuildTShape(ctx, profile),
            "IFCZSHAPEPROFILEDEF" => SteelProfiles.BuildZShape(ctx, profile),
            "IFCCSHAPEPROFILEDEF" => SteelProfiles.BuildCShape(ctx, profile),
            "IFCTRAPEZIUMPROFILEDEF" => BasicProfiles.BuildTrapezium(ctx, profile),
            "IFCARBITRARYCLOSEDPROFILEDEF" => BuildArbitraryClosed(ctx, profile),
            "IFCARBITRARYOPENPROFILEDEF" => BuildArbitraryOpenAsClosed(ctx, profile),
            "IFCARBITRARYPROFILEDEF" => BuildArbitrary(ctx, profile),
            "IFCARBITRARYPROFILEDEFWITHVOIDS" => BuildArbitraryWithVoids(ctx, profile),
            "IFCCOMPOSITEPROFILEDEF" => BuildComposite(ctx, profile),
            "IFCDERIVEDPROFILEDEF" => BuildDerived(ctx, profile),
            _ => throw new NotSupportedException($"Unsupported profile {profile.GetEntityName()} #{profile.Id}"),
        };

        ctx.ProfileCache[profile.Id] = result;
        ctx.Diagnostics.RecordSupported(profile.GetEntityName());
        return result;
    }

    public static bool IsOpenProfileType(string entityName)
        => entityName == "IFCARBITRARYOPENPROFILEDEF";

    /// <summary>Evaluates an open profile curve without forcing closure.</summary>
    public static IReadOnlyList<Vector2> BuildOpen(MeshingContext ctx, IfcEntity profile)
    {
        var points = profile.GetEntityName() switch
        {
            "IFCARBITRARYOPENPROFILEDEF" => BuildArbitraryOpen(ctx, profile),
            _ => throw new NotSupportedException($"Unsupported open profile {profile.GetEntityName()} #{profile.Id}"),
        };
        ctx.Diagnostics.RecordSupported(profile.GetEntityName());
        return points;
    }

    public static bool IsCurveClosed(MeshingContext ctx, IfcEntity curve)
    {
        var points = CurveEvaluator.Evaluate2D(ctx, curve);
        return points.Count >= 3 && points[0].DistanceSquared(points[^1]) < 1e-8f;
    }

    static PolygonWithHoles BuildArbitraryClosed(MeshingContext ctx, IfcEntity profile)
    {
        var curve = MeshHelpers.ResolveRequired(ctx, profile, IfcArbitraryClosedProfileDef.Instance.OuterCurve);
        return BuildArbitraryClosedFromCurve(ctx, curve);
    }

    static PolygonWithHoles BuildArbitraryClosedFromCurve(MeshingContext ctx, IfcEntity curve)
    {
        var points = CurveEvaluator.Evaluate2D(ctx, curve, dropClosure: true);
        return new PolygonWithHoles(PolygonWithHoles.CleanRing(points));
    }

    static IReadOnlyList<Vector2> BuildArbitraryOpen(MeshingContext ctx, IfcEntity profile)
    {
        var curve = MeshHelpers.ResolveRequired(ctx, profile, IfcArbitraryOpenProfileDef.Instance.Curve);
        return CurveEvaluator.Evaluate2D(ctx, curve, dropClosure: false);
    }

    /// <summary>Open profiles cannot form solids; only used when callers need a closed fallback.</summary>
    static PolygonWithHoles BuildArbitraryOpenAsClosed(MeshingContext ctx, IfcEntity profile)
        => throw new NotSupportedException(
            $"Open profile #{profile.Id} cannot form a closed area; use {nameof(BuildOpen)} for ribbon extrusion");

    static PolygonWithHoles BuildArbitrary(MeshingContext ctx, IfcEntity profile)
    {
        var curveId = MeshHelpers.ReadOptionalIdByIndex(profile, 2)
            ?? throw new InvalidOperationException($"IFCARBITRARYPROFILEDEF #{profile.Id} missing curve");
        var curve = ctx.GetEntity(curveId);
        if (IsCurveClosed(ctx, curve))
            return BuildArbitraryClosedFromCurve(ctx, curve);
        throw new NotSupportedException(
            $"Open IFCARBITRARYPROFILEDEF #{profile.Id}; use open-path extrusion instead of {nameof(Build)}");
    }

    static PolygonWithHoles BuildArbitraryWithVoids(MeshingContext ctx, IfcEntity profile)
    {
        var outerCurve = MeshHelpers.ResolveRequired(ctx, profile, IfcArbitraryProfileDefWithVoids.Instance.OuterCurve);
        var outer = BuildArbitraryClosedFromCurve(ctx, outerCurve);
        var holes = MeshHelpers.ReadIds(profile, IfcArbitraryProfileDefWithVoids.Instance.InnerCurves)
            .Select(id => PolygonWithHoles.CleanRing(CurveEvaluator.Evaluate2D(ctx, ctx.GetEntity(id), dropClosure: true)))
            .Cast<IReadOnlyList<Vector2>>()
            .ToList();
        return new PolygonWithHoles(outer.Outer, holes);
    }

    static PolygonWithHoles BuildComposite(MeshingContext ctx, IfcEntity profile)
    {
        var profiles = MeshHelpers.ReadIds(profile, IfcCompositeProfileDef.Instance.Profiles)
            .Select(id => Build(ctx, ctx.GetEntity(id)))
            .ToList();
        if (profiles.Count == 0)
            throw new InvalidOperationException("Empty composite profile");
        var outer = profiles[0].Outer.ToList();
        var holes = profiles.Skip(1).Select(p => p.Outer).ToList();
        return new PolygonWithHoles(outer, holes);
    }

    static PolygonWithHoles BuildDerived(MeshingContext ctx, IfcEntity profile)
    {
        var parent = Build(ctx, MeshHelpers.ResolveRequired(ctx, profile, IfcDerivedProfileDef.Instance.ParentProfile));
        var op = MeshHelpers.ResolveRequired(ctx, profile, IfcDerivedProfileDef.Instance.Operator);
        var map = Placements.ReadProfileTransformationOperator(ctx, op);
        var outer = parent.Outer.Select(p =>
        {
            var w = new Vector3(p.X, p.Y, 0).Transform(map);
            return new Vector2(w.X.Value, w.Y.Value);
        }).ToList();
        var holes = parent.Holes.Select(h => h.Select(p =>
        {
            var w = new Vector3(p.X, p.Y, 0).Transform(map);
            return new Vector2(w.X.Value, w.Y.Value);
        }).ToList()).Cast<IReadOnlyList<Vector2>>().ToList();
        return new PolygonWithHoles(outer, holes);
    }
}

internal static class Point3DVector2Ext
{
    public static Vector2 Vector2(this Point3D p) => new(p.X.Value, p.Y.Value);
}
