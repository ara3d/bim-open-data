namespace Ara3D.BimOpenSchema.IO.Gltf;

/// <summary>Which instances of a BOS model go into the glTF.</summary>
public sealed record GlbExportOptions
{
    /// <summary>The entities to write, as indices into the Entities table. Null writes every
    /// entity that has geometry; an empty set writes a scene with no nodes.</summary>
    public IReadOnlyCollection<int>? EntityIndices { get; init; }

    /// <summary>Also write instances flagged hidden (BimGeometry.InstanceFlagEnum.IsHidden).
    /// Off by default, as the SDK's GltfWriter and the DataModel's IsHidden column treat them.</summary>
    public bool IncludeHidden { get; init; }

    public static GlbExportOptions Default { get; } = new();
}
