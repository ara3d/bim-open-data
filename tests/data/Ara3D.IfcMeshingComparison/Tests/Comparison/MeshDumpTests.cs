using Ara3D.Ifc.Mesher;
using Ara3D.Ifc.Mesher.Tests;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

[TestFixture]
public sealed class MeshDumpTests
{
    const string TwoBoxes = """
        #1=IFCRECTANGLEPROFILEDEF(.AREA.,'p',$,1.,1.);
        #2=IFCDIRECTION((0.,0.,1.));
        #3=IFCEXTRUDEDAREASOLID(#1,$,#2,1.);
        #4=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3));
        #5=IFCPRODUCTDEFINITIONSHAPE($,$,(#4));
        #6=IFCCARTESIANPOINT((0.,0.,0.));
        #7=IFCAXIS2PLACEMENT3D(#6,$,$);
        #8=IFCLOCALPLACEMENT($,#7);
        #9=IFCMEMBER('a',$,'A',$,$,#8,#5,$);
        #10=IFCCARTESIANPOINT((3.,0.,0.));
        #11=IFCAXIS2PLACEMENT3D(#10,$,$);
        #12=IFCLOCALPLACEMENT($,#11);
        #13=IFCMEMBER('b',$,'B',$,$,#12,#5,$);
        """;

    [Test]
    public void RoundTrip_KeepsMeshesInstancesEntitiesAndMatrices()
    {
        using var micro = MicroIfc.WriteTemp(TwoBoxes);
        var (model, _) = ModelAssembler.BuildModel(micro.Context.Resolver);
        var path = new FilePath(Path.Combine(Path.GetTempPath(), $"meshdump-{Guid.NewGuid():N}{MeshDump.Extension}"));
        var header = new MeshDumpHeader("Test", "1.0", new MeshDumpSource("two-boxes.ifc", "00", 0), 7, new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc));
        try
        {
            MeshDump.Write(path, header, model);
            var (readHeader, read) = MeshDump.Read(path);

            Assert.That(readHeader, Is.EqualTo(header));
            Assert.That(read.Meshes, Has.Count.EqualTo(model.Meshes.Count));
            Assert.That(read.Instances, Has.Count.EqualTo(2));
            Assert.That(read.Instances.Select(i => i.EntityIndex), Is.EqualTo(new[] { 9, 13 }));
            Assert.That((float)read.Instances[1].Matrix4x4.M41, Is.EqualTo(3f).Within(1e-6f));
            Assert.That(read.Meshes[0].Points.Count, Is.EqualTo(model.Meshes[0].Points.Count));
            Assert.That(read.Meshes[0].FaceIndices.Count, Is.EqualTo(model.Meshes[0].FaceIndices.Count));
            Assert.That(MeshDump.ReadHeader(path).Mesher, Is.EqualTo("Test"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void DumpBackend_WithoutADump_FailsWithNoDump()
    {
        var backend = new DumpBackend("Nowhere", "a mesher with no dumps", [new DirectoryPath(Path.GetTempPath())]);
        var result = backend.Build(new FilePath(Path.Combine(Path.GetTempPath(), "never-meshed.ifc")));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Single(), Is.EqualTo(DumpBackend.NoDump));
    }
}
