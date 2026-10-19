using Ara3D.IO.StepParser;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Step.Mesher.Tests.Model;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>Surfaces past the plane in the committed generation of the library: the hand-written unit cylinder
/// (radius 1 mm, height 1 mm) meshes closed; the unit sphere's face reaches a function the library declares without
/// a body, which the bridge turns into a pending failure and an Error at the face instead of an exception.</summary>
public static class PlatoOtherSurfaceTests
{
    /// <summary>The mesher's default chordal bound (StepMeshingOptions.MaxDeviation, 1 mm): on the unit cylinder,
    /// 24 segments per circle, the 15 degree angle governing. A bound of 0.001 mm (72 segments) took over 90 s in face
    /// meshing after the regeneration of 2026-10-19 (TKT-313 log).</summary>
    const double ChordalMillimetres = 1;

    const int SegmentsPerCircle = 24;

    static StepPlatoMesh MeshFixture(string name)
    {
        using var document = new StepDocument(StepText.Fixture(name));
        return new StepModel(document).MeshFile(ChordalMillimetres);
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
    public static void SphereFaceIsAPendingFailureNotAnException()
    {
        var file = MeshFixture("unit-sphere.stp");
        Assert.That(file.Failures, Has.Count.EqualTo(1));
        Assert.That((file.Failures[0].Kind, file.Failures[0].Index, file.Failures[0].IsPending), Is.EqualTo(("face", 0, true)));
        Assert.That(file.IsMeshed(new BrepFaceIndex(0)), Is.False);
        Assert.That(file.Outcomes[0].Diagnostics.Select(d => (string)d.Code), Does.Contain("HOST-LIBRARY-THREW"));
        Assert.That((int)file.Mesh.Triangles.Count, Is.Zero);
        var shape = file.ToShapeMesh(new BodyIndex(0), new StepUnits(0.001, 1e-6));
        Assert.That((shape.Mesh.FaceIndices.Count, shape.FacesMeshed, shape.FacesDropped), Is.EqualTo((0, 0, 1)));
    }
}
