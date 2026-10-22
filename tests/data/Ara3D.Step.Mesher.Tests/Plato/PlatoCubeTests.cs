using Ara3D.IO.StepParser;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Step.Mesher.Tests.Model;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>TKT-313's first chunk: the unit cube meshed by the Plato library compiled to C#, checked twice, by the
/// library's own functions and laws on its ModelMesh (millimetres) and by C# on the contract's mesh (metres).</summary>
public static class PlatoCubeTests
{
    const double ChordalMillimetres = 0.001;

    static StepPlatoMesh MeshFixture(string name)
    {
        using var document = new StepDocument(StepText.Fixture(name));
        return new StepModel(document).MeshFile(ChordalMillimetres);
    }

    [Test]
    public static void LibraryMeshesTheCubeWithoutFailures()
    {
        var file = MeshFixture("unit-cube.stp");
        Assert.That(file.Failures, Is.Empty);
        Assert.That(file.Outcomes.SelectMany(o => o.Diagnostics).Where(d => (bool)d.Severity.IsError()).Select(d => (string)d.Code), Is.Empty);
        Assert.That(((int)file.Mesh.Positions.Count, (int)file.Mesh.Triangles.Count, (int)file.Mesh.Quads.Count), Is.EqualTo((8, 0, 6)), "one quad per face: the host prefers quads (TKT-405)");
    }

    [Test]
    public static void LibraryMeshFunctionsHoldOnTheCube()
    {
        var mesh = MeshFixture("unit-cube.stp").Mesh;
        Assert.That((bool)mesh.IsManifold(), Is.True, "IsManifold");
        Assert.That((bool)mesh.IsClosed(), Is.True, "IsClosed");
        Assert.That((bool)mesh.IsConsistentlyOriented(), Is.True, "IsConsistentlyOriented");
        Assert.That((int)mesh.ReferencedEulerCharacteristic(), Is.EqualTo(2), "ReferencedEulerCharacteristic");
        Assert.That((double)mesh.SignedVolume(), Is.EqualTo(1.0).Within(1e-9), "SignedVolume, mm^3");
        Assert.That((double)mesh.Area(), Is.EqualTo(6.0).Within(1e-9), "Area, mm^2");
        Assert.That((int)mesh.ComponentsOf().ComponentCount, Is.EqualTo(1), "components");
    }

    [Test]
    public static void LibraryMeshLawsHoldOnTheCube()
    {
        var mesh = MeshFixture("unit-cube.stp").Mesh;
        var table = mesh.EdgeTableOf();
        var components = Enumerable.Range(0, mesh.ComponentsOf().ComponentCount).Select(c => new MeshComponentIndex(c)).ToArray();
        var edges = Enumerable.Range(0, table.CountEdges()).Select(k => new UndirectedEdgeIndex(k)).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(edges, Has.Length.EqualTo(12), "the cube's 12 edges: 6 quads on 8 vertices");
            Assert.That((bool)edges.All(k => table.Law_ClosedMeansEveryEdgeHasTwoSides(k)), Is.True, "Law_ClosedMeansEveryEdgeHasTwoSides");
            Assert.That((bool)edges.All(k => table.Law_ManifoldMeansNoEdgeHasMoreThanTwoSides(k)), Is.True, "Law_ManifoldMeansNoEdgeHasMoreThanTwoSides");
            Assert.That((bool)table.Law_OrientedExactlyWhenNoEdgeIsMisoriented(), Is.True, "Law_OrientedExactlyWhenNoEdgeIsMisoriented");
            Assert.That((bool)components.All(c => mesh.Law_ClosedOrientedSurfaceHasGenus(c)), Is.True, "Law_ClosedOrientedSurfaceHasGenus");
            Assert.That((bool)components.All(c => mesh.Law_AngleDefectSumIsTwoPiTimesEuler(c)), Is.True, "Law_AngleDefectSumIsTwoPiTimesEuler");
            Assert.That((bool)components.All(c => mesh.Law_VectorAreaVanishes(c)), Is.True, "Law_VectorAreaVanishes");
            Assert.That((bool)mesh.Law_ComponentEulerCharacteristicsAddUp(), Is.True, "Law_ComponentEulerCharacteristicsAddUp");
            Assert.That((bool)mesh.Law_ReversalNegatesSignedVolume(), Is.True, "Law_ReversalNegatesSignedVolume");
            Assert.That((bool)mesh.Law_TriangleMeshKeepsArea(), Is.True, "Law_TriangleMeshKeepsArea");
            Assert.That((bool)mesh.Law_TriangleMeshKeepsPositions(), Is.True, "Law_TriangleMeshKeepsPositions");
        });
    }

    [Test]
    public static void ContractMeshIsTheClosedOutwardCubeInMetres()
    {
        var file = MeshFixture("unit-cube.stp");
        var shape = file.ToShapeMesh(new BodyIndex(0), new StepUnits(0.001, 1e-6));
        var mesh = shape.Mesh;
        Assert.That((mesh.Points.Count, mesh.FaceIndices.Count), Is.EqualTo((8, 12)));
        Assert.That((shape.FacesMeshed, shape.FacesDropped), Is.EqualTo((6, 0)));
        Assert.That(MeshChecks.UnpairedEdges(mesh), Is.Empty, "every directed edge has its reverse, once");
        Assert.That(MeshChecks.EulerCharacteristic(mesh), Is.EqualTo(2));
        Assert.That(MeshChecks.SignedVolume(mesh), Is.EqualTo(1e-9).Within(1e-15), "outward: positive volume, m^3");
        Assert.That(mesh.Points.Select(p => ((float)p.X, (float)p.Y, (float)p.Z)).Distinct().Count(), Is.EqualTo(8));
        Assert.That(Enumerable.All(mesh.Points, p => IsCorner(p.X) && IsCorner(p.Y) && IsCorner(p.Z)), Is.True, "corners at 0 or 0.001 m");
    }

    static bool IsCorner(float c)
        => Math.Abs(c) < 1e-9f || Math.Abs(c - 0.001f) < 1e-9f;
}
