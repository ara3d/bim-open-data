namespace Ara3D.BimOpenSchema.IO.Gltf;

/// <summary>What an export wrote. Counts are of the glTF output, not of the BOS input.</summary>
/// <param name="Nodes">glTF nodes, one per BOS instance written.</param>
/// <param name="Meshes">glTF meshes: one per pair of BOS mesh and BOS material in use.</param>
/// <param name="Materials">glTF materials: one per BOS material in use.</param>
/// <param name="Triangles">Triangles drawn: each node's mesh counted once per node.</param>
/// <param name="SkippedEmpty">Selected instances left out because their mesh has no triangles
/// or their mesh index is out of range; glTF does not allow an empty accessor.</param>
/// <param name="UnmatchedEntityIndices">Distinct indices in <see cref="GlbExportOptions.EntityIndices"/>
/// that produced no node: out of range of the Entities table, or with no instance that could be
/// drawn (none at all, only empty meshes, or only hidden ones while IncludeHidden is off). Zero
/// when no filter was given. When it equals the number of indices given, the file has no nodes;
/// STEP ids or Revit element ids passed as entity indices end up here.</param>
/// <param name="Bytes">Size of the .glb file, or null when nothing was written yet.</param>
public sealed record GlbExportSummary(
    int Nodes,
    int Meshes,
    int Materials,
    long Triangles,
    int SkippedEmpty,
    int UnmatchedEntityIndices,
    long? Bytes = null);
