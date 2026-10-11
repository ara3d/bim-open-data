using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class ModelBuildTests
{
    [Test]
    public void BuildModel_InstanceEntityIndexMatchesProductExpressId()
    {
        using var model = MicroIfc.WriteTemp("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'Box',$,2.,2.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,4.);
            #4=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3));
            #5=IFCPRODUCTDEFINITIONSHAPE($,$,(#4));
            #6=IFCCARTESIANPOINT((0.,0.,0.));
            #7=IFCAXIS2PLACEMENT3D(#6,$,$);
            #8=IFCLOCALPLACEMENT($,#7);
            #9=IFCWALL('g',$,'Wall',$,$,#8,#5,$);
            """);

        var (built, _) = ModelAssembler.BuildModel(model.Context.Resolver);
        Assert.That(built.Instances, Has.Count.EqualTo(1));
        Assert.That(built.Instances[0].EntityIndex, Is.EqualTo(9));
    }

    [Test]
    public void ProductShape_ExtrudedWall_MeshesViaDispatcher()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'Box',$,2.,2.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,4.);
            #4=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3));
            #5=IFCPRODUCTDEFINITIONSHAPE($,$,(#4));
            #6=IFCCARTESIANPOINT((100.,0.,0.));
            #7=IFCAXIS2PLACEMENT3D(#6,$,$);
            #8=IFCLOCALPLACEMENT($,#7);
            #9=IFCWALL('g',$,'Wall',$,$,#8,#5,$);
            """);

        var ctx = model.Context;
        var mesh = ModelAssembler.BuildEntityMesh(ctx, model.Entity(9));
        Assert.That(mesh, Is.Not.Null);
        var bounds = MeshHelpers.GetBounds(mesh!.Value);
        Assert.That((float)bounds.Min.X.Value, Is.EqualTo(99f).Within(1e-3f));
    }

    [Test]
    public void MappedItem_MultiSolidRepresentation_EmitsMultipleInstances()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'A',$,1.,1.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,1.);
            #4=IFCRECTANGLEPROFILEDEF(.AREA.,'B',$,1.,1.);
            #5=IFCCARTESIANPOINT((2.,0.,0.));
            #6=IFCAXIS2PLACEMENT3D(#5,$,$);
            #7=IFCEXTRUDEDAREASOLID(#4,#6,#2,1.);
            #8=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3,#7));
            #9=IFCCARTESIANPOINT((0.,0.,0.));
            #10=IFCAXIS2PLACEMENT3D(#9,$,$);
            #11=IFCREPRESENTATIONMAP(#10,#8);
            #12=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#9,1.,$);
            #13=IFCMAPPEDITEM(#11,#12);
            #14=IFCSHAPEREPRESENTATION($,'Body','MappedRepresentation',(#13));
            #15=IFCPRODUCTDEFINITIONSHAPE($,$,(#14));
            """);

        var parts = new List<CollectedPart>();
        GeometryPartCollector.CollectParts(model.Context, model.Entity(15), Matrix4x4.Identity, 99, parts);
        Assert.That(parts, Has.Count.EqualTo(2));
        Assert.That(parts.Select(p => p.EntityIndex).Distinct().Single(), Is.EqualTo(99));
    }

    [Test]
    public void InchConversionUnit_ResolvesLengthScaleToMeters()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
            #2=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(25.4),#1);
            #3=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
            #4=IFCCONVERSIONBASEDUNIT(#3,.LENGTHUNIT.,'INCH',#2);
            #5=IFCUNITASSIGNMENT((#4));
            #6=IFCPROJECT('p',$,'P',$,$,$,$,(#5));
            """);

        Assert.That(model.Context.LengthScale, Is.EqualTo(0.0254).Within(1e-9));
    }

    [Test]
    public void MappedFacetedBrep_InchUnits_EmitsInstance()
    {
        using var model = MicroIfc.WriteTemp("""
            #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
            #2=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(25.4),#1);
            #3=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
            #4=IFCCONVERSIONBASEDUNIT(#3,.LENGTHUNIT.,'INCH',#2);
            #5=IFCUNITASSIGNMENT((#4));
            #6=IFCCARTESIANPOINT((0.,0.,0.));
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCDIRECTION((1.,0.,0.));
            #9=IFCAXIS2PLACEMENT3D(#6,#7,#8);
            #10=IFCCARTESIANPOINT((0.,0.,0.));
            #11=IFCCARTESIANPOINT((1.,0.,0.));
            #12=IFCCARTESIANPOINT((1.,1.,0.));
            #13=IFCCARTESIANPOINT((0.,1.,0.));
            #14=IFCCARTESIANPOINT((0.,0.,1.));
            #15=IFCCARTESIANPOINT((1.,0.,1.));
            #16=IFCCARTESIANPOINT((1.,1.,1.));
            #17=IFCCARTESIANPOINT((0.,1.,1.));
            #18=IFCPOLYLOOP((#10,#11,#12,#13));
            #19=IFCFACEOUTERBOUND(#18,.T.);
            #20=IFCFACE((#19));
            #21=IFCPOLYLOOP((#15,#16,#17,#14));
            #22=IFCFACEOUTERBOUND(#21,.T.);
            #23=IFCFACE((#22));
            #24=IFCPOLYLOOP((#14,#17,#12,#13));
            #25=IFCFACEOUTERBOUND(#24,.T.);
            #26=IFCFACE((#25));
            #27=IFCPOLYLOOP((#16,#15,#11,#12));
            #28=IFCFACEOUTERBOUND(#27,.T.);
            #29=IFCFACE((#28));
            #30=IFCPOLYLOOP((#15,#14,#10,#11));
            #31=IFCFACEOUTERBOUND(#30,.T.);
            #32=IFCFACE((#31));
            #33=IFCPOLYLOOP((#17,#16,#12,#13));
            #34=IFCFACEOUTERBOUND(#33,.T.);
            #35=IFCFACE((#34));
            #36=IFCCLOSEDSHELL((#20,#23,#26,#29,#32,#35));
            #37=IFCFACETEDBREP(#36);
            #38=IFCSHAPEREPRESENTATION($,'Body','Brep',(#37));
            #39=IFCREPRESENTATIONMAP(#9,#38);
            #40=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#6,1.,$);
            #41=IFCMAPPEDITEM(#39,#40);
            #42=IFCSHAPEREPRESENTATION($,'Body','MappedRepresentation',(#41));
            #43=IFCPRODUCTDEFINITIONSHAPE($,$,(#42));
            #44=IFCLOCALPLACEMENT($,#9);
            #45=IFCMEMBER('g',$,'M',$,$,#44,#43,$);
            #46=IFCPROJECT('p',$,'P',$,$,$,$,(#5));
            """);

        var (built, _) = ModelAssembler.BuildModel(model.Context.Resolver);
        Assert.That(built.Instances, Has.Count.EqualTo(1));
        Assert.That(built.Meshes.Sum(m => m.FaceIndices.Count), Is.GreaterThan(0));
    }
}
