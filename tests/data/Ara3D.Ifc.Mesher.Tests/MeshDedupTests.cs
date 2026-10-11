using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>Mesh deduplication in <see cref="ModelAssembler"/>: identical parts share one mesh,
/// parts that differ only past the first sampled vertices do not.</summary>
[TestFixture]
public sealed class MeshDedupTests
{
    [Test]
    public void MeshDedup_SameProfileDifferentDepths_KeepSeparateMeshes()
    {
        using var model = MicroIfc.WriteTemp("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'p',$,1.,1.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,1.);
            #4=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3));
            #5=IFCPRODUCTDEFINITIONSHAPE($,$,(#4));
            #6=IFCCARTESIANPOINT((0.,0.,0.));
            #7=IFCAXIS2PLACEMENT3D(#6,$,$);
            #8=IFCLOCALPLACEMENT($,#7);
            #9=IFCMEMBER('a',$,'A',$,$,#8,#5,$);
            #10=IFCEXTRUDEDAREASOLID(#1,$,#2,2.);
            #11=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#10));
            #12=IFCPRODUCTDEFINITIONSHAPE($,$,(#11));
            #13=IFCCARTESIANPOINT((3.,0.,0.));
            #14=IFCAXIS2PLACEMENT3D(#13,$,$);
            #15=IFCLOCALPLACEMENT($,#14);
            #16=IFCMEMBER('b',$,'B',$,$,#15,#12,$);
            """);

        var (built, _) = ModelAssembler.BuildModel(model.Context.Resolver);

        Assert.That(built.Instances, Has.Count.EqualTo(2));
        Assert.That(built.Meshes, Has.Count.EqualTo(2),
            "same profile with different extrusion depth must not fingerprint-merge");
    }

    [Test]
    public void MeshDedup_IdenticalExtrusions_ShareOneMesh()
    {
        using var model = MicroIfc.WriteTemp("""
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
            """);

        var (built, _) = ModelAssembler.BuildModel(model.Context.Resolver);

        Assert.That(built.Instances, Has.Count.EqualTo(2));
        Assert.That(built.Meshes, Has.Count.EqualTo(1),
            "identical local topology should still dedup");
    }
}
