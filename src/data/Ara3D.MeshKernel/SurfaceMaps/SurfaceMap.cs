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

    /// <summary>
    /// How far <paramref name="chordPoint"/>, a point of a chord or flat triangle between surface
    /// points taken at domain position <paramref name="uv"/>, lies from the surface. The default is
    /// its distance to the surface point at <paramref name="uv"/>, which can only overstate the
    /// distance to the surface; a map with a closed form overrides it, and a plane answers 0, so
    /// planar faces are never refined.
    /// </summary>
    public virtual float ChordDeviation(Vector2 uv, Vector3 chordPoint)
        => chordPoint.Distance(Unproject(uv));

    /// <summary>How far the chord between the surface points at <paramref name="a"/> and
    /// <paramref name="b"/> strays from the surface at its midpoint.</summary>
    public float Sag(Vector2 a, Vector2 b)
        => ChordDeviation((a + b) * 0.5f, (Unproject(a) + Unproject(b)) * 0.5f);
}
