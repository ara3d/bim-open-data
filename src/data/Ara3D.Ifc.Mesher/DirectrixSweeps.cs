using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Profiles swept along a directrix curve with parallel-transport frames. The reference
/// surface of a surface-curve sweep and the fixed reference of a fixed-reference sweep are not used.</summary>
public static class DirectrixSweeps
{
    /// <summary>Basic directrix-frame sweep; fixed reference not fully implemented.</summary>
    public static TriangleMesh3D BuildSurfaceCurveSweptAreaSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordApproximate("IFCSURFACECURVESWEPTAREASOLID", "Directrix frames only; surface ref ignored");
        var profile = ProfileBuilder.Build(ctx, MeshHelpers.ResolveRequired(ctx, solid, IfcSurfaceCurveSweptAreaSolid.Instance.SweptArea));
        var directrix = MeshHelpers.ResolveRequired(ctx, solid, IfcSurfaceCurveSweptAreaSolid.Instance.Directrix);
        var path = CurveEvaluator.Evaluate3D(ctx, directrix);
        var frames = BuildParallelTransportFrames(path);
        var profile2D = profile.Outer;

        var meshes = new List<TriangleMesh3D>();
        for (var i = 0; i < frames.Count - 1; i++)
        {
            var p0 = profile2D.Select(p => (Point3D)frames[i].ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
            var p1 = profile2D.Select(p => (Point3D)frames[i + 1].ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
            meshes.Add(SweepRings.Connect(p0, p1));
        }
        return MeshHelpers.Merge(meshes);
    }

    public static TriangleMesh3D BuildFixedReferenceSweptAreaSolid(MeshingContext ctx, IfcEntity solid)
    {
        ctx.Diagnostics.RecordApproximate("IFCFIXEDREFERENCESWEPTAREASOLID", "Treated as surface curve swept with fixed Z reference");
        return BuildSurfaceCurveSweptAreaSolid(ctx, solid);
    }

    /// <summary>One frame per path point, each rotated from the previous by the smallest rotation that
    /// takes the previous tangent to the new one, so the profile does not spin along the path.</summary>
    internal static List<Frame3D> BuildParallelTransportFrames(IReadOnlyList<Vector3> path)
    {
        var frames = new List<Frame3D>(path.Count);
        if (path.Count == 0)
            return frames;

        var z = path.Count > 1 ? (path[1] - path[0]).Normalize : Vector3.UnitZ;
        var x = MathF.Abs(Vector3.Dot(Vector3.UnitX, z)) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var y = Vector3.Cross(z, x).Normalize;
        x = Vector3.Cross(y, z).Normalize;
        frames.Add(new Frame3D(new Point3D(path[0].X, path[0].Y, path[0].Z), new OrthonormalBasis3D(new Axes3D(x, y, z), true)));

        for (var i = 1; i < path.Count; i++)
        {
            var prev = frames[^1];
            var tangent = (path[i] - path[i - 1]).Normalize;
            if (tangent.LengthSquared() < 1e-12f)
            {
                frames.Add(prev.WithOrigin(new Point3D(path[i].X, path[i].Y, path[i].Z)));
                continue;
            }
            var newZ = tangent;
            var oldZ = prev.Z;
            var axis = Vector3.Cross(oldZ, newZ);
            if (axis.LengthSquared() < 1e-12f)
            {
                frames.Add(new Frame3D(new Point3D(path[i].X, path[i].Y, path[i].Z), prev.Basis));
            }
            else
            {
                var angle = MathF.Acos(Math.Clamp(Vector3.Dot(oldZ, newZ), -1f, 1f));
                var rot = Matrix4x4.CreateFromAxisAngle(axis.Normalize, angle);
                var newX = prev.X.Transform(rot);
                var newY = prev.Y.Transform(rot);
                frames.Add(new Frame3D(new Point3D(path[i].X, path[i].Y, path[i].Z), new OrthonormalBasis3D(new Axes3D(newX, newY, newZ), true)));
            }
        }
        return frames;
    }
}
