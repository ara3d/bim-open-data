using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using SharpGLTF.Schema2;
using BimMaterial = Ara3D.Models.Material;
using GltfMaterial = SharpGLTF.Schema2.Material;

namespace Ara3D.BimOpenSchema.IO.Gltf;

/// <summary>Turns one BOS model into one glTF model. Every BOS mesh in use is written once:
/// its positions into a shared vertex buffer view and its indices into a shared index buffer
/// view, each behind its own pair of accessors. glTF binds a material to a mesh primitive, not
/// to a node, so each pair of BOS mesh and BOS material becomes one glTF mesh over the same
/// accessors. Each instance becomes one root node whose matrix is the BOS transform followed
/// by the z-up to y-up rotation.</summary>
internal sealed class GltfSceneBuilder(IBimData data, GlbExportOptions options)
{
    public const string Generator = "Ara3D.BimOpenSchema.IO.Gltf";

    /// <summary>BOS is z-up and glTF is y-up: (x, y, z) becomes (x, z, -y). Written with exact
    /// zeros and ones; CreateRotationX(-π/2) leaves 4e-8 where zeros belong. System.Numerics
    /// multiplies row vectors, so this goes on the right of an instance matrix.</summary>
    public static readonly Matrix4x4 ZUpToYUp = new(
        1, 0, 0, 0,
        0, 0, -1, 0,
        0, 1, 0, 0,
        0, 0, 0, 1);

    private readonly BimGeometry _geometry = data.Geometry ?? new BimGeometry();
    private readonly ModelRoot _model = ModelRoot.CreateModel();
    private readonly Dictionary<int, (Accessor Positions, Accessor Indices)> _accessors = new();
    private readonly Dictionary<int, GltfMaterial> _materials = new();
    private readonly Dictionary<(int Mesh, int Material), Mesh> _meshes = new();

    public (ModelRoot Model, GlbExportSummary Summary) Build()
    {
        _model.Asset.Generator = Generator;
        var scene = _model.UseScene(0);
        _model.DefaultScene = scene;

        var selection = SelectInstances();
        var instances = selection.Instances;
        WriteMeshBuffers(instances);

        long triangles = 0;
        foreach (var i in instances)
        {
            var meshIndex = _geometry.InstanceMeshIndex[i];
            var materialIndex = _geometry.InstanceMaterialIndex[i];
            var entityIndex = _geometry.InstanceEntityIndex[i];

            var node = scene.CreateNode(EntityString(entityIndex, e => e.Name));
            node.Mesh = UseMesh(meshIndex, materialIndex);
            node.LocalMatrix = (Matrix4x4)_geometry.GetTransformMatrix(_geometry.InstanceTransformIndex[i]) * ZUpToYUp;
            node.Extras = EntityExtras(entityIndex);
            triangles += _accessors[meshIndex].Indices.Count / 3;
        }

        return (_model, new GlbExportSummary(instances.Count, _meshes.Count, _materials.Count, triangles,
            selection.SkippedEmpty, selection.UnmatchedEntityIndices));
    }

    /// <summary>The instances to write, in table order, with what was left out on the way.</summary>
    private sealed record Selection(List<int> Instances, int SkippedEmpty, int UnmatchedEntityIndices);

    /// <summary>Picks the instances to write. A selected instance whose mesh has no triangles is
    /// left out and counted; a requested entity that ends up with no node is counted as unmatched.</summary>
    private Selection SelectInstances()
    {
        var entities = options.EntityIndices is null ? null : new HashSet<int>(options.EntityIndices);
        var meshCount = _geometry.GetNumMeshes();
        var instances = new List<int>();
        var matched = new HashSet<int>();
        var skipped = 0;
        for (var i = 0; i < _geometry.GetNumInstances(); i++)
        {
            if (!options.IncludeHidden && (_geometry.InstanceFlags[i] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0)
                continue;
            var entity = _geometry.InstanceEntityIndex[i];
            if (entities is not null && !entities.Contains(entity))
                continue;
            var mesh = _geometry.InstanceMeshIndex[i];
            if (mesh < 0 || mesh >= meshCount || IsEmpty(_geometry.GetMeshSlice(mesh)))
            {
                skipped++;
                continue;
            }
            instances.Add(i);
            matched.Add(entity);
        }
        var unmatched = entities?.Count(e => !matched.Contains(e)) ?? 0;
        return new Selection(instances, skipped, unmatched);
    }

    private static bool IsEmpty(Ara3D.Models.MeshSliceStruct slice)
        => slice.IndexCount == 0 || slice.VertexCount == 0;

    /// <summary>Writes each mesh the instances use, once, in metres: positions as float VEC3 in
    /// one strided buffer view and indices as uint32 in another. BOS indices are already local
    /// to their mesh, as glTF accessors expect.</summary>
    private void WriteMeshBuffers(List<int> instances)
    {
        var meshes = instances.Select(i => _geometry.InstanceMeshIndex[i]).Distinct().Order().ToList();
        var slices = meshes.Select(_geometry.GetMeshSlice).ToList();
        if (meshes.Count == 0)
            return;

        var positions = new float[slices.Sum(s => s.VertexCount) * 3];
        var indices = new uint[slices.Sum(s => (long)s.IndexCount)];
        var p = 0;
        var x = 0;
        foreach (var s in slices)
        {
            for (var v = s.BaseVertex; v < s.BaseVertex + s.VertexCount; v++)
            {
                positions[p++] = _geometry.VertexX[v] / BimGeometry.VertexMultiplier;
                positions[p++] = _geometry.VertexY[v] / BimGeometry.VertexMultiplier;
                positions[p++] = _geometry.VertexZ[v] / BimGeometry.VertexMultiplier;
            }
            for (var k = s.FirstIndex; k < s.FirstIndex + s.IndexCount; k++)
                indices[x++] = (uint)_geometry.IndexBuffer[k];
        }

        const int positionStride = 3 * sizeof(float);
        var positionView = _model.UseBufferView(MemoryMarshal.AsBytes(positions.AsSpan()).ToArray(),
            byteStride: positionStride, target: BufferMode.ARRAY_BUFFER);
        var indexView = _model.UseBufferView(MemoryMarshal.AsBytes(indices.AsSpan()).ToArray(),
            target: BufferMode.ELEMENT_ARRAY_BUFFER);

        var vertexOffset = 0;
        var indexOffset = 0;
        for (var m = 0; m < meshes.Count; m++)
        {
            var s = slices[m];
            var positionAccessor = _model.CreateAccessor();
            positionAccessor.SetVertexData(positionView, vertexOffset * positionStride, s.VertexCount);
            var indexAccessor = _model.CreateAccessor();
            indexAccessor.SetIndexData(indexView, indexOffset * sizeof(uint), (int)s.IndexCount, IndexEncodingType.UNSIGNED_INT);
            _accessors.Add(meshes[m], (positionAccessor, indexAccessor));
            vertexOffset += s.VertexCount;
            indexOffset += (int)s.IndexCount;
        }
    }

    private Mesh UseMesh(int meshIndex, int materialIndex)
    {
        if (_meshes.TryGetValue((meshIndex, materialIndex), out var mesh))
            return mesh;
        mesh = _model.CreateMesh();
        var primitive = mesh.CreatePrimitive();
        var (positions, indices) = _accessors[meshIndex];
        primitive.SetVertexAccessor("POSITION", positions);
        primitive.SetIndexAccessor(indices);
        primitive.Material = UseMaterial(materialIndex);
        _meshes.Add((meshIndex, materialIndex), mesh);
        return mesh;
    }

    /// <summary>One glTF material per BOS material; index -1 (no material) gets the SDK's
    /// default grey. Double-sided, because BIM meshes do not keep a consistent winding.</summary>
    private GltfMaterial UseMaterial(int materialIndex)
    {
        if (_materials.TryGetValue(materialIndex, out var material))
            return material;
        var source = _geometry.GetMaterial(materialIndex, BimMaterial.Default);
        material = _model.CreateMaterial();
        material.InitializePBRMetallicRoughness();
        material.DoubleSided = true;
        material.Alpha = source.Color.A < 1f ? AlphaMode.BLEND : AlphaMode.OPAQUE;
        var channels = material.Channels.ToList();
        var baseColor = channels.Single(c => c.Key == "BaseColor");
        baseColor.Color = new Vector4(source.Color.R, source.Color.G, source.Color.B, source.Color.A);
        var metallicRoughness = channels.Single(c => c.Key == "MetallicRoughness");
        metallicRoughness.SetFactor("MetallicFactor", source.Metallic);
        metallicRoughness.SetFactor("RoughnessFactor", source.Roughness);
        _materials.Add(materialIndex, material);
        return material;
    }

    /// <summary>The ids a viewer's pick needs to find the element: the entity's row in the
    /// Entities table and its GlobalId. A missing id is left out, never filled in.</summary>
    private JsonObject? EntityExtras(int entityIndex)
    {
        if (!IsEntity(entityIndex))
            return null;
        var extras = new JsonObject { [BosGlb.EntityIndexKey] = entityIndex };
        var globalId = EntityString(entityIndex, e => e.GlobalId);
        if (globalId is not null)
            extras[BosGlb.GlobalIdKey] = globalId;
        return extras;
    }

    private string? EntityString(int entityIndex, Func<Entity, StringIndex> field)
    {
        if (!IsEntity(entityIndex))
            return null;
        var s = (int)field(data.Entities[entityIndex]);
        return s >= 0 && s < data.Strings.Length && !string.IsNullOrEmpty(data.Strings[s]) ? data.Strings[s] : null;
    }

    private bool IsEntity(int entityIndex)
        => entityIndex >= 0 && entityIndex < data.Entities.Length;
}
