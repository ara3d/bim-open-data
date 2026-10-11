using Ara3D.Geometry;
using Ara3D.Ifc.Mesher.Approach1;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;

namespace Ara3D.IfcMeshingComparison.Tests.PureCSharp;

[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class BrepTests
{
    [Test]
    public void FacetedBrep_DuplicateConsecutiveVertices_Meshes()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.,0.));
            #3=IFCCARTESIANPOINT((0.,2.,0.));
            #4=IFCPOLYLOOP((#1,#1,#2,#3,#1));
            #5=IFCFACEOUTERBOUND(#4,.T.);
            #6=IFCFACE((#5));
            #7=IFCCLOSEDSHELL((#6));
            #8=IFCFACETEDBREP(#7);
            """);

        var ctx = model.Context;
        var mesh = Brep.BuildFacetedBrep(ctx, model.Entity(8));
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
    }

    [Test]
    public void FacetedBrep_TriangleFace_Meshes()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.,0.));
            #3=IFCCARTESIANPOINT((0.,2.,0.));
            #4=IFCPOLYLOOP((#1,#2,#3));
            #5=IFCFACEOUTERBOUND(#4,.T.);
            #6=IFCFACE((#5));
            #7=IFCCLOSEDSHELL((#6));
            #8=IFCFACETEDBREP(#7);
            """);

        var ctx = model.Context;
        var mesh = Brep.BuildFacetedBrep(ctx, model.Entity(8));
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
        Assert.That(mesh.Points, Has.Count.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void ConnectedFaceSet_TriangleFace_Meshes()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.,0.));
            #3=IFCCARTESIANPOINT((0.,2.,0.));
            #4=IFCPOLYLOOP((#1,#2,#3));
            #5=IFCFACEOUTERBOUND(#4,.T.);
            #6=IFCFACE((#5));
            #7=IFCCONNECTEDFACESET((#6));
            """);

        var ctx = model.Context;
        var mesh = Brep.BuildConnectedFaceSet(ctx, model.Entity(7));
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
    }

    [Test]
    public void FaceBasedSurfaceModel_UnwrapsConnectedFaceSet()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.,0.));
            #3=IFCCARTESIANPOINT((0.,2.,0.));
            #4=IFCPOLYLOOP((#1,#2,#3));
            #5=IFCFACEOUTERBOUND(#4,.T.);
            #6=IFCFACE((#5));
            #7=IFCCONNECTEDFACESET((#6));
            #8=IFCFACEBASEDSURFACEMODEL((#7));
            """);

        var ctx = model.Context;
        var mesh = Brep.BuildFaceBasedSurfaceModel(ctx, model.Entity(8));
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
    }

    [Test]
    public void FacetedBrep_CollinearFace_IsReportedAsDroppedAgainstItsProduct()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.,0.));
            #3=IFCCARTESIANPOINT((0.,2.,0.));
            #4=IFCPOLYLOOP((#1,#2,#3));
            #5=IFCFACEOUTERBOUND(#4,.T.);
            #6=IFCFACE((#5));
            #10=IFCCARTESIANPOINT((4.,0.,0.));
            #11=IFCCARTESIANPOINT((5.,0.,0.));
            #12=IFCPOLYLOOP((#1,#10,#11));
            #13=IFCFACEOUTERBOUND(#12,.T.);
            #14=IFCFACE((#13));
            #15=IFCCLOSEDSHELL((#6,#14));
            #16=IFCFACETEDBREP(#15);
            """);

        var ctx = model.Context;
        ctx.CurrentProductId = 99;
        var mesh = Brep.BuildFacetedBrep(ctx, model.Entity(16));

        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
        Assert.That(ctx.Diagnostics.DroppedFaceCounts, Is.EqualTo(new Dictionary<int, int> { [99] = 1 }));
        Assert.That(ctx.Diagnostics.Messages, Has.Some.Matches<string>(m =>
            m.Contains("product #99") && m.Contains("#14") && m.Contains("triangulation failed")));
    }

    [Test]
    public void FacetedBrep_SelfCrossingRing_IsReportedWithItsAreaDeviation()
    {
        // A bow tie: its triangles cover far more area than the ring encloses.
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCCARTESIANPOINT((4.,4.,0.));
            #3=IFCCARTESIANPOINT((4.,0.,0.));
            #4=IFCCARTESIANPOINT((0.,4.,0.));
            #5=IFCPOLYLOOP((#1,#2,#3,#4));
            #6=IFCFACEOUTERBOUND(#5,.T.);
            #7=IFCFACE((#6));
            #8=IFCCLOSEDSHELL((#7));
            #9=IFCFACETEDBREP(#8);
            """);

        var ctx = model.Context;
        var mesh = Brep.BuildFacetedBrep(ctx, model.Entity(9));

        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(0));
        Assert.That(ctx.Diagnostics.TotalDroppedFaces, Is.EqualTo(1));
        Assert.That(ctx.Diagnostics.Messages, Has.Some.Matches<string>(m =>
            m.Contains("outside any product") && m.Contains("#7") && m.Contains("area off by")));
    }

    [Test]
    public void DroppedFaces_AreAggregatedPerProduct()
    {
        var diagnostics = new MeshingDiagnostics();
        for (var face = 100; face < 150; face++)
            diagnostics.RecordDroppedFace(7, face, "triangulation failed");
        diagnostics.RecordDroppedFace(8, 200, "area off by 12.5 %");

        Assert.That(diagnostics.Messages, Has.Count.EqualTo(2));
        Assert.That(diagnostics.DroppedFaceCounts[7], Is.EqualTo(50));
        Assert.That(diagnostics.Messages[0], Does.Contain("product #7").And.Contain("dropped 50 face(s)").And.Contain("..."));
    }

    [Test]
    [Category("Slow")]
    public void DentalClinic_FaceBasedSurfaceModels_ProduceGeometry()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var (model, diagnostics) = ModelAssembler.BuildModel(file);
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
        var (model, _) = ModelAssembler.BuildModel(file);
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Slow")]
    public void SteelPlates_BrepFile_ProducesGeometry()
    {
        TestFiles.RequireExists(TestFiles.SteelPlates);
        using var file = TestFiles.LoadStep(TestFiles.SteelPlates);
        var (model, _) = ModelAssembler.BuildModel(file);
        Assert.That(model.Instances.Count, Is.GreaterThan(0));
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
    }
}
