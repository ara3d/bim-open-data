using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Maps face-bound points between 3D world space and a 2D domain suitable for triangulation,
/// and back. Planar faces use an in-plane projection (<see cref="PlanarMap"/>); parametric
/// surfaces (<see cref="CylinderMap"/>, <see cref="RevolutionMap"/>, <see cref="BSplineSurfaceMap"/>)
/// project into surface parameter space so wrapping curved patches triangulate without
/// self-overlap and unproject back onto the true curved surface.
/// </summary>
internal abstract class SurfaceMap
{
    public abstract List<Vector2> ProjectRing(IReadOnlyList<Vector3> ring);
    public abstract Vector3 Unproject(Vector2 uv);

    /// <summary>The map for a face: its declared surface when it is one of the four kinds above and
    /// the ring lies on it, else the plane of the ring itself.</summary>
    public static SurfaceMap Resolve(MeshingContext ctx, IfcEntity face, IReadOnlyList<Vector3> outer)
    {
        if (FaceBounds.IsFaceSurfaceEntity(face))
        {
            var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
            var surfaceName = surface?.GetEntityName();
            if (surfaceName == "IFCCYLINDRICALSURFACE"
                && CylinderMap.TryBuild(ctx, surface!, out var cyl))
                return cyl;
            if (surfaceName == "IFCSURFACEOFREVOLUTION"
                && RevolutionMap.TryBuild(ctx, surface!, out var rev))
                return rev;
            if (surfaceName is "IFCBSPLINESURFACEWITHKNOTS" or "IFCRATIONALBSPLINESURFACEWITHKNOTS" or "IFCBSPLINESURFACE"
                && BSplineSurfaceMap.TryBuild(ctx, surface!, out var bsp))
                return bsp;
            if (PlanarMap.TryFromFaceSurface(ctx, face, outer, out var facePlane))
                return new PlanarMap(facePlane);
        }
        return new PlanarMap(outer.Count == 3
            ? FacePlane.OfTriangle(outer[0], outer[1], outer[2])
            : FacePlane.Newell(outer));
    }
}
