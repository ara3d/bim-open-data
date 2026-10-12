using System.Text.Json;
using System.Text.Json.Serialization;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>A mesher in <c>bench/ifc-mesher/glb/manifest.json</c>: its folder name, version, and the
/// flat colour its GLBs are drawn in (<c>#rrggbb</c>).</summary>
public sealed record GlbMesherInfo(string Name, string Version, string? Color);

/// <summary>One mesher's GLB of one file; <c>Path</c> is relative to the glb folder.</summary>
public sealed record GlbManifestEntry(string Path, int Triangles, int Instances, int Meshes, long Bytes, long ElapsedMs)
{
    public static GlbManifestEntry Of(string mesher, string fileName, GlbEntry entry)
        => new($"{mesher}/{fileName}", entry.Triangles, entry.Instances, entry.Meshes, entry.Bytes, entry.ElapsedMs);
}

/// <summary>A sample IFC file and the GLBs made from it, keyed by mesher name. A mesher with no output is absent.</summary>
public sealed record GlbManifestFile(string Name, IReadOnlyDictionary<string, GlbManifestEntry> Glbs);

/// <summary>The contract with the comparison page (docs/plans/mesher-comparison-page.md, camelCase JSON).</summary>
public sealed record GlbManifest(
    DateTime GeneratedUtc,
    IReadOnlyList<GlbMesherInfo> Meshers,
    IReadOnlyList<GlbManifestFile> Files)
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Write(FilePath path)
    {
        path.GetDirectory().Create();
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
