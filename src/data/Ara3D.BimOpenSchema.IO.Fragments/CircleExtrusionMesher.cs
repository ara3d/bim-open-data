using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Integer3 = Ara3D.Geometry.Integer3;
using Point3D = Ara3D.Geometry.Point3D;
using TriangleMesh3D = Ara3D.Geometry.TriangleMesh3D;
using V3 = System.Numerics.Vector3;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>Tessellates a circle extrusion (a tube of a given radius swept along a path; Fragments
/// uses it for reinforcement bars) with the resolution That Open's viewer uses: a ring of
/// round(200 r) points clamped to 6..30, and an arc sampled at round(4 aperture r) points clamped
/// to 4..32. Each part of an axis (a wire, a wire set, or an arc) is its own capped tube.</summary>
public static class CircleExtrusionMesher
{
    public static TriangleMesh3D Tessellate(CircleExtrusion extrusion)
    {
        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        for (var a = 0; a < extrusion.AxesLength; a++)
        {
            if (extrusion.RadiusLength == 0)
                break;
            var radius = (float)extrusion.Radius(Math.Min(a, extrusion.RadiusLength - 1));
            foreach (var path in Paths(extrusion.Axes(a)!.Value))
                AddTube(path, radius, points, faces);
        }
        return new TriangleMesh3D(points, faces);
    }

    /// <summary>Each part of an axis as a polyline, in the order the axis lists them.</summary>
    public static IEnumerable<IReadOnlyList<V3>> Paths(Axis axis)
    {
        for (var k = 0; k < axis.PartsLength && k < axis.OrderLength; k++)
        {
            var index = (int)axis.Order(k);
            switch (axis.Parts(k))
            {
                case AxisPartClass.WIRE when index < axis.WiresLength:
                    var w = axis.Wires(index)!.Value;
                    yield return [ToV3(w.P1), ToV3(w.P2)];
                    break;
                case AxisPartClass.WIRE_SET when index < axis.WireSetsLength:
                    var set = axis.WireSets(index)!.Value;
                    yield return Enumerable.Range(0, set.PsLength).Select(i => ToV3(set.Ps(i)!.Value)).ToList();
                    break;
                case AxisPartClass.CIRCLE_CURVE when index < axis.CircleCurvesLength:
                    yield return Arc(axis.CircleCurves(index)!.Value);
                    break;
            }
        }
    }

    /// <summary>An arc as That Open's viewer reads it: x_direction is the axis the arc turns about,
    /// y_direction points from the centre to the first point, and aperture is the angle swept.</summary>
    public static IReadOnlyList<V3> Arc(CircleCurve curve)
    {
        var center = ToV3(curve.Position);
        var axis = V3.Normalize(ToV3(curve.XDirection));
        var first = ToV3(curve.YDirection);
        var count = Math.Clamp((int)Math.Round(curve.Aperture * curve.Radius * 4), 4, 32);
        if (!float.IsFinite(curve.Aperture * curve.Radius))
            count = 4;
        return Enumerable.Range(0, count)
            .Select(i => center + curve.Radius * V3.Transform(first, System.Numerics.Quaternion.CreateFromAxisAngle(axis, curve.Aperture * i / (count - 1))))
            .ToList();
    }

    public static int RingSize(float radius)
        => Math.Clamp((int)Math.Round(radius * 200), 6, 30);

    /// <summary>A capped tube along a polyline. Rings are kept perpendicular to the path by
    /// carrying the previous ring's normal along (parallel transport), so the tube does not twist.</summary>
    private static void AddTube(IReadOnlyList<V3> path, float radius, List<Point3D> points, List<Integer3> faces)
    {
        var centers = path.Where((p, i) => i == 0 || V3.Distance(p, path[i - 1]) > 1e-9f).ToList();
        if (centers.Count < 2 || !(radius > 0))
            return;

        var size = RingSize(radius);
        var first = points.Count;
        var normal = AnyPerpendicular(V3.Normalize(centers[1] - centers[0]));
        for (var k = 0; k < centers.Count; k++)
        {
            var tangent = Tangent(centers, k);
            var projected = normal - V3.Dot(normal, tangent) * tangent;
            normal = projected.LengthSquared() > 1e-12f ? V3.Normalize(projected) : AnyPerpendicular(tangent);
            var binormal = V3.Cross(tangent, normal);
            for (var j = 0; j < size; j++)
            {
                var angle = 2 * MathF.PI * j / size;
                var p = centers[k] + radius * (MathF.Cos(angle) * normal + MathF.Sin(angle) * binormal);
                points.Add(new Point3D(p.X, p.Y, p.Z));
            }
        }

        int At(int ring, int j) => first + ring * size + (j % size);
        for (var k = 0; k + 1 < centers.Count; k++)
            for (var j = 0; j < size; j++)
            {
                faces.Add(new Integer3(At(k, j), At(k, j + 1), At(k + 1, j + 1)));
                faces.Add(new Integer3(At(k, j), At(k + 1, j + 1), At(k + 1, j)));
            }

        var last = centers.Count - 1;
        for (var j = 1; j + 1 < size; j++)
        {
            faces.Add(new Integer3(At(0, 0), At(0, j + 1), At(0, j)));
            faces.Add(new Integer3(At(last, 0), At(last, j), At(last, j + 1)));
        }
    }

    private static V3 Tangent(IReadOnlyList<V3> c, int k)
    {
        var before = k > 0 ? V3.Normalize(c[k] - c[k - 1]) : V3.Zero;
        var after = k + 1 < c.Count ? V3.Normalize(c[k + 1] - c[k]) : V3.Zero;
        var sum = before + after;
        return sum.LengthSquared() > 1e-12f ? V3.Normalize(sum) : (after != V3.Zero ? after : before);
    }

    private static V3 AnyPerpendicular(V3 t)
    {
        var helper = MathF.Abs(t.Z) < 0.9f ? V3.UnitZ : V3.UnitX;
        return V3.Normalize(V3.Cross(helper, t));
    }

    private static V3 ToV3(FloatVector v) => new(v.X, v.Y, v.Z);
}
