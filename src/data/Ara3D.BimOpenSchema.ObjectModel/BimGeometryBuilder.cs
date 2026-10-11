using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D.Geometry;
using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.BimOpenSchema;

public record Instance(
    int EntityIndex, 
    int MaterialIndex,
    int MeshIndex,
    int TransformIndex,
    byte Flags);

/// <summary>
/// This class is provided to make it easier to build a BIM geometry object incrementally. 
/// </summary>
public class BimGeometryBuilder
{
    public List<Instance> Instances { get; private set; } = new();
    public List<TriangleMesh3D> Meshes { get; private set; } = new();
    public IndexedSet<Material> Materials { get; private set; } = new();
    public IndexedSet<Matrix4x4> Matrices { get; private set; } = new();

    public void AddMeshes(IEnumerable<TriangleMesh3D> meshes)
        => Meshes.AddRange(meshes);

    public int AddMesh(TriangleMesh3D mesh)
    {
        Meshes.Add(mesh);
        return Meshes.Count - 1;
    }

    public int AddMaterial(Material material)
        => Materials.Add(material);

    public int AddInstance(int entityIndex, int materialIndex, int meshIndex, int transformIndex, byte flags)
    {
        var es = new Instance(entityIndex, materialIndex, meshIndex, transformIndex, flags);
        Instances.Add(es);
        return Instances.Count - 1;
    }

    public int AddTransform(Matrix4x4 matrix)
        => Matrices.Add(matrix);

    /// <summary>
    /// Number of meshes the last <see cref="BuildModel"/> call left empty because a vertex was NaN or infinite.
    /// The mesh keeps its slot (so every instance's mesh index stays valid and the instance tables are unchanged)
    /// but has no vertices and no indices; readers already flag an empty mesh as bad. Convert the count into a
    /// BOS diagnostic (BimDataBuilder.AddDiagnostic) after BuildModel.
    /// </summary>
    public int NonFiniteMeshCount { get; private set; }

    /// <summary>
    /// Number of coordinates the last <see cref="BuildModel"/> call saturated at int.MinValue or int.MaxValue
    /// because they lay beyond about +/-214.7 km. Convert the count into a BOS diagnostic after BuildModel.
    /// </summary>
    public int ClampedCoordinateCount { get; private set; }

    /// <summary>
    /// Converts a coordinate in metres to BOS vertex units (0.1 mm), rounding to the nearest unit with halves
    /// away from zero. Truncating instead would pull every vertex toward the origin. The product is taken in
    /// double because a float cannot count single units above 16,777,216 (1.68 km). A coordinate beyond the int
    /// range (about 214.7 km) saturates at int.MaxValue or int.MinValue rather than wrapping; BuildModel counts
    /// those. A NaN has no unit count and throws; BuildModel screens meshes for non-finite vertices first.
    /// </summary>
    public static int ToVertexUnits(float metres)
        => ToVertexUnits(metres, out _);

    static int ToVertexUnits(float metres, out bool clamped)
    {
        if (!float.IsFinite(metres))
            throw new ArgumentOutOfRangeException(nameof(metres), metres, "A vertex coordinate must be finite.");
        var units = Math.Round((double)metres * BimGeometry.VertexMultiplier, MidpointRounding.AwayFromZero);
        clamped = units < int.MinValue || units > int.MaxValue;
        return (int)Math.Clamp(units, int.MinValue, int.MaxValue);
    }

    static bool IsFinite(TriangleMesh3D mesh)
    {
        foreach (var p in mesh.Points)
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z))
                return false;
        return true;
    }

    public BimGeometry BuildModel()
    {
        NonFiniteMeshCount = 0;
        ClampedCoordinateCount = 0;
        int Convert(float metres)
        {
            var units = ToVertexUnits(metres, out var clamped);
            if (clamped) ClampedCoordinateCount++;
            return units;
        }

        var r = new BimGeometry
        {
            InstanceEntityIndex = new int[Instances.Count],
            InstanceMaterialIndex = new int[Instances.Count],
            InstanceMeshIndex = new int[Instances.Count],
            InstanceTransformIndex = new int[Instances.Count],
            InstanceFlags = new byte[Instances.Count]
        };

        for (var i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            r.InstanceEntityIndex[i] = inst.EntityIndex;
            r.InstanceMaterialIndex[i] = inst.MaterialIndex;
            r.InstanceMeshIndex[i] = inst.MeshIndex;
            r.InstanceTransformIndex[i] = inst.TransformIndex;
            r.InstanceFlags[i] = inst.Flags;
        }

        var verticesX = new List<int>();
        var verticesY = new List<int>();
        var verticesZ = new List<int>();
        var indices = new List<int>();

        r.MeshVertexOffset = new int[Meshes.Count];
        r.MeshIndexOffset = new int[Meshes.Count];

        for (var i=0; i < Meshes.Count; i++)
        {
            var m = Meshes[i];
            r.MeshVertexOffset[i] = verticesX.Count;
            r.MeshIndexOffset[i] = indices.Count;
            if (!IsFinite(m))
            {
                NonFiniteMeshCount++;
                continue;
            }

            foreach (var vert in m.Points)
            {
                verticesX.Add(Convert(vert.X));
                verticesY.Add(Convert(vert.Y));
                verticesZ.Add(Convert(vert.Z));
            }

            foreach (var face in m.FaceIndices)
            {
                indices.Add(face.A);
                indices.Add(face.B);
                indices.Add(face.C);
            }
        }
        
        r.VertexX = verticesX.ToArray();
        r.VertexY = verticesY.ToArray();
        r.VertexZ = verticesZ.ToArray();
        r.IndexBuffer = indices.ToArray();

        var materials = Materials.OrderedMembers().ToList();

        r.MaterialRed = new byte[materials.Count];
        r.MaterialGreen = new byte[materials.Count];
        r.MaterialBlue = new byte[materials.Count];
        r.MaterialAlpha = new byte[materials.Count];
        r.MaterialRoughness = new byte[materials.Count];
        r.MaterialMetallic = new byte[materials.Count];

        for (var i=0; i < materials.Count; i++)
        {
            var m = materials[i];
            r.MaterialRed[i] = m.Color.R.Value.ToByteFromNormalized();
            r.MaterialGreen[i] = m.Color.G.Value.ToByteFromNormalized();
            r.MaterialBlue[i] = m.Color.B.Value.ToByteFromNormalized();
            r.MaterialAlpha[i] = m.Color.A.Value.ToByteFromNormalized();
            r.MaterialRoughness[i] = m.Roughness.ToByteFromNormalized();
            r.MaterialMetallic[i] = m.Metallic.ToByteFromNormalized();
        }

        var transforms = Matrices.OrderedMembers().ToList();
        var n = transforms.Count;
        r.TransformQW = new float[n];
        r.TransformQX = new float[n];
        r.TransformQY = new float[n];
        r.TransformQZ = new float[n];
        r.TransformSX = new float[n];
        r.TransformSY = new float[n];
        r.TransformSZ = new float[n];
        r.TransformTX = new float[n];
        r.TransformTY = new float[n];
        r.TransformTZ = new float[n];
        
        for (var i=0; i < n; i++)
        {
            var mat = transforms[i];
            if (!System.Numerics.Matrix4x4.Decompose(mat, out var scale, out var rot, out var tr))
            {
                scale = Vector3.One;
                rot = Quaternion.Identity;
                tr = Vector3.Zero;
            }

            r.TransformQW[i] = rot.W;
            r.TransformQX[i] = rot.X;
            r.TransformQY[i] = rot.Y;
            r.TransformQZ[i] = rot.Z;
            r.TransformSX[i] = scale.X;
            r.TransformSY[i] = scale.Y;
            r.TransformSZ[i] = scale.Z;
            r.TransformTX[i] = tr.X;
            r.TransformTY[i] = tr.Y;
            r.TransformTZ[i] = tr.Z;
        }
        return r;
    }
}