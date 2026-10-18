using SN = System.Numerics;

namespace Ara3D.Step.Mesher;

/// <summary>One shape item at one place: <see cref="Transform"/> carries the item's own coordinates (metres) to
/// the root's, row-vector convention. <see cref="ProductDefinitionId"/> is the product_definition whose shape
/// holds it, or 0 when no product reaches its representation; <see cref="Depth"/> counts assembly levels above
/// it (0 for a root product).</summary>
public readonly record struct StepPlacedItem(
    int ItemId,
    int RepresentationId,
    int ProductDefinitionId,
    SN.Matrix4x4 Transform,
    int Depth,
    StepUnits Units);

/// <summary>A link the mesher refused to follow because it leads back to an instance already on the path:
/// <see cref="Path"/> lists the ids from the first visit of <see cref="Path"/>'s last id to its repeat, and
/// <see cref="Link"/> names the kind of link (next_assembly_usage_occurrence or mapped_item).</summary>
public sealed record StepCycle(string Link, IReadOnlyList<int> Path);
