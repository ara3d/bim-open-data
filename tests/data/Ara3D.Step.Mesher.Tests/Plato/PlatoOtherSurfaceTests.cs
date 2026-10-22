using Ara3D.IO.StepParser;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Step.Mesher.Tests.Model;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>Surfaces past the plane: the hand-written unit cylinder (radius 1 mm, height 1 mm) and unit sphere (radius
/// 1 mm, one face with a pole-to-pole seam) each mesh closed and consistently oriented through the compiled library.
/// </summary>
public static class PlatoOtherSurfaceTests
{
    /// <summary>The mesher's default chordal bound (StepMeshingOptions.MaxDeviation, 1 mm): on the unit cylinder,
    /// 24 segments per circle, the 15 degree angle governing. A bound of 0.001 mm (72 segments) took over 90 s in face
    /// meshing after the regeneration of 2026-10-19 (TKT-313 log).</summary>
    const double ChordalMillimetres = 1;

    const int SegmentsPerCircle = 24;

    /// <summary>The unit sphere's bound. At 1 mm on a 1 mm radius the version 1 contract (closed, oriented, within the
    /// chordal bound) admits a mesh of 1.498 mm^3, and since the 15 degree angular test no longer governs face
    /// refinement the mesher returns about that; at 0.05 mm every element lies within 5% of the radius, so the 80%
    /// volume floor below measures the surface rather than the bound.</summary>
    const double SphereChordalMillimetres = 0.05;

    static StepPlatoMesh MeshFixture(string name, double chordal = ChordalMillimetres)
    {
        using var document = new StepDocument(StepText.Fixture(name));
        return new StepModel(document).MeshFile(chordal);
    }

    [Test]
    public static void CylinderMeshesClosedAndOriented()
    {
        var file = MeshFixture("unit-cylinder.stp");
        var mesh = file.Mesh;
        Assert.That(file.Failures, Is.Empty);
        Assert.That((bool)mesh.IsClosed(), Is.True, "IsClosed");
        Assert.That((bool)mesh.IsConsistentlyOriented(), Is.True, "IsConsistentlyOriented");
        Assert.That((int)mesh.ReferencedEulerCharacteristic(), Is.EqualTo(2));
        var prism = SegmentsPerCircle / 2.0 * Math.Sin(2 * Math.PI / SegmentsPerCircle);
        Assert.That((double)mesh.SignedVolume(), Is.InRange(prism, Math.PI), "between the inscribed 24-gon prism and the cylinder, mm^3");
        var shape = file.ToShapeMesh(new BodyIndex(0), new StepUnits(0.001, 1e-6));
        Assert.That(MeshChecks.UnpairedEdges(shape.Mesh), Is.Empty);
        Assert.That(MeshChecks.SignedVolume(shape.Mesh), Is.EqualTo((double)mesh.SignedVolume() * 1e-9).Within(1e-15));
    }

    [Test]
    public static void SphereMeshesClosedAndOriented()
    {
        var file = MeshFixture("unit-sphere.stp", SphereChordalMillimetres);
        var mesh = file.Mesh;
        Assert.That(file.Failures, Is.Empty);
        Assert.That(file.IsMeshed(new BrepFaceIndex(0)), Is.True);
        Assert.That((bool)mesh.IsClosed(), Is.True, "IsClosed");
        Assert.That((bool)mesh.IsConsistentlyOriented(), Is.True, "IsConsistentlyOriented");
        Assert.That((int)mesh.ReferencedEulerCharacteristic(), Is.EqualTo(2));
        Assert.That((double)mesh.SignedVolume(), Is.InRange(0.8 * 4 * Math.PI / 3, 4 * Math.PI / 3), "within 20% below the sphere, mm^3");
        var shape = file.ToShapeMesh(new BodyIndex(0), new StepUnits(0.001, 1e-6));
        Assert.That(MeshChecks.UnpairedEdges(shape.Mesh), Is.Empty);
        Assert.That((shape.FacesMeshed, shape.FacesDropped, shape.FacesPending), Is.EqualTo((1, 0, 0)));
    }
}
