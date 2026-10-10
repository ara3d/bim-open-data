using SharpGLTF.Schema2;

namespace Ara3D.BimOpenSchema.IO.Gltf;

/// <summary>Writes a BIM Open Schema model as binary glTF (.glb). One glTF node per BOS
/// instance; each node's extras hold the instance's entity index and the entity's GlobalId.
/// Output is y-up and in metres, as glTF requires.</summary>
public static class BosGlb
{
    /// <summary>Node extras key: the instance's row in the BOS Entities table (an int).</summary>
    public const string EntityIndexKey = "entityIndex";

    /// <summary>Node extras key: the entity's GlobalId (IFC GlobalId, Revit UniqueId), when it has one.</summary>
    public const string GlobalIdKey = "globalId";

    /// <summary>Reads a .bos archive and writes its geometry to a .glb file.</summary>
    public static GlbExportSummary WriteGlb(string bosPath, string glbPath, GlbExportOptions? options = null)
        => ParquetUtils.ReadBimDataFromParquetZip(bosPath).WriteGlb(glbPath, options);

    /// <summary>Writes the geometry of a loaded BOS model to a .glb file.</summary>
    public static GlbExportSummary WriteGlb(this IBimData data, string glbPath, GlbExportOptions? options = null)
    {
        var (model, summary) = data.ToGltf(options);
        model.SaveGLB(glbPath);
        return summary with { Bytes = new FileInfo(glbPath).Length };
    }

    /// <summary>Builds the glTF model in memory, for a caller that wants another container
    /// (model.SaveGLTF, model.WriteGLB to a stream) or to add to the scene first.</summary>
    public static (ModelRoot Model, GlbExportSummary Summary) ToGltf(this IBimData data, GlbExportOptions? options = null)
        => new GltfSceneBuilder(data, options ?? GlbExportOptions.Default).Build();
}
