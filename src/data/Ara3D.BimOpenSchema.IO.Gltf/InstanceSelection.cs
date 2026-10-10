namespace Ara3D.BimOpenSchema.IO.Gltf;

/// <summary>One instance to write, with every index checked against its table. Material is -1
/// when the instance has none or names a row the Materials table does not have.</summary>
internal readonly record struct Placement(int Entity, int Mesh, int Material, int Transform);

/// <summary>The instances an export writes, in table order, and what it left out on the way.
/// Every index in the Instances table is checked here against the table it points into, so a
/// hand-built or damaged BimGeometry (an InstanceFlags column shorter than the others, a mesh,
/// material, or transform past the end of its table) is skipped or defaulted and counted
/// rather than thrown on. Mesh offsets into the vertex and index buffers are trusted.</summary>
internal sealed record InstanceSelection(
    List<Placement> Placements,
    int SkippedEmpty,
    int SkippedBadTransform,
    int DefaultedMaterials,
    int UnmatchedEntityIndices)
{
    public static InstanceSelection Select(BimGeometry g, GlbExportOptions options)
    {
        var requested = options.EntityIndices is null ? null : new HashSet<int>(options.EntityIndices);
        var meshCount = Math.Min(g.MeshIndexOffset.Length, g.MeshVertexOffset.Length);
        var materialCount = MinLength(g.MaterialRed, g.MaterialGreen, g.MaterialBlue, g.MaterialAlpha, g.MaterialMetallic, g.MaterialRoughness);
        var transformCount = MinLength(g.TransformTX, g.TransformTY, g.TransformTZ, g.TransformQX, g.TransformQY,
            g.TransformQZ, g.TransformQW, g.TransformSX, g.TransformSY, g.TransformSZ);

        var placements = new List<Placement>();
        var matched = new HashSet<int>();
        int skippedEmpty = 0, skippedBadTransform = 0, defaultedMaterials = 0;
        for (var i = 0; i < g.InstanceMeshIndex.Length; i++)
        {
            if (!options.IncludeHidden && IsHidden(g, i))
                continue;
            var entity = At(g.InstanceEntityIndex, i);
            if (requested is not null && !requested.Contains(entity))
                continue;
            var mesh = g.InstanceMeshIndex[i];
            if (mesh < 0 || mesh >= meshCount || IsEmpty(g.GetMeshSlice(mesh)))
            {
                skippedEmpty++;
                continue;
            }
            var transform = At(g.InstanceTransformIndex, i);
            if (transform < 0 || transform >= transformCount)
            {
                skippedBadTransform++;
                continue;
            }
            var material = At(g.InstanceMaterialIndex, i);
            if (material < -1 || material >= materialCount)
            {
                defaultedMaterials++;
                material = -1;
            }
            placements.Add(new Placement(entity, mesh, material, transform));
            matched.Add(entity);
        }
        var unmatched = requested?.Count(e => !matched.Contains(e)) ?? 0;
        return new InstanceSelection(placements, skippedEmpty, skippedBadTransform, defaultedMaterials, unmatched);
    }

    /// <summary>Hidden when the flag says so; an instance past the end of InstanceFlags (files
    /// older than the column, hand-built geometry) is visible.</summary>
    private static bool IsHidden(BimGeometry g, int instance)
        => instance < g.InstanceFlags.Length
           && (g.InstanceFlags[instance] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0;

    private static bool IsEmpty(Ara3D.Models.MeshSliceStruct slice)
        => slice.IndexCount == 0 || slice.VertexCount == 0;

    /// <summary>The value at <paramref name="i"/>, or -1 (none) past the end of a short column.</summary>
    private static int At(int[] column, int i)
        => i < column.Length ? column[i] : -1;

    private static int MinLength(params Array[] columns)
        => columns.Min(c => c.Length);
}
