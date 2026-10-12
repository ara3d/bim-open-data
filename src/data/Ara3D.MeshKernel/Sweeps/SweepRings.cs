using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>The one strip of quads between two consecutive rings of a sweep, shared by revolutions,
/// swept disks, and directrix sweeps.</summary>
public static class SweepRings
{
    /// <summary>Joins two rings of equal length into a closed band of triangles, wrapping the last
    /// point back to the first.</summary>
    public static TriangleMesh3D Connect(IReadOnlyList<Point3D> row0, IReadOnlyList<Point3D> row1)
    {
        var points = row0.Concat(row1).ToList();
        var n = row0.Count;
        var faces = new List<Integer3>();
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            faces.Add(new Integer3(i, j, j + n));
            faces.Add(new Integer3(i, j + n, i + n));
        }
        return new TriangleMesh3D(points, faces);
    }
}
