using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>Collects the triangles of one solid into one mesh. Points that round to the same
/// 10 um cell on every axis share one vertex: welding is by position, not by topology.</summary>
public sealed class WeldedMeshBuilder
{
    const float CellsPerMetre = 1e5f;

    readonly List<Point3D> _points = [];
    readonly List<Integer3> _faces = [];
    readonly Dictionary<(int, int, int), int> _indices = [];

    public int PointCount => _points.Count;
    public int FaceCount => _faces.Count;

    /// <summary>The vertex index for <paramref name="p"/>, adding a vertex when its cell has none.</summary>
    public int Index(Vector3 p)
    {
        var key = Quantize(p);
        if (_indices.TryGetValue(key, out var index))
            return index;
        index = _points.Count;
        _points.Add(p);
        _indices[key] = index;
        return index;
    }

    public void AddFace(Integer3 face)
        => _faces.Add(face);

    /// <summary>The mesh so far. It shares the builder's lists, so call it once, after the last face.</summary>
    public TriangleMesh3D ToMesh()
        => new(_points, _faces);

    static (int, int, int) Quantize(Vector3 p)
        => ((int)MathF.Round(p.X * CellsPerMetre), (int)MathF.Round(p.Y * CellsPerMetre), (int)MathF.Round(p.Z * CellsPerMetre));
}
