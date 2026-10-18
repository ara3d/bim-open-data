using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>In-plane projection for a planar face.</summary>
public sealed class PlanarMap(FacePlane plane) : SurfaceMap
{
    public override IReadOnlyList<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
        => ring.Select(plane.Project).ToList();

    public override Vector3 Unproject(Vector2 uv) => plane.Unproject(uv);

    /// <summary>A point between points of the plane lies on it.</summary>
    public override float ChordDeviation(Vector2 uv, Vector3 chordPoint) => 0;
}
