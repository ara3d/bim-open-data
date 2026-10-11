using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Breps and surface models in the corpus models build.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusBrepTests
{
    [Test]
    [Category("Slow")]
    public void DentalClinic_FaceBasedSurfaceModels_ProduceGeometry()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
        Assert.That(diagnostics.EntityStatus.GetValueOrDefault("IFCCONNECTEDFACESET"),
            Is.EqualTo(GeometrySupportStatus.Supported));
    }

    [Test]
    [Category("Slow")]
    public void Duplex_FaceBasedSurfaceModels_ProduceGeometry()
    {
        TestFiles.RequireExists(TestFiles.Duplex);
        using var file = TestFiles.LoadStep(TestFiles.Duplex);
        var (model, _) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Slow")]
    public void SteelPlates_BrepFile_ProducesGeometry()
    {
        TestFiles.RequireExists(TestFiles.SteelPlates);
        using var file = TestFiles.LoadStep(TestFiles.SteelPlates);
        var (model, _) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Instances.Count, Is.GreaterThan(0));
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Slow")]
    public void DigitalHub_BicycleProxyAdvancedBrep_Builds()
    {
        var ifcPath = TestFiles.DigitalHub;
        TestFiles.RequireExists(ifcPath);

        using var file = new IfcFile(ifcPath, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        var brep = ctx.GetEntity(135371);
        var mesh = Brep.BuildAdvancedBrep(ctx, brep);

        TestContext.WriteLine($"Bicycle brep #135371: {mesh.Points.Count} pts, {mesh.FaceIndices.Count} tris");
        Assert.That(mesh.FaceIndices, Has.Count.GreaterThan(100));
        Assert.That(ctx.Diagnostics.EntityStatus.Keys, Does.Contain("IFCEDGELOOP"));
    }
}
