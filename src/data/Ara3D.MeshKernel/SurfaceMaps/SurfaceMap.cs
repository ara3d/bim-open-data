using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Maps face-bound points between 3D world space and a 2D domain suitable for triangulation,
/// and back. Planar faces use an in-plane projection (<see cref="PlanarMap"/>); parametric
/// surfaces (<see cref="CylinderMap"/>, <see cref="RevolutionMap"/>, <see cref="BSplineSurfaceMap"/>)
/// project into surface parameter space so wrapping curved patches triangulate without
/// self-overlap and unproject back onto the true curved surface.
/// </summary>
public abstract class SurfaceMap
{
    public abstract IReadOnlyList<Vector2> ProjectRing(IReadOnlyList<Vector3> ring);
    public abstract Vector3 Unproject(Vector2 uv);
}
