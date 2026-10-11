using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>The plane a face ring lies in: an origin, the normal, and the in-plane axes U and V
/// that give each point its 2D coordinates.</summary>
internal readonly record struct FacePlane(Vector3 Origin, Vector3 Normal, Vector3 U, Vector3 V)
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

/// <summary>In-plane projection for a planar face.</summary>
internal sealed class PlanarMap(FacePlane plane) : SurfaceMap
{
    public override List<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
        => ring.Select(plane.Project).ToList();

    public override Vector3 Unproject(Vector2 uv) => plane.Unproject(uv);

    /// <summary>The face's declared IFCPLANE or IFCCURVEBOUNDEDPLANE, when the ring lies in it.
    /// A shared IFCPLANE (the golden box, say) need not contain every face's boundary; then the
    /// caller falls back to the ring's own plane.</summary>
    public static bool TryFromFaceSurface(MeshingContext ctx, IfcEntity face, IReadOnlyList<Vector3> outer, out FacePlane plane)
    {
        plane = default;
        var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
        if (surface == null)
            return false;

        var ok = surface.GetEntityName() switch
        {
            "IFCPLANE" => TryGetPlaneSurface(ctx, surface, out plane),
            "IFCCURVEBOUNDEDPLANE" => TryGetCurveBoundedPlaneSurface(ctx, surface, out plane),
            _ => false,
        };
        if (!ok || outer.Count < 3)
            return ok;

        var maxDist = 0f;
        foreach (var p in outer)
            maxDist = MathF.Max(maxDist, MathF.Abs(Vector3.Dot(p - plane.Origin, plane.Normal)));
        return maxDist <= PlaneContainmentTolerance(ctx);
    }

    static float PlaneContainmentTolerance(MeshingContext ctx)
        => MathF.Max(Tolerance.Eps, MathF.Abs((float)ctx.LengthScale) * 1e-4f);

    static bool TryGetPlaneSurface(MeshingContext ctx, IfcEntity planeEntity, out FacePlane plane)
    {
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, planeEntity, IfcElementarySurface.Instance.Position);
        plane = FacePlane.FromFrame(frame);
        return true;
    }

    static bool TryGetCurveBoundedPlaneSurface(MeshingContext ctx, IfcEntity bounded, out FacePlane plane)
    {
        var basis = MeshHelpers.ResolveRequired(ctx, bounded, IfcCurveBoundedPlane.Instance.BasisSurface);
        return TryGetPlaneSurface(ctx, basis, out plane);
    }
}
