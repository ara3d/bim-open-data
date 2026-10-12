using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>The plane a face ring lies in: an origin, the normal, and the in-plane axes U and V
/// that give each point its 2D coordinates.</summary>
public readonly record struct FacePlane(Vector3 Origin, Vector3 Normal, Vector3 U, Vector3 V)
{
    public static FacePlane OfTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a);
        if (normal.LengthSquared() < 1e-12f)
            return Newell([a, b, c]);
        normal = normal.Normalize;
        var u = MathF.Abs(normal.Y) < 0.9f
            ? Vector3.Cross(Vector3.UnitY, normal).Normalize
            : Vector3.Cross(Vector3.UnitX, normal).Normalize;
        var v = Vector3.Cross(normal, u);
        return new FacePlane(a, normal, u, v);
    }

    public static FacePlane OfRing(IReadOnlyList<Vector3> points)
        => points.Count == 3 ? OfTriangle(points[0], points[1], points[2]) : Newell(points);

    /// <summary>Newell's method: the normal that fits a non-planar or many-sided ring best.</summary>
    public static FacePlane Newell(IReadOnlyList<Vector3> points)
    {
        var normal = Vector3.Zero;
        float nx = 0, ny = 0, nz = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % points.Count];
            nx += (p0.Y - p1.Y) * (p0.Z + p1.Z);
            ny += (p0.Z - p1.Z) * (p0.X + p1.X);
            nz += (p0.X - p1.X) * (p0.Y + p1.Y);
        }
        normal = new Vector3(nx, ny, nz);
        if (normal.LengthSquared() < 1e-12f)
            normal = Vector3.UnitZ;
        else
            normal = normal.Normalize;
        var u = MathF.Abs(normal.Y) < 0.9f
            ? Vector3.Cross(Vector3.UnitY, normal).Normalize
            : Vector3.Cross(Vector3.UnitX, normal).Normalize;
        var v = Vector3.Cross(normal, u);
        return new FacePlane(points[0], normal, u, v);
    }

    public static FacePlane FromFrame(Frame3D frame)
        => new(frame.Origin.Vector3, frame.Z, frame.X, frame.Y);

    public Vector2 Project(Vector3 p)
    {
        var d = p - Origin;
        return new Vector2(Vector3.Dot(d, U), Vector3.Dot(d, V));
    }

    public Vector3 Unproject(Vector2 p2)
        => Origin + U * p2.X + V * p2.Y;
}
