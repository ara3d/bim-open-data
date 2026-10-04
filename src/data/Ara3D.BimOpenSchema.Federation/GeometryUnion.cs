namespace Ara3D.BimOpenSchema.Federation;

/// <summary>Concatenates the geometry of several documents into one BimGeometry, in input
/// order, the way BimDataBuilder.AddBimData concatenates their entities: each part keeps every
/// instance, mesh, material, and transform, with its indices shifted past the parts before it.
/// Nothing is shared or deduplicated across parts, so an element that two models both draw
/// (the same GlobalId in each) is drawn twice, once per model, each instance pointing at its
/// own model's entity.</summary>
public static class GeometryUnion
{
    /// <summary>One document's geometry and the number of entities it adds to the union, which
    /// is how far its instances' entity indices move.</summary>
    public sealed record Part(BimGeometry Geometry, int EntityCount);

    public static BimGeometry Union(IReadOnlyList<Part> parts) => new()
    {
        InstanceEntityIndex = Concat(parts, g => g.InstanceEntityIndex, OffsetsOf(parts, p => p.EntityCount)),
        InstanceMaterialIndex = Concat(parts, g => g.InstanceMaterialIndex, OffsetsOf(parts, p => p.Geometry.MaterialRed.Length)),
        InstanceMeshIndex = Concat(parts, g => g.InstanceMeshIndex, OffsetsOf(parts, p => p.Geometry.MeshVertexOffset.Length)),
        InstanceTransformIndex = Concat(parts, g => g.InstanceTransformIndex, OffsetsOf(parts, p => p.Geometry.TransformTX.Length)),
        InstanceFlags = Concat(parts, g => g.InstanceFlags),
        VertexX = Concat(parts, g => g.VertexX),
        VertexY = Concat(parts, g => g.VertexY),
        VertexZ = Concat(parts, g => g.VertexZ),
        // Index buffer entries are local to their mesh, so they are copied as they are.
        IndexBuffer = Concat(parts, g => g.IndexBuffer),
        MeshVertexOffset = Concat(parts, g => g.MeshVertexOffset, OffsetsOf(parts, p => p.Geometry.VertexX.Length)),
        MeshIndexOffset = Concat(parts, g => g.MeshIndexOffset, OffsetsOf(parts, p => p.Geometry.IndexBuffer.Length)),
        MaterialRed = Concat(parts, g => g.MaterialRed),
        MaterialGreen = Concat(parts, g => g.MaterialGreen),
        MaterialBlue = Concat(parts, g => g.MaterialBlue),
        MaterialAlpha = Concat(parts, g => g.MaterialAlpha),
        MaterialRoughness = Concat(parts, g => g.MaterialRoughness),
        MaterialMetallic = Concat(parts, g => g.MaterialMetallic),
        TransformTX = Concat(parts, g => g.TransformTX),
        TransformTY = Concat(parts, g => g.TransformTY),
        TransformTZ = Concat(parts, g => g.TransformTZ),
        TransformQX = Concat(parts, g => g.TransformQX),
        TransformQY = Concat(parts, g => g.TransformQY),
        TransformQZ = Concat(parts, g => g.TransformQZ),
        TransformQW = Concat(parts, g => g.TransformQW),
        TransformSX = Concat(parts, g => g.TransformSX),
        TransformSY = Concat(parts, g => g.TransformSY),
        TransformSZ = Concat(parts, g => g.TransformSZ),
    };

    // The running total of each part's size before it: part i's indices move by offsets[i].
    private static int[] OffsetsOf(IReadOnlyList<Part> parts, Func<Part, int> size)
    {
        var offsets = new int[parts.Count];
        for (var i = 1; i < parts.Count; i++)
            offsets[i] = offsets[i - 1] + size(parts[i - 1]);
        return offsets;
    }

    private static T[] Concat<T>(IReadOnlyList<Part> parts, Func<BimGeometry, T[]> column)
        => parts.SelectMany(p => column(p.Geometry)).ToArray();

    // A negative index means "none" (an instance whose entity the converter could not find)
    // and stays negative rather than pointing into another part.
    private static int[] Concat(IReadOnlyList<Part> parts, Func<BimGeometry, int[]> column, int[] offsets)
        => parts.SelectMany((p, i) => column(p.Geometry).Select(x => x < 0 ? x : x + offsets[i])).ToArray();
}
