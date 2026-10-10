using System.Numerics;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>World-space views of BOS geometry for assertions: each instance's triangles placed by
/// its transform (scale, then rotation, then translation, as BimGeometry documents).</summary>
public static class GeometryQueries
{
    /// <summary>The world-space triangles of one instance.</summary>
    public static IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> Triangles(this BimGeometry g, int instance)
    {
        var m = g.InstanceMeshIndex[instance];
        var t = g.InstanceTransformIndex[instance];
        var rotation = new Quaternion(g.TransformQX[t], g.TransformQY[t], g.TransformQZ[t], g.TransformQW[t]);
        var scale = new Vector3(g.TransformSX[t], g.TransformSY[t], g.TransformSZ[t]);
        var translation = new Vector3(g.TransformTX[t], g.TransformTY[t], g.TransformTZ[t]);
        var vertexOffset = g.MeshVertexOffset[m];
        var first = g.MeshIndexOffset[m];
        var end = m + 1 < g.MeshIndexOffset.Length ? g.MeshIndexOffset[m + 1] : g.IndexBuffer.Length;

        Vector3 World(int corner)
        {
            var v = vertexOffset + g.IndexBuffer[corner];
            var local = new Vector3(g.VertexX[v], g.VertexY[v], g.VertexZ[v]) / BimGeometry.VertexMultiplier;
            return Vector3.Transform(local * scale, rotation) + translation;
        }

        for (var c = first; c + 2 < end; c += 3)
            yield return (World(c), World(c + 1), World(c + 2));
    }

    /// <summary>The world bounding box of every entity that has geometry, by entity index.</summary>
    public static Dictionary<int, (Vector3 Min, Vector3 Max)> EntityBoxes(this BimGeometry g)
    {
        var boxes = new Dictionary<int, (Vector3 Min, Vector3 Max)>();
        for (var i = 0; i < g.InstanceEntityIndex.Length; i++)
        {
            var e = g.InstanceEntityIndex[i];
            var box = boxes.TryGetValue(e, out var b) ? b : (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            foreach (var (a, bb, c) in g.Triangles(i))
                box = (Vector3.Min(Vector3.Min(box.Item1, a), Vector3.Min(bb, c)), Vector3.Max(Vector3.Max(box.Item2, a), Vector3.Max(bb, c)));
            boxes[e] = box;
        }
        return boxes;
    }

    /// <summary>Signed volume enclosed by an instance's triangles: positive when a closed mesh's
    /// faces point outward.</summary>
    public static double SignedVolume(this BimGeometry g, int instance)
        => g.Triangles(instance).Sum(t => (double)Vector3.Dot(t.A, Vector3.Cross(t.B, t.C)) / 6);

    public static double Area(this BimGeometry g, int instance)
        => g.Triangles(instance).Sum(t => (double)Vector3.Cross(t.B - t.A, t.C - t.A).Length() / 2);
}
