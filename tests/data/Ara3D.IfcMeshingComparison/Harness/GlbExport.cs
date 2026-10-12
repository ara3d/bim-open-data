using Ara3D.Geometry;
using Ara3D.IO.GltfExporter;
using Ara3D.Models;
using Ara3D.Utils;
using SysMatrix = System.Numerics.Matrix4x4;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>One GLB written for the comparison page: where it is and what it holds.</summary>
public readonly record struct GlbEntry(string Path, int Triangles, int Instances, int Meshes, long Bytes, long ElapsedMs);

/// <summary>Turns a <see cref="Model3D"/> from any mesher into a Y-up GLB of one flat colour, the
/// form the BIM Open Viewer and other glTF tools open.</summary>
public static class GlbExport
{
    /// <summary>Z-up (IFC, the meshers) to Y-up (glTF): (x, y, z) becomes (x, z, -y). Exact zeros and
    /// ones, the same matrix as <c>GltfSceneBuilder.ZUpToYUp</c>. System.Numerics multiplies row
    /// vectors, so it goes on the right of an instance matrix.</summary>
    public static readonly SysMatrix ZUpToYUp = new(
        1, 0, 0, 0,
        0, 0, -1, 0,
        0, 1, 0, 0,
        0, 0, 0, 1);

    /// <summary>The same meshes with every instance transform rotated to Y-up. Meshes stay shared.</summary>
    public static Model3D ToYUp(Model3D model)
        => new(model.Meshes, model.Instances
            .Select(i => i.WithMatrix(new Matrix4x4(i.Matrix4x4.Value * ZUpToYUp)))
            .ToList());

    /// <summary>The same model with every instance given one flat colour.</summary>
    public static Model3D WithColor(Model3D model, Color color)
    {
        var material = Material.Default.WithColor(color);
        return new Model3D(model.Meshes, model.Instances.Select(i => i.WithMaterial(material)).ToList());
    }

    /// <summary>Writes <paramref name="model"/>, already Y-up, to <paramref name="glbPath"/>. The entry's
    /// <c>Path</c> is the file name; <see cref="GlbManifest"/> makes it relative to the glb folder.</summary>
    public static GlbEntry WriteGlb(Model3D model, FilePath glbPath, long elapsedMs)
    {
        glbPath.GetDirectory().Create();
        model.WriteGlb(glbPath);
        var triangles = model.Instances.Sum(i => i.MeshIndex < 0 ? 0 : model.Meshes[i.MeshIndex].FaceIndices.Count);
        var visible = model.Instances.Count(i => i.MeshIndex >= 0 && i.IsVisible);
        return new GlbEntry(glbPath.GetFileName(), triangles, visible, model.Meshes.Count, new FileInfo(glbPath).Length, elapsedMs);
    }
}
