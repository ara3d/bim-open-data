#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ara3D.BimOpenSchema;

/// <summary>
/// A read-only view over one BOS model that the readers and writers around BOS share, so
/// none of them re-derives it: which instances can be drawn and where, which are hidden,
/// each entity's GlobalId and name with absence as null, each entity's instances, parameters,
/// and relations, each entity's bounds, and parameter values typed and null when missing.
/// <para>
/// Every index in the Instances table is checked once, when the view is built, against the
/// table it points into, and each fault is counted: a mesh or transform index out of range
/// makes the instance undrawable, a material index out of range is read as no material (-1),
/// and an entity index out of range as no entity (-1). A column shorter than the Instances
/// table reads as -1 past its end, and an InstanceFlags column shorter than it (older files,
/// hand-built geometry) leaves the missing instances visible. Mesh offsets into the vertex and
/// index buffers are trusted. Table sizes are the shortest column of each table.
/// </para>
/// <para>
/// Absence: a GlobalId, name, or label is null when its string index is -1 (absent, as the
/// specification defines it), out of range, or names an empty string (files written before
/// -1 meant absent store an unset value as ""). A typed parameter value is null when the
/// value is missing, never 0.
/// </para>
/// <para>
/// Cost: the constructor is one pass over the Instances table and keeps one byte per
/// instance. The groupings by entity and the mesh bounds are each built once, on first use,
/// in time linear in their tables, and are safe to build from several threads.
/// </para>
/// </summary>
public sealed class BosScene
{
    [Flags]
    private enum Fault : byte
    {
        None = 0,
        Hidden = 1,
        BadMesh = 2,
        BadTransform = 4,
        BadMaterial = 8,
        NoEntity = 16,
        EmptyMesh = 32,
    }

    private readonly Fault[] _faults;
    private readonly Lazy<RowGroups> _instancesByEntity;
    private readonly Lazy<RowGroups> _parametersByEntity;
    private readonly Lazy<RowGroups> _relationsBySource;
    private readonly Lazy<BosBox?[]> _meshBounds;

    public BosScene(IBimData data)
    {
        Data = data;
        var g = Geometry = data.Geometry ?? new BimGeometry();
        EntityCount = data.Entities.Length;
        MeshCount = Math.Min(g.MeshIndexOffset.Length, g.MeshVertexOffset.Length);
        MaterialCount = MinLength(g.MaterialRed, g.MaterialGreen, g.MaterialBlue, g.MaterialAlpha, g.MaterialMetallic, g.MaterialRoughness);
        TransformCount = MinLength(g.TransformTX, g.TransformTY, g.TransformTZ, g.TransformQX, g.TransformQY,
            g.TransformQZ, g.TransformQW, g.TransformSX, g.TransformSY, g.TransformSZ);
        InstanceCount = g.InstanceMeshIndex.Length;

        var emptyMeshes = new bool[MeshCount];
        for (var m = 0; m < MeshCount; m++)
        {
            var slice = g.GetMeshSlice(m);
            emptyMeshes[m] = slice.IndexCount == 0 || slice.VertexCount == 0;
        }

        _faults = new Fault[InstanceCount];
        for (var i = 0; i < InstanceCount; i++)
        {
            var f = Fault.None;
            if (i < g.InstanceFlags.Length && (g.InstanceFlags[i] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0)
                f |= Fault.Hidden;
            var mesh = g.InstanceMeshIndex[i];
            if ((uint)mesh >= (uint)MeshCount)
                f |= Fault.BadMesh;
            else if (emptyMeshes[mesh])
                f |= Fault.EmptyMesh;
            if ((uint)At(g.InstanceTransformIndex, i) >= (uint)TransformCount)
                f |= Fault.BadTransform;
            var material = At(g.InstanceMaterialIndex, i);
            if (material < -1 || material >= MaterialCount)
                f |= Fault.BadMaterial;
            if (!IsEntity(At(g.InstanceEntityIndex, i)))
                f |= Fault.NoEntity;
            _faults[i] = f;

            if ((f & Fault.BadMesh) != 0)
                InstancesWithBadMesh++;
            else if ((f & Fault.BadTransform) != 0)
                InstancesWithBadTransform++;
            else
            {
                if ((f & Fault.BadMaterial) != 0) InstancesWithBadMaterial++;
                if ((f & Fault.NoEntity) != 0) InstancesWithoutEntity++;
            }
        }

        // Key EntityCount holds the drawable instances with no entity.
        _instancesByEntity = new(() => new RowGroups(EntityCount + 1, InstanceCount,
            i => !IsDrawable(i) ? -1 : (_faults[i] & Fault.NoEntity) != 0 ? EntityCount : g.InstanceEntityIndex[i]));
        _parametersByEntity = new(() => new RowGroups(EntityCount, data.Parameters.Length, p => (int)data.Parameters[p].Entity));
        _relationsBySource = new(() => new RowGroups(EntityCount, data.Relations.Length, r => (int)data.Relations[r].EntityA));
        _meshBounds = new(ComputeMeshBounds);
    }

    public IBimData Data { get; }

    /// <summary>The model's geometry, or an empty one when it has none.</summary>
    public BimGeometry Geometry { get; }

    public int EntityCount { get; }
    public int InstanceCount { get; }

    /// <summary>Rows of the Meshes table: the shorter of its two columns.</summary>
    public int MeshCount { get; }

    /// <summary>Rows of the Materials table: the shortest of its six columns.</summary>
    public int MaterialCount { get; }

    /// <summary>Rows of the Transforms table: the shortest of its ten columns.</summary>
    public int TransformCount { get; }

    /// <summary>Instances that cannot be drawn because their mesh index is out of range.</summary>
    public int InstancesWithBadMesh { get; }

    /// <summary>Instances with a valid mesh that cannot be placed because their transform index is out of range.</summary>
    public int InstancesWithBadTransform { get; }

    /// <summary>Drawable instances whose material index is neither -1 nor a row of the Materials table; read as -1.</summary>
    public int InstancesWithBadMaterial { get; }

    /// <summary>Drawable instances whose entity index is missing or out of range; read as -1.</summary>
    public int InstancesWithoutEntity { get; }

    //== Instances

    /// <summary>True when the instance's mesh and transform indices are rows of their tables.</summary>
    public bool IsDrawable(int instance)
        => (_faults[instance] & (Fault.BadMesh | Fault.BadTransform)) == 0;

    public bool IsHidden(int instance)
        => (_faults[instance] & Fault.Hidden) != 0;

    /// <summary>The instance with its indices checked; an invalid index is -1.</summary>
    public BosInstance Instance(int instance)
    {
        var f = _faults[instance];
        var g = Geometry;
        return new BosInstance(
            instance,
            (f & Fault.NoEntity) != 0 ? -1 : g.InstanceEntityIndex[instance],
            (f & Fault.BadMesh) != 0 ? -1 : g.InstanceMeshIndex[instance],
            (f & Fault.BadMaterial) != 0 ? -1 : At(g.InstanceMaterialIndex, instance),
            (f & Fault.BadTransform) != 0 ? -1 : g.InstanceTransformIndex[instance],
            (f & Fault.Hidden) != 0);
    }

    /// <summary>The drawable instances the filter selects, in table order, with what it left
    /// out counted. O(instances).</summary>
    public BosSelection Select(BosSceneFilter? filter = null)
    {
        filter ??= BosSceneFilter.Default;
        var requested = filter.Entities is null ? null : new HashSet<int>(filter.Entities);
        var matched = new HashSet<int>();
        var selected = new List<BosInstance>();
        int badMesh = 0, emptyMesh = 0, badTransform = 0, defaulted = 0;
        for (var i = 0; i < InstanceCount; i++)
        {
            var f = _faults[i];
            if (!filter.IncludeHidden && (f & Fault.Hidden) != 0)
                continue;
            if (requested is not null && ((f & Fault.NoEntity) != 0 || !requested.Contains(Geometry.InstanceEntityIndex[i])))
                continue;
            if ((f & Fault.BadMesh) != 0)
                badMesh++;
            else if (!filter.IncludeEmptyMeshes && (f & Fault.EmptyMesh) != 0)
                emptyMesh++;
            else if ((f & Fault.BadTransform) != 0)
                badTransform++;
            else
            {
                if ((f & Fault.BadMaterial) != 0)
                    defaulted++;
                var instance = Instance(i);
                selected.Add(instance);
                matched.Add(instance.Entity);
            }
        }
        var unmatched = requested?.Count(e => !matched.Contains(e)) ?? 0;
        return new BosSelection(selected, badMesh, emptyMesh, badTransform, defaulted, unmatched);
    }

    /// <summary>The entity's drawable instances, hidden ones and those with empty meshes
    /// included, in table order; empty for an entity out of range.</summary>
    public ReadOnlySpan<int> InstancesOf(int entity)
        => IsEntity(entity) ? _instancesByEntity.Value.Rows(entity) : ReadOnlySpan<int>.Empty;

    /// <summary>The drawable instances whose entity index is missing or out of range, in table order.</summary>
    public ReadOnlySpan<int> UnassignedInstances
        => _instancesByEntity.Value.Rows(EntityCount);

    /// <summary>True when the entity has at least one drawable instance (hidden ones count).</summary>
    public bool HasGeometry(int entity)
        => InstancesOf(entity).Length > 0;

    /// <summary>The entities with geometry, in entity order.</summary>
    public IEnumerable<int> ElementsWithGeometry
        => Enumerable.Range(0, EntityCount).Where(HasGeometry);

    /// <summary>The matrix of a row of the Transforms table: scale, then rotation, then
    /// translation, for System.Numerics row vectors.</summary>
    public Matrix4x4 WorldMatrix(int transform)
        => (Matrix4x4)Geometry.GetTransformMatrix(transform);

    /// <summary>The world-space box of the entity's visible instances: the eight corners of each
    /// mesh's own box, moved by the instance's transform. Null when the entity has no visible
    /// instance with vertices: no place is guessed. O(the entity's instances), after a first
    /// call that bounds every mesh once.</summary>
    public BosBox? Bounds(int entity)
    {
        var meshBounds = _meshBounds.Value;
        BosBox? result = null;
        foreach (var i in InstancesOf(entity))
        {
            if (IsHidden(i) || meshBounds[Geometry.InstanceMeshIndex[i]] is not { } local)
                continue;
            var world = local.Transform(WorldMatrix(Geometry.InstanceTransformIndex[i]));
            result = result?.Union(world) ?? world;
        }
        return result;
    }

    /// <summary>A mesh's box in its own coordinates, in metres; null for a mesh with no vertices.</summary>
    private BosBox?[] ComputeMeshBounds()
    {
        var g = Geometry;
        var boxes = new BosBox?[MeshCount];
        for (var m = 0; m < MeshCount; m++)
        {
            var slice = g.GetMeshSlice(m);
            if (slice.VertexCount <= 0)
                continue;
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            for (var v = slice.BaseVertex; v < slice.BaseVertex + slice.VertexCount; v++)
            {
                minX = Math.Min(minX, g.VertexX[v]); maxX = Math.Max(maxX, g.VertexX[v]);
                minY = Math.Min(minY, g.VertexY[v]); maxY = Math.Max(maxY, g.VertexY[v]);
                minZ = Math.Min(minZ, g.VertexZ[v]); maxZ = Math.Max(maxZ, g.VertexZ[v]);
            }
            boxes[m] = new BosBox(new Vector3(minX, minY, minZ) / BimGeometry.VertexMultiplier,
                new Vector3(maxX, maxY, maxZ) / BimGeometry.VertexMultiplier);
        }
        return boxes;
    }

    //== Entities and strings

    public bool IsEntity(int entity)
        => (uint)entity < (uint)EntityCount;

    /// <summary>The entity's GlobalId (IFC GlobalId, Revit UniqueId), or null when it has none.</summary>
    public string? GlobalId(int entity)
        => IsEntity(entity) ? Label(Data.Entities[entity].GlobalId) : null;

    /// <summary>The entity's name, or null when it has none. Also names a category or a type,
    /// which are entities too.</summary>
    public string? Name(int entity)
        => IsEntity(entity) ? Label(Data.Entities[entity].Name) : null;

    public (string? GlobalId, string? Name) EntityIds(int entity)
        => (GlobalId(entity), Name(entity));

    /// <summary>The document's title, or null when it has none.</summary>
    public string? DocumentTitle(DocumentIndex document)
        => (uint)document < (uint)Data.Documents.Length ? Label(Data.Documents[(int)document].Title) : null;

    /// <summary>The string, or null when the index is negative or out of range. An empty
    /// string is returned as "": for a parameter value, empty is a value.</summary>
    public string? Text(StringIndex index)
        => Data.Get(index);

    /// <summary>The string, or null when it is missing or empty: for identity fields (GlobalId,
    /// names, titles, units), which older files store as "" when unset.</summary>
    public string? Label(StringIndex index)
        => Data.Label(index);

    //== Parameters and relations

    /// <summary>Rows of the Parameters table whose entity is <paramref name="entity"/>, in table order.</summary>
    public ReadOnlySpan<int> ParametersOf(int entity)
        => _parametersByEntity.Value.Rows(entity);

    /// <summary>Rows of the Relations table whose source (EntityA) is <paramref name="entity"/>, in table order.</summary>
    public ReadOnlySpan<int> RelationsFrom(int entity)
        => _relationsBySource.Value.Rows(entity);

    /// <summary>The parameter's descriptor, or null when its index is out of range.</summary>
    public ParameterDescriptor? Descriptor(Parameter p)
        => (uint)p.Descriptor < (uint)Data.Descriptors.Length ? Data.Descriptors[(int)p.Descriptor] : null;

    /// <summary>True when the descriptor is known and the value it points at is present.</summary>
    public bool HasValue(Parameter p) => Descriptor(p)?.Type switch
    {
        ParameterType.Int => true,
        ParameterType.Number => NumberValue(p).HasValue,
        ParameterType.String => StringValue(p) is not null,
        ParameterType.Entity => EntityValue(p).HasValue,
        ParameterType.Point => PointValue(p).HasValue,
        _ => false,
    };

    // Each typed value is null when the descriptor is missing, its type is another, or the
    // index the parameter holds is out of its table.

    public int? IntValue(Parameter p)
        => IsType(p, ParameterType.Int) ? p.Value : null;

    public float? NumberValue(Parameter p)
        => IsType(p, ParameterType.Number) && (uint)p.Value < (uint)Data.Numbers.Length ? Data.Numbers[p.Value] : null;

    /// <summary>The string value; "" when BOS stores an empty string, null when it is missing.</summary>
    public string? StringValue(Parameter p)
        => IsType(p, ParameterType.String) ? Text((StringIndex)p.Value) : null;

    /// <summary>The referenced entity's index.</summary>
    public int? EntityValue(Parameter p)
        => IsType(p, ParameterType.Entity) && IsEntity(p.Value) ? p.Value : null;

    public Point? PointValue(Parameter p)
        => IsType(p, ParameterType.Point) && (uint)p.Value < (uint)Data.Points.Length ? Data.Points[p.Value] : null;

    private bool IsType(Parameter p, ParameterType type)
        => Descriptor(p)?.Type == type;

    //==

    /// <summary>The value at <paramref name="i"/>, or -1 (none) past the end of a short column.</summary>
    private static int At(int[] column, int i)
        => i < column.Length ? column[i] : -1;

    private static int MinLength(params Array[] columns)
        => columns.Min(c => c.Length);
}
