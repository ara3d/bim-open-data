using System.Text;
using System.Text.Json;

namespace Ara3D.Step.CorpusHarness;

/// <summary>A minimal glTF 2.0 binary writer for the gallery: one mesh with up to three primitives, unindexed
/// triangles with a colour per vertex, and two unindexed line sets (every discretized edge, and the edges of the
/// faces that did not mesh). Positions are float32 after <see cref="Centre"/> is subtracted, so a model placed far
/// from its origin keeps its precision. No library: the format is a JSON chunk and a binary chunk.</summary>
public sealed class GlbWriter
{
    readonly List<float> _tri = [];
    readonly List<float> _col = [];
    readonly List<float> _edges = [];
    readonly List<float> _failed = [];

    public double[] Centre { get; set; } = [0, 0, 0];

    public int Triangles => _tri.Count / 9;
    public int EdgeSegments => _edges.Count / 6;
    public int FailedSegments => _failed.Count / 6;

    public void Triangle((double x, double y, double z) a, (double x, double y, double z) b, (double x, double y, double z) c, (float r, float g, float b) colour)
    {
        foreach (var p in new[] { a, b, c })
        {
            _tri.Add((float)(p.x - Centre[0]));
            _tri.Add((float)(p.y - Centre[1]));
            _tri.Add((float)(p.z - Centre[2]));
            _col.Add(colour.r);
            _col.Add(colour.g);
            _col.Add(colour.b);
        }
    }

    public void Segment(IReadOnlyList<(double x, double y, double z)> polyline, bool failed)
    {
        var target = failed ? _failed : _edges;
        for (var i = 0; i + 1 < polyline.Count; i++)
            foreach (var p in new[] { polyline[i], polyline[i + 1] })
            {
                target.Add((float)(p.x - Centre[0]));
                target.Add((float)(p.y - Centre[1]));
                target.Add((float)(p.z - Centre[2]));
            }
    }

    public void Write(string path)
    {
        var bin = new MemoryStream();
        var views = new List<object>();
        var accessors = new List<object>();
        var primitives = new List<object>();
        int Accessor(List<float> data, int components)
        {
            var count = data.Count / components;
            var offset = (int)bin.Length;
            var bytes = new byte[data.Count * 4];
            Buffer.BlockCopy(data.ToArray(), 0, bytes, 0, bytes.Length);
            bin.Write(bytes);
            while (bin.Length % 4 != 0)
                bin.WriteByte(0);
            var min = Enumerable.Range(0, components).Select(k => Enumerable.Range(0, count).Select(i => data[i * components + k]).DefaultIfEmpty(0).Min()).ToArray();
            var max = Enumerable.Range(0, components).Select(k => Enumerable.Range(0, count).Select(i => data[i * components + k]).DefaultIfEmpty(0).Max()).ToArray();
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = bytes.Length, target = 34962 });
            accessors.Add(new { bufferView = views.Count - 1, componentType = 5126, count, type = components == 3 ? "VEC3" : "VEC4", min, max });
            return accessors.Count - 1;
        }
        var materials = new List<object>
        {
            new { name = "faces", pbrMetallicRoughness = new { metallicFactor = 0.0, roughnessFactor = 0.85 }, doubleSided = true },
            new { name = "edges", pbrMetallicRoughness = new { baseColorFactor = new[] { 0.15, 0.17, 0.2, 1.0 }, metallicFactor = 0.0 } },
            new { name = "failed", pbrMetallicRoughness = new { baseColorFactor = new[] { 0.9, 0.1, 0.1, 1.0 }, metallicFactor = 0.0 } },
        };
        if (_tri.Count > 0)
            primitives.Add(new { attributes = new Dictionary<string, int> { ["POSITION"] = Accessor(_tri, 3), ["COLOR_0"] = Accessor(_col, 3) }, material = 0, mode = 4 });
        if (_edges.Count > 0)
            primitives.Add(new { attributes = new Dictionary<string, int> { ["POSITION"] = Accessor(_edges, 3) }, material = 1, mode = 1 });
        if (_failed.Count > 0)
            primitives.Add(new { attributes = new Dictionary<string, int> { ["POSITION"] = Accessor(_failed, 3) }, material = 2, mode = 1 });
        var json = JsonSerializer.Serialize(new
        {
            asset = new { version = "2.0", generator = "step-corpus-harness gallery" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0, name = Path.GetFileNameWithoutExtension(path) } },
            meshes = new[] { new { primitives } },
            materials,
            accessors,
            bufferViews = views,
            buffers = new[] { new { byteLength = (int)bin.Length } },
        });
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonPadded = (jsonBytes.Length + 3) / 4 * 4;
        using var f = new BinaryWriter(File.Create(path));
        f.Write(0x46546C67u);
        f.Write(2u);
        f.Write((uint)(12 + 8 + jsonPadded + 8 + bin.Length));
        f.Write((uint)jsonPadded);
        f.Write(0x4E4F534Au);
        f.Write(jsonBytes);
        for (var i = jsonBytes.Length; i < jsonPadded; i++)
            f.Write((byte)0x20);
        f.Write((uint)bin.Length);
        f.Write(0x004E4942u);
        f.Write(bin.ToArray());
    }
}
