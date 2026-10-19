using Ara3D.Step.Mesher.Tests.Model;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>The entry point meshing through the Plato library: the unit cube as StepMesher.Mesh returns it (the C# half
/// of TKT-314 criterion 3), and the cylinder's faces recorded as supported.</summary>
public static class PlatoStepMesherTests
{
    [Test]
    public static void UnitCubeComesBackAsTheClosedOutwardCube()
    {
        var result = StepMesher.Mesh(StepText.Fixture("unit-cube.stp"));
        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That((result.MeshCount, result.InstanceCount, result.TriangleCount, result.GeometryPending), Is.EqualTo((1, 1, 12, 0)));
        var mesh = result.Model!.Meshes[0];
        Assert.That((mesh.Points.Count, mesh.FaceIndices.Count), Is.EqualTo((8, 12)));
        Assert.That(MeshChecks.UnpairedEdges(mesh), Is.Empty, "closed and consistently oriented");
        Assert.That(MeshChecks.EulerCharacteristic(mesh), Is.EqualTo(2));
        Assert.That(MeshChecks.SignedVolume(mesh), Is.EqualTo(1e-9).Within(1e-15), "outward, m^3");
        Assert.That((float)result.Bounds.Max.X, Is.EqualTo(0.001f).Within(1e-9f));
        Assert.That(result.Diagnostics.EntityCounts["PLANE"], Is.EqualTo(6));
        Assert.That(result.Diagnostics.DroppedFaces, Is.Empty);
    }

    [Test]
    public static void CylinderFacesAreRecordedSupported()
    {
        var result = StepMesher.Mesh(StepText.Fixture("unit-cylinder.stp"));
        Assert.That((result.MeshCount, result.GeometryPending), Is.EqualTo((1, 0)));
        Assert.That(result.Diagnostics.EntityStatus["CYLINDRICAL_SURFACE"], Is.EqualTo(StepSupport.Supported));
        Assert.That(MeshChecks.UnpairedEdges(result.Model!.Meshes[0]), Is.Empty);
    }
}
