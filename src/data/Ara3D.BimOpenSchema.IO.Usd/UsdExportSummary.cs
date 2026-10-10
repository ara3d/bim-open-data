namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>What one export wrote, counted as it was written.</summary>
/// <param name="Materials">Material prims: one per row of the BOS material table.</param>
/// <param name="Prototypes">Prototype meshes: one per row of the BOS mesh table.</param>
/// <param name="Descriptors">Parameter attributes declared under /Model/Descriptors: one per BOS
/// descriptor that has a value on some element.</param>
/// <param name="Entities">Entity prims: one per row of the BOS entity table.</param>
/// <param name="Elements">Entity prims with geometry (Xform, kind component): entities with at least
/// one instance that has a mesh. The other entities are Scope prims.</param>
/// <param name="Instances">Instance prims, including those under /Model/Unassigned.</param>
/// <param name="UnassignedInstances">Instances whose entity index is missing or out of range.</param>
/// <param name="InstancesWithoutMesh">BOS instances not written because their mesh index is missing or out of range.</param>
/// <param name="Attributes">bim: identity attributes and parameter properties written on entity prims.</param>
/// <param name="Relationships">Relationship targets written from the BOS relations table.</param>
/// <param name="RelationsLeftOut">Relations not written because an entity index is missing or out of
/// range, or the relation type is unknown.</param>
/// <param name="ParametersWithoutValue">Parameters left out because the value (for an entity reference,
/// the target entity) or the descriptor is missing.</param>
/// <param name="DuplicateParameters">Parameters left out because their entity already had a value for the
/// same descriptor; the first value in table order is kept.</param>
public sealed record UsdExportSummary(
    int Materials,
    int Prototypes,
    int Descriptors,
    int Entities,
    int Elements,
    int Instances,
    int UnassignedInstances,
    int InstancesWithoutMesh,
    int Attributes,
    int Relationships,
    int RelationsLeftOut,
    int ParametersWithoutValue,
    int DuplicateParameters);
