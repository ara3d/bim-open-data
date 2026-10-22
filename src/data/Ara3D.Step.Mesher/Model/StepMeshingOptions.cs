using Ara3D.Geometry;
using Ara3D.Models;

namespace Ara3D.Step.Mesher;

/// <summary>How <see cref="StepMesher"/> meshes. <see cref="MaxDeviation"/> is the chordal bound in metres passed to
/// <see cref="StepShapeMesher.MeshItem"/>; <see cref="Unstyled"/> is the material of a shape no styled_item colours,
/// <see cref="Grey"/> when null.</summary>
public sealed record StepMeshingOptions(float MaxDeviation = MeshKernel.Tolerance.ChordDeviation, Material? Unstyled = null)
{
    public static readonly Material Grey = Material.Default.WithColor(new Color(0.7f, 0.7f, 0.7f, 1f));

    public Material UnstyledMaterial => Unstyled ?? Grey;

    /// <summary><see cref="MaxDeviation"/> in the decoded model's millimetres (the Plato library's DecodeRaw scales
    /// every file to millimetres): the chordal bound StepPlatoMesher.MeshFile takes.</summary>
    public double ChordalMillimetres => MaxDeviation * 1000.0;
}
