using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>Measurements of a triangle mesh.</summary>
public static class MeshMeasure
{
    public static Bounds3D Bounds(TriangleMesh3D mesh)
        => mesh.Points.Count == 0 ? Bounds3D.Empty : mesh.Points.Bounds();

    /// <summary>Volume by the divergence theorem; negative when the triangles wind inward.</summary>
    public static double SignedVolume(TriangleMesh3D mesh)
    {
        double volume = 0;
        foreach (var face in mesh.FaceIndices)
        {
            var a = mesh.Points[face.A].Vector3;
            var b = mesh.Points[face.B].Vector3;
            var c = mesh.Points[face.C].Vector3;
            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
        }
        return volume;
    }
}
