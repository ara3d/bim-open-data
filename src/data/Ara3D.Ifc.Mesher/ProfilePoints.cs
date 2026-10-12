using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Point-list helpers shared by the parameterized profiles.</summary>
internal static class ProfilePoints
{
    /// <summary>Places a profile's points by its optional 2D position.</summary>
    public static PolygonWithHoles ApplyPosition(MeshingContext ctx, IfcEntity profile, IReadOnlyList<Vector2> points)
    {
        var placement = MeshHelpers.ResolveOptional(ctx, profile, IfcParameterizedProfileDef.Instance.Position);
        if (placement is null)
            return new PolygonWithHoles(points);
        var frame = Placements.ReadAxis2Placement2D(ctx, placement);
        return new PolygonWithHoles(points.Select(p =>
        {
            var w = frame.ToWorld(new Vector3(p.X, p.Y, 0));
            return new Vector2(w.X.Value, w.Y.Value);
        }).ToList());
    }

    /// <summary>Drops consecutive points closer than 1e-5.</summary>
    public static List<Vector2> SimplifyPolygon(List<Vector2> points)
    {
        if (points.Count < 2)
            return points;
        var result = new List<Vector2> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].DistanceSquared(result[^1]) > 1e-10f)
                result.Add(points[i]);
        }
        return result;
    }
}
