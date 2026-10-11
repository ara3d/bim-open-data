using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Swept disk solids: a circle swept along a directrix. A straight directrix gets a closed
/// tube with annular or fan caps; a curved one uses parallel-transport frames (approximate for twist)
/// and no inner tube.</summary>
public static class SweptDisks
{
    public static TriangleMesh3D BuildSweptDiskSolid(MeshingContext ctx, IfcEntity solid)
    {
        var entityName = solid.GetEntityName();
        ctx.Diagnostics.RecordApproximate(entityName, "Parallel-transport frames for curved paths; straight paths use annular caps");
        var directrix = MeshHelpers.ResolveRequired(ctx, solid, IfcSweptDiskSolid.Instance.Directrix);
        var path = CurveEvaluator.Evaluate3D(ctx, directrix);
        if (path.Count < 2)
            throw new InvalidOperationException("Directrix too short");

        var outerR = ctx.ScaleLength(MeshHelpers.ReadNumber(solid, IfcSweptDiskSolid.Instance.Radius));
        var innerR = ctx.ScaleLength(MeshHelpers.ReadNumber(solid, IfcSweptDiskSolid.Instance.InnerRadius));

        if (path.Count == 2 && (path[1] - path[0]).LengthSquared > 1e-12f)
            return BuildStraightSweptDisk(
                new Point3D(path[0].X, path[0].Y, path[0].Z),
                new Point3D(path[1].X, path[1].Y, path[1].Z),
                outerR, innerR, ctx.CircleSegments);

        var frames = DirectrixSweeps.BuildParallelTransportFrames(path);
        var circle = SampleCircleProfile(outerR, ctx.CircleSegments);

        var meshes = new List<TriangleMesh3D>();
        for (var i = 0; i < frames.Count - 1; i++)
        {
            var c0 = circle.Select(p => (Point3D)frames[i].ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
            var c1 = circle.Select(p => (Point3D)frames[i + 1].ToWorld(new Vector3(p.X, p.Y, 0))).ToList();
            meshes.Add(SweepRings.Connect(c0, c1));
        }

        if (innerR > 1e-6f)
            ctx.Diagnostics.RecordApproximate(entityName, "Inner radius tube not subtracted on curved directrix");

        return MeshHelpers.Merge(meshes);
    }

    static TriangleMesh3D BuildStraightSweptDisk(Point3D start, Point3D end, float radius, float innerRadius, int segments)
    {
        if (radius <= 0f)
            throw new InvalidOperationException("Swept disk radius must be positive.");
        if (innerRadius < 0f || innerRadius >= radius)
            throw new InvalidOperationException("Swept disk inner radius must be smaller than radius.");

        var tangent = (end.Vector3 - start.Vector3).Normalize;
        var (xAxis, yAxis) = CreatePerpendicularAxes(tangent);
        var points = new List<Point3D>();
        AddRing(points, start, xAxis, yAxis, radius, segments);
        AddRing(points, end, xAxis, yAxis, radius, segments);
        var faces = new List<Integer3>();

        AddTubeSideFaces(faces, 0, segments, segments, reverse: false);

        if (innerRadius > 0f)
        {
            var innerStart = points.Count;
            AddRing(points, start, xAxis, yAxis, innerRadius, segments);
            var innerEnd = points.Count;
            AddRing(points, end, xAxis, yAxis, innerRadius, segments);
            AddTubeSideFaces(faces, innerStart, innerEnd, segments, reverse: true);
            AddAnnularCapFaces(faces, 0, innerStart, segments, reverse: true);
            AddAnnularCapFaces(faces, segments, innerEnd, segments, reverse: false);
        }
        else
        {
            AddFanCapFaces(faces, 0, segments, reverse: true);
            AddFanCapFaces(faces, segments, segments, reverse: false);
        }

        return new TriangleMesh3D(points, faces);
    }

    static (Vector3 xAxis, Vector3 yAxis) CreatePerpendicularAxes(Vector3 tangent)
    {
        var z = tangent.Normalize;
        var x = MathF.Abs(Vector3.Dot(Vector3.UnitX, z)) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var y = Vector3.Cross(z, x).Normalize;
        x = Vector3.Cross(y, z).Normalize;
        return (x, y);
    }

    static void AddRing(List<Point3D> points, Point3D center, Vector3 xAxis, Vector3 yAxis, float radius, int segments)
    {
        for (var i = 0; i < segments; i++)
        {
            var t = MathF.Tau * i / segments;
            points.Add(center + xAxis * (MathF.Cos(t) * radius) + yAxis * (MathF.Sin(t) * radius));
        }
    }

    static void AddTubeSideFaces(List<Integer3> faces, int startA, int startB, int segments, bool reverse)
    {
        for (var i = 0; i < segments; i++)
        {
            var j = (i + 1) % segments;
            var a = startA + i;
            var b = startA + j;
            var c = startB + j;
            var d = startB + i;
            if (reverse)
            {
                faces.Add(new Integer3(a, c, b));
                faces.Add(new Integer3(a, d, c));
            }
            else
            {
                faces.Add(new Integer3(a, b, c));
                faces.Add(new Integer3(a, c, d));
            }
        }
    }

    static void AddFanCapFaces(List<Integer3> faces, int center, int ringStart, bool reverse)
    {
        for (var i = 0; i < ringStart; i++)
        {
            var j = (i + 1) % ringStart;
            faces.Add(reverse
                ? new Integer3(center, ringStart + j, ringStart + i)
                : new Integer3(center, ringStart + i, ringStart + j));
        }
    }

    static void AddAnnularCapFaces(List<Integer3> faces, int outerStart, int innerStart, int segments, bool reverse)
    {
        for (var i = 0; i < segments; i++)
        {
            var j = (i + 1) % segments;
            var a = outerStart + i;
            var b = outerStart + j;
            var c = innerStart + j;
            var d = innerStart + i;
            if (reverse)
            {
                faces.Add(new Integer3(a, c, b));
                faces.Add(new Integer3(a, d, c));
            }
            else
            {
                faces.Add(new Integer3(a, b, c));
                faces.Add(new Integer3(a, c, d));
            }
        }
    }

    static List<Vector2> SampleCircleProfile(float radius, int segments)
        => Enumerable.Range(0, segments).Select(i =>
        {
            var t = MathF.Tau * i / segments;
            return new Vector2(MathF.Cos(t) * radius, MathF.Sin(t) * radius);
        }).ToList();
}
