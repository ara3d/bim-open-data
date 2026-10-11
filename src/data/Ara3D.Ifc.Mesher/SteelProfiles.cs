using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using static Ara3D.Ifc.Mesher.ProfilePoints;

namespace Ara3D.Ifc.Mesher;

/// <summary>The parameterized steel sections: I, L, U, T, Z, and C shapes. Only the I shape's fillet
/// radius is honoured; the other radii and slopes are ignored.</summary>
internal static class SteelProfiles
{
    public static PolygonWithHoles BuildIShape(MeshingContext ctx, IfcEntity profile)
    {
        var w = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcIShapeProfileDef.Instance.OverallWidth));
        var d = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcIShapeProfileDef.Instance.OverallDepth));
        var tw = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcIShapeProfileDef.Instance.WebThickness));
        var tf = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcIShapeProfileDef.Instance.FlangeThickness));
        var fillet = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcIShapeProfileDef.Instance.FilletRadius));
        var hw = w / 2f;
        var hd = d / 2f;
        var points = fillet > 1e-6f
            ? BuildIShapeWithFillet(hw, hd, tw, tf, fillet, ctx.ArcSegments / 4)
            :
            [
                new(-hw, -hd), new(hw, -hd), new(hw, -hd + tf), new(tw / 2, -hd + tf),
                new(tw / 2, hd - tf), new(hw, hd - tf), new(hw, hd), new(-hw, hd),
                new(-hw, hd - tf), new(-tw / 2, hd - tf), new(-tw / 2, -hd + tf), new(-hw, -hd + tf),
            ];
        return ApplyPosition(ctx, profile, points);
    }

    static List<Vector2> BuildIShapeWithFillet(float hw, float hd, float tw, float tf, float r, int arcSeg)
    {
        r = Math.Min(r, Math.Min(tf, tw) / 2f);
        var pts = new List<Vector2> { new(-hw, -hd), new(hw, -hd), new(hw, -hd + tf) };
        AddArc(pts, new Vector2(tw / 2 + r, -hd + tf + r), r, MathF.PI, 3 * MathF.PI / 2, arcSeg);
        pts.Add(new(tw / 2, hd - tf));
        AddArc(pts, new Vector2(tw / 2 + r, hd - tf - r), r, MathF.PI / 2, MathF.PI, arcSeg);
        pts.AddRange([new(hw, hd - tf), new(hw, hd), new(-hw, hd), new(-hw, hd - tf)]);
        AddArc(pts, new Vector2(-tw / 2 - r, hd - tf - r), r, 0, MathF.PI / 2, arcSeg);
        pts.Add(new(-tw / 2, -hd + tf));
        AddArc(pts, new Vector2(-tw / 2 - r, -hd + tf + r), r, 3 * MathF.PI / 2, MathF.Tau, arcSeg);
        pts.Add(new(-hw, -hd + tf));
        return pts;
    }

    public static PolygonWithHoles BuildLShape(MeshingContext ctx, IfcEntity profile)
    {
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcLShapeProfileDef.Instance.Depth));
        var width = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcLShapeProfileDef.Instance.Width));
        var t = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcLShapeProfileDef.Instance.Thickness));
        var hw = width / 2f;
        var hd = depth / 2f;
        var points = new List<Vector2>
        {
            new(-hw, -hd), new(hw, -hd), new(hw, -hd + t), new(-hw + t, -hd + t),
            new(-hw + t, hd), new(-hw, hd),
        };
        return ApplyPosition(ctx, profile, points);
    }

    public static PolygonWithHoles BuildUShape(MeshingContext ctx, IfcEntity profile)
    {
        var d = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcUShapeProfileDef.Instance.Depth));
        var w = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcUShapeProfileDef.Instance.FlangeWidth));
        var tf = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcUShapeProfileDef.Instance.FlangeThickness));
        var tw = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcUShapeProfileDef.Instance.WebThickness));
        var points = new List<Vector2>
        {
            new(-w / 2, -d / 2), new(w / 2, -d / 2), new(w / 2, -d / 2 + tf),
            new(tw / 2, -d / 2 + tf), new(tw / 2, d / 2 - tf),
            new(w / 2, d / 2 - tf), new(w / 2, d / 2), new(-w / 2, d / 2),
            new(-w / 2, d / 2 - tf), new(-tw / 2, d / 2 - tf),
            new(-tw / 2, -d / 2 + tf), new(-w / 2, -d / 2 + tf),
        };
        return ApplyPosition(ctx, profile, points);
    }

    public static PolygonWithHoles BuildTShape(MeshingContext ctx, IfcEntity profile)
    {
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTShapeProfileDef.Instance.Depth));
        var flangeWidth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTShapeProfileDef.Instance.FlangeWidth));
        var webThick = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTShapeProfileDef.Instance.WebThickness));
        var flangeThick = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcTShapeProfileDef.Instance.FlangeThickness));
        var points = new List<Vector2>
        {
            new(-flangeWidth / 2, depth / 2 - flangeThick), new(flangeWidth / 2, depth / 2 - flangeThick),
            new(flangeWidth / 2, depth / 2), new(-flangeWidth / 2, depth / 2),
            new(-flangeWidth / 2, depth / 2 - flangeThick), new(-webThick / 2, depth / 2 - flangeThick),
            new(-webThick / 2, -depth / 2), new(webThick / 2, -depth / 2),
            new(webThick / 2, depth / 2 - flangeThick), new(-flangeWidth / 2, depth / 2 - flangeThick),
        };
        return ApplyPosition(ctx, profile, SimplifyPolygon(points));
    }

    public static PolygonWithHoles BuildZShape(MeshingContext ctx, IfcEntity profile)
    {
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcZShapeProfileDef.Instance.Depth));
        var flangeWidth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcZShapeProfileDef.Instance.FlangeWidth));
        var webThick = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcZShapeProfileDef.Instance.WebThickness));
        var flangeThick = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcZShapeProfileDef.Instance.FlangeThickness));
        var points = new List<Vector2>
        {
            new(-flangeWidth / 2, depth / 2), new(flangeWidth / 2, depth / 2),
            new(flangeWidth / 2, depth / 2 - flangeThick), new(webThick / 2, depth / 2 - flangeThick),
            new(webThick / 2, -depth / 2 + flangeThick), new(flangeWidth / 2, -depth / 2 + flangeThick),
            new(flangeWidth / 2, -depth / 2), new(-flangeWidth / 2, -depth / 2),
            new(-flangeWidth / 2, -depth / 2 + flangeThick), new(-webThick / 2, -depth / 2 + flangeThick),
            new(-webThick / 2, depth / 2 - flangeThick), new(-flangeWidth / 2, depth / 2 - flangeThick),
        };
        return ApplyPosition(ctx, profile, points);
    }

    public static PolygonWithHoles BuildCShape(MeshingContext ctx, IfcEntity profile)
    {
        var depth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCShapeProfileDef.Instance.Depth));
        var width = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCShapeProfileDef.Instance.Width));
        var wallThick = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCShapeProfileDef.Instance.WallThickness));
        var girth = ctx.ScaleLength(MeshHelpers.ReadNumber(profile, IfcCShapeProfileDef.Instance.Girth));
        var points = new List<Vector2>
        {
            new(-width / 2, -depth / 2), new(width / 2 - girth, -depth / 2),
            new(width / 2 - girth, -depth / 2 + wallThick), new(-width / 2 + wallThick, -depth / 2 + wallThick),
            new(-width / 2 + wallThick, depth / 2 - wallThick),
            new(width / 2 - girth, depth / 2 - wallThick),
            new(width / 2 - girth, depth / 2), new(-width / 2, depth / 2),
        };
        return ApplyPosition(ctx, profile, points);
    }
}
