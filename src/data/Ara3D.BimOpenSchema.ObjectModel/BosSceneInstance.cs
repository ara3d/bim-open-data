#nullable enable
using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

/// <summary>One row of the BOS Instances table with every index checked against the table it
/// points into. <see cref="Entity"/> is -1 when the entity index is missing or out of range,
/// <see cref="Material"/> is -1 when the instance has no material or names a row the Materials
/// table does not have. An instance from <see cref="BosScene.Select"/> or
/// <see cref="BosScene.InstancesOf"/> always has a valid <see cref="Mesh"/> and
/// <see cref="Transform"/>; <see cref="BosScene.Instance"/> gives -1 for an invalid one.</summary>
public readonly record struct BosInstance(int Index, int Entity, int Mesh, int Material, int Transform, bool IsHidden)
{
    /// <summary>True when the mesh and transform are rows of their tables, so the instance can be placed.</summary>
    public bool IsDrawable => Mesh >= 0 && Transform >= 0;
}

/// <summary>Which instances <see cref="BosScene.Select"/> returns.</summary>
public sealed record BosSceneFilter
{
    /// <summary>The entities whose instances to return, as indices into the Entities table.
    /// Null returns every entity's; an empty set returns none. Instances with no entity match
    /// only when this is null.</summary>
    public IReadOnlyCollection<int>? Entities { get; init; }

    /// <summary>Also return instances flagged hidden (BimGeometry.InstanceFlagEnum.IsHidden).</summary>
    public bool IncludeHidden { get; init; }

    /// <summary>Also return instances whose mesh has no vertices or no indices. A writer whose
    /// format allows an empty mesh (USD) sets this; glTF does not allow an empty accessor.</summary>
    public bool IncludeEmptyMeshes { get; init; }

    public static BosSceneFilter Default { get; } = new();
}

/// <summary>The instances a filter selected, in table order, and how many of the instances it
/// matched were left out or changed on the way. Hidden instances left out by the filter are
/// not counted: hiding them is the filter's choice, not a fault in the data.</summary>
/// <param name="Instances">The selected instances; each is drawable.</param>
/// <param name="SkippedBadMesh">Matched instances whose mesh index is out of range of the Meshes table.</param>
/// <param name="SkippedEmptyMesh">Matched instances whose mesh is empty, when <see cref="BosSceneFilter.IncludeEmptyMeshes"/> is off.</param>
/// <param name="SkippedBadTransform">Matched instances whose transform index is out of range of the Transforms table.</param>
/// <param name="DefaultedMaterials">Selected instances whose material index is out of range of the
/// Materials table, returned with material -1. Index -1 (no material) is not counted.</param>
/// <param name="UnmatchedEntities">Distinct indices in <see cref="BosSceneFilter.Entities"/> with no
/// selected instance: out of range, or with no instance that passed. Zero when no entities were given.</param>
public sealed record BosSelection(
    IReadOnlyList<BosInstance> Instances,
    int SkippedBadMesh,
    int SkippedEmptyMesh,
    int SkippedBadTransform,
    int DefaultedMaterials,
    int UnmatchedEntities);
