using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using static Ara3D.Ifc.Mesher.ProfilePoints;

namespace Ara3D.Ifc.Mesher;

/// <summary>Rectangles, circles, ellipses, and trapezia, with their hollow and rounded variants.</summary>
internal static class BasicProfiles
{
    public static PolygonWithHoles BuildRectangle(MeshingContext ctx, IfcEntity profile)
    {
        if (TryReadLegacyRectangleDims(ctx, profile, out var lx, out var ly))
        {
            var legacyPoints = new List<Vector2>
            {
                new(-lx / 2f, -ly / 2f), new(lx / 2f, -ly / 2f),
                new(lx / 2f, ly / 2f), new(-lx / 2f, ly / 2f),
            };
            var placementId = MeshHelpers.ReadOptionalIdByIndex(profile, 0);
            if (placementId is not null)
            {
                var placement = ctx.GetEntity(placementId.Value);
                var frame = Placements.ReadAxis2Placement2D(ctx, placement);
                return new PolygonWithHoles(legacyPoints.Select(p =>
                {
                    var w = frame.ToWorld(new Vector3(p.X, p.Y, 0));
                    return new Vector2(w.X.Value, w.Y.Value);
                }).ToList());
            }
            return new PolygonWithHoles(legacyPoints);
        }

        var x = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRectangleProfileDef.Instance.XDim));
        var y = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRectangleProfileDef.Instance.YDim));
        var points = new List<Vector2>
        {
            new(-x / 2f, -y / 2f), new(x / 2f, -y / 2f),
            new(x / 2f, y / 2f), new(-x / 2f, y / 2f),
        };
        return ApplyPosition(ctx, profile, points);
    }

    static bool TryReadLegacyRectangleDims(MeshingContext ctx, IfcEntity profile, out float x, out float y)
    {
        x = y = 0;
        if (profile.GetEntityName() != "IFCRECTANGLEPROFILEDEF" || profile.Attributes.Count != 4)
            return false;
        var token0 = profile.GetValue(0);
        if (!token0.IsId)
            return false;
        x = ctx.ScaleLength(MeshHelpers.ReadNumberByIndex(profile, 2));
        y = ctx.ScaleLength(MeshHelpers.ReadNumberByIndex(profile, 3));
        return x > 0 && y > 0;
    }

    public static PolygonWithHoles BuildRectangleHollow(MeshingContext ctx, IfcEntity profile)
    {
        var outer = BuildRectangle(ctx, profile);
        var x = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRectangleHollowProfileDef.Instance.XDim));
        var y = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRectangleHollowProfileDef.Instance.YDim));
        var wt = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRectangleHollowProfileDef.Instance.WallThickness));
        var inner = new List<Vector2>
        {
            new(-x / 2f + wt, -y / 2f + wt), new(x / 2f - wt, -y / 2f + wt),
            new(x / 2f - wt, y / 2f - wt), new(-x / 2f + wt, y / 2f - wt),
        };
        var positioned = ApplyPosition(ctx, profile, inner);
        return new PolygonWithHoles(outer.Outer, [positioned.Outer]);
    }

    public static PolygonWithHoles BuildRoundedRectangle(MeshingContext ctx, IfcEntity profile)
    {
        var x = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRoundedRectangleProfileDef.Instance.XDim));
        var y = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRoundedRectangleProfileDef.Instance.YDim));
        var r = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcRoundedRectangleProfileDef.Instance.RoundingRadius));
        r = Math.Min(r, Math.Min(x, y) / 2f);
        var points = RoundRectPoints(x, y, r, ctx.ArcSegments / 4);
        return ApplyPosition(ctx, profile, points);
    }

    static List<Vector2> RoundRectPoints(float x, float y, float r, int arcSeg)
    {
        var hx = x / 2f - r;
        var hy = y / 2f - r;
        var pts = new List<Vector2>();
        pts.AddRange(CircleSamples.Arc(new Vector2(hx, hy), r, 0, MathF.PI / 2, arcSeg));
        pts.AddRange(CircleSamples.Arc(new Vector2(-hx, hy), r, MathF.PI / 2, MathF.PI, arcSeg));
        pts.AddRange(CircleSamples.Arc(new Vector2(-hx, -hy), r, MathF.PI, 3 * MathF.PI / 2, arcSeg));
        pts.AddRange(CircleSamples.Arc(new Vector2(hx, -hy), r, 3 * MathF.PI / 2, MathF.Tau, arcSeg));
        return pts;
    }

    public static PolygonWithHoles BuildCircle(MeshingContext ctx, IfcEntity profile)
    {
        var radius = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCircleProfileDef.Instance.Radius));
        var points = CircleSamples.Ring(radius, ctx.CircleSegments);
        return ApplyPosition(ctx, profile, points);
    }

    public static PolygonWithHoles BuildCircleHollow(MeshingContext ctx, IfcEntity profile)
    {
        var outerR = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCircleHollowProfileDef.Instance.Radius));
        var wall = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCircleHollowProfileDef.Instance.WallThickness));
        var innerR = outerR - wall;
        if (innerR <= 1e-6f)
            throw new InvalidOperationException($"Invalid hollow circle profile #{profile.Id}: inner radius <= 0");
        var outer = ApplyPosition(ctx, profile, CircleSamples.Ring(outerR, ctx.CircleSegments));
        var inner = ApplyPosition(ctx, profile, CircleSamples.Ring(innerR, ctx.CircleSegments));
        return new PolygonWithHoles(outer.Outer, [inner.Outer]);
    }

    public static PolygonWithHoles BuildEllipse(MeshingContext ctx, IfcEntity profile)
    {
        var a = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcEllipseProfileDef.Instance.SemiAxis1));
        var b = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcEllipseProfileDef.Instance.SemiAxis2));
        var points = Enumerable.Range(0, ctx.CircleSegments)
            .Select(i =>
            {
                var t = MathF.Tau * i / ctx.CircleSegments;
                return new Vector2(a * MathF.Cos(t), b * MathF.Sin(t));
            }).ToList();
        return ApplyPosition(ctx, profile, points);
    }

    public static PolygonWithHoles BuildTrapezium(MeshingContext ctx, IfcEntity profile)
    {
        var bx = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTrapeziumProfileDef.Instance.BottomXDim));
        var tx = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTrapeziumProfileDef.Instance.TopXDim));
        var y = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTrapeziumProfileDef.Instance.YDim));
        var dx = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTrapeziumProfileDef.Instance.TopXOffset));
        var points = new List<Vector2>
        {
            new(-bx / 2, -y / 2), new(bx / 2, -y / 2),
            new(dx + tx / 2, y / 2), new(dx - tx / 2, y / 2),
        };
        return ApplyPosition(ctx, profile, points);
    }
}
