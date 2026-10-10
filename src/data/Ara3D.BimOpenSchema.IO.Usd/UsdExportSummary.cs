namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>What one export wrote, counted as it was written.</summary>
/// <param name="Materials">Material prims: one per row of the BOS material table.</param>
/// <param name="Prototypes">Prototype meshes: one per row of the BOS mesh table.</param>
/// <param name="Descriptors">Parameter attributes declared under /Model/Descriptors: one per BOS
/// descriptor that has a value on some element.</param>
/// <param name="Elements">Element prims: entities with at least one instance that has a mesh.</param>
/// <param name="Instances">Instance prims, including those under /Model/Unassigned.</param>
/// <param name="UnassignedInstances">Instances whose entity index is missing or out of range.</param>
/// <param name="InstancesWithoutMesh">BOS instances not written because their mesh index is missing or out of range.</param>
/// <param name="Attributes">bim: attributes written on element prims.</param>
/// <param name="ParametersWithoutValue">Parameters left out because the value, its descriptor, or (for an
/// entity reference) the target's name is missing.</param>
/// <param name="DuplicateParameters">Parameters left out because their entity already had a value for the
/// same descriptor; the first value in table order is kept.</param>
public sealed record UsdExportSummary(
    int Materials,
    int Prototypes,
    int Descriptors,
    int Elements,
    int Instances,
    int UnassignedInstances,
    int InstancesWithoutMesh,
    int Attributes,
    int ParametersWithoutValue,
    int DuplicateParameters);
