using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>Whole-mesh operations that build a new mesh from existing ones.</summary>
public static class MeshOps
{
    public static TriangleMesh3D Merge(IReadOnlyList<TriangleMesh3D> meshes)
    {
        if (meshes.Count == 0)
            return new TriangleMesh3D([], []);
        if (meshes.Count == 1)
            return meshes[0];

        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        foreach (var mesh in meshes)
        {
            var offset = points.Count;
            points.AddRange(mesh.Points);
            faces.AddRange(mesh.FaceIndices.Select(f => new Integer3(f.A + offset, f.B + offset, f.C + offset)));
        }
        return new TriangleMesh3D(points, faces);
    }

    public static TriangleMesh3D Transform(TriangleMesh3D mesh, Matrix4x4 matrix)
    {
        var points = mesh.Points.Select(p =>
        {
            var v = p.Vector3.Transform(matrix);
            return new Point3D(v.X, v.Y, v.Z);
        }).ToList();
        // A mirror (negative-determinant operator) reverses handedness; flip triangle winding so
        // world-space normals stay outward.
        var faces = LinearDeterminant(matrix) < 0
            ? mesh.FaceIndices.Select(f => new Integer3(f.A, f.C, f.B)).ToList()
            : mesh.FaceIndices;
        return new TriangleMesh3D(points, faces);
    }

    /// <summary>Determinant of the upper-left 3×3 (rotation/scale/mirror) block of an affine transform.</summary>
    public static float LinearDeterminant(Matrix4x4 m)
        => m.M11 * (m.M22 * m.M33 - m.M23 * m.M32)
         - m.M12 * (m.M21 * m.M33 - m.M23 * m.M31)
         + m.M13 * (m.M21 * m.M32 - m.M22 * m.M31);
}
