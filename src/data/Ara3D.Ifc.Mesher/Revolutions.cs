using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Revolved area solids: the profile swept around an axis, capped at both ends when the
/// angle is less than a full turn.</summary>
public static class Revolutions
{
    public static TriangleMesh3D BuildRevolvedAreaSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordSupported("IFCREVOLVEDAREASOLID");
        var profile = ProfileBuilder.Build(ctx, MeshHelpers.ResolveRequired(ctx, solid, IfcRevolvedAreaSolid.Instance.SweptArea));
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, solid, IfcSweptAreaSolid.Instance.Position);
        var axisEntity = MeshHelpers.ResolveRequired(ctx, solid, IfcRevolvedAreaSolid.Instance.Axis);
        var axisPlacement = axisEntity.GetEntityName() == "IFCAXIS1PLACEMENT"
            ? Placements.IdentityFrame
            : Placements.ReadAxis2Placement3D(ctx, axisEntity);
        var axisPoint = axisEntity.GetEntityName() == "IFCAXIS1PLACEMENT"
            ? Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, axisEntity, IfcPlacement.Instance.Location))
            : axisPlacement.Origin.Vector3;
        var axisDir = axisEntity.GetEntityName() == "IFCAXIS1PLACEMENT"
            ? (MeshHelpers.ResolveOptional(ctx, axisEntity, IfcAxis1Placement.Instance.Axis) is { } ax
                ? Placements.ReadDirection3D(ctx, ax, Vector3.UnitZ)
                : Vector3.UnitZ)
            : axisPlacement.Z;
        var angle = (float)MeshHelpers.ReadNumber(solid, IfcRevolvedAreaSolid.Instance.Angle);
        if (angle <= 0)
            angle = MathF.Tau;

        var profile3D = profile.Outer.Select(p => (Point3D)frame.ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
        var segments = Math.Max(8, (int)(angle / MathF.Tau * ctx.CircleSegments));
        var meshes = new List<TriangleMesh3D>();

        var origin = axisPoint;
        for (var i = 0; i < segments; i++)
        {
            var t0 = angle * i / segments;
            var t1 = angle * (i + 1) / segments;
            var row0 = RotatePoints(profile3D, origin, axisDir, t0);
            var row1 = RotatePoints(profile3D, origin, axisDir, t1);
            meshes.Add(SweepRings.Connect(row0, row1));
        }

        if (angle < MathF.Tau - 1e-4f)
        {
            meshes.AddRange(BuildRevolveEndCaps(profile, frame, origin, axisDir, angle));
        }

        return MeshHelpers.Merge(meshes);
    }

    static List<Point3D> RotatePoints(IReadOnlyList<Point3D> points, Vector3 origin, Vector3 axis, float angle)
    {
        var rot = Matrix4x4.CreateFromAxisAngle(axis, angle);
        var t = Matrix4x4.CreateTranslation(-origin) * rot * Matrix4x4.CreateTranslation(origin);
        return points.Select(p => p.Transform(t)).ToList();
    }

    static IEnumerable<TriangleMesh3D> BuildRevolveEndCaps(
        PolygonWithHoles profile,
        Frame3D frame,
        Vector3 origin,
        Vector3 axisDir,
        float angle)
    {
        foreach (var rotation in new[] { 0f, angle })
        {
            foreach (var tri in profile.Triangulate())
            {
                var points = new[] { tri.A, tri.B, tri.C }
                    .Select(p => ProfilePointToWorld(frame, origin, axisDir, p, rotation))
                    .ToList();
                yield return new TriangleMesh3D(points, [new Integer3(0, 1, 2)]);
            }
        }
    }

    static Point3D ProfilePointToWorld(Frame3D frame, Vector3 origin, Vector3 axisDir, Point2D p, float rotation)
    {
        var pt = (Point3D)frame.ToWorld(new Vector3(p.X.Value, p.Y.Value, 0));
        return rotation == 0f ? pt : RotatePoints([pt], origin, axisDir, rotation)[0];
    }
}
