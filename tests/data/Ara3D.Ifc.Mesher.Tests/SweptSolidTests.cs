using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class SweptSolidTests
{
    [Test]
    public void SurfaceOfLinearExtrusion_OpenProfile_RibbonBounds()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((4.,0.));
            #3=IFCPOLYLINE((#1,#2));
            #4=IFCARBITRARYOPENPROFILEDEF(.CURVE.,'Path',#3);
            #5=IFCDIRECTION((0.,0.,1.));
            #6=IFCSURFACEOFLINEAREXTRUSION(#4,$,#5,2.);
            """);

        var ctx = model.Context;
        var mesh = SweptSolids.BuildSurfaceOfLinearExtrusion(ctx, model.Entity(6));
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(2));
        var bounds = MeshHelpers.GetBounds(mesh);
        Assert.That((float)bounds.Min.Z.Value, Is.EqualTo(0f).Within(1e-5f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(2f).Within(1e-5f));
        Assert.That((float)bounds.Max.X.Value, Is.EqualTo(4f).Within(1e-5f));
    }

    [Test]
    public void SurfaceOfLinearExtrusion_WithPlacement()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((3.,0.));
            #3=IFCPOLYLINE((#1,#2));
            #4=IFCARBITRARYOPENPROFILEDEF(.CURVE.,'Path',#3);
            #5=IFCCARTESIANPOINT((10.,0.,0.));
            #6=IFCAXIS2PLACEMENT3D(#5,$,$);
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCSURFACEOFLINEAREXTRUSION(#4,#6,#7,1.);
            """);

        var ctx = model.Context;
        var mesh = SweptSolids.BuildSurfaceOfLinearExtrusion(ctx, model.Entity(8));
        var bounds = MeshHelpers.GetBounds(mesh);
        Assert.That((float)bounds.Min.X.Value, Is.EqualTo(10f).Within(1e-4f));
        Assert.That((float)bounds.Max.X.Value, Is.EqualTo(13f).Within(1e-4f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void Duplex_OpenProfileRibbon_WithAxisPlacement()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.2084999999999992,-17.59149999999997));
            #2=IFCCARTESIANPOINT((6.261999999999995,-17.59149999999999));
            #3=IFCPOLYLINE((#1,#2));
            #4=IFCARBITRARYOPENPROFILEDEF(.CURVE.,$,#3);
            #5=IFCCARTESIANPOINT((0.,0.,0.));
            #6=IFCAXIS2PLACEMENT3D(#5,$,$);
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCSURFACEOFLINEAREXTRUSION(#4,#6,#7,2.6);
            """);

        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(8));
        Assert.That(mesh, Is.Not.Null);
        var bounds = MeshHelpers.GetBounds(mesh!.Value);
        Assert.That((float)bounds.Min.Y.Value, Is.EqualTo(-17.5915f).Within(1e-3f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(2.6f).Within(1e-3f));
        Assert.That((float)(bounds.Max.X.Value - bounds.Min.X.Value), Is.EqualTo(6.0535f).Within(1e-3f));
    }

    [Test]
    public void OpenProfile_CompositeCurveRibbon_Builds()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.));
            #3=IFCCARTESIANPOINT((3.,1.));
            #4=IFCPOLYLINE((#1,#2));
            #5=IFCPOLYLINE((#2,#3));
            #6=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#4);
            #7=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#5);
            #8=IFCCOMPOSITECURVE((#6,#7),.F.);
            #9=IFCARBITRARYOPENPROFILEDEF(.CURVE.,$,#8);
            #10=IFCDIRECTION((0.,0.,1.));
            #11=IFCSURFACEOFLINEAREXTRUSION(#9,$,#10,1.);
            """);

        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(11));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
        var bounds = MeshHelpers.GetBounds(mesh.Value);
        Assert.That((float)bounds.Max.X.Value, Is.EqualTo(3f).Within(1e-3f));
        Assert.That((float)bounds.Max.Y.Value, Is.EqualTo(1f).Within(1e-3f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void ExtrudedAreaSolid_Rectangle_Bounds()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'Box',$,2.,3.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,4.);
            """);

        var ctx = model.Context;
        var mesh = SweptSolids.BuildExtrudedAreaSolid(ctx, model.Entity(3));
        var bounds = MeshHelpers.GetBounds(mesh);
        Assert.That((float)bounds.Min.Z.Value, Is.EqualTo(0f).Within(1e-5f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(4f).Within(1e-5f));
        Assert.That((float)bounds.Min.X.Value, Is.EqualTo(-1f).Within(1e-5f));
        Assert.That((float)bounds.Max.X.Value, Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void ExtrudedAreaSolid_WithPlacement()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'Box',$,2.,2.);
            #2=IFCCARTESIANPOINT((10.,20.,30.));
            #3=IFCAXIS2PLACEMENT3D(#2,$,$);
            #4=IFCDIRECTION((0.,0.,1.));
            #5=IFCEXTRUDEDAREASOLID(#1,#3,#4,4.);
            """);

        var ctx = model.Context;
        var mesh = SweptSolids.BuildExtrudedAreaSolid(ctx, model.Entity(5));
        var bounds = MeshHelpers.GetBounds(mesh);
        Assert.That((float)bounds.Min.X.Value, Is.EqualTo(9f).Within(1e-4f));
        Assert.That((float)bounds.Max.Z.Value, Is.EqualTo(34f).Within(1e-4f));
    }

    [Test]
    public void SmallInchBoltShankCircleProfile_Triangulates()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCIRCLEPROFILEDEF(.AREA.,'Bolt shank',$,0.375);
            """, lengthScaleOverride: 0.0254, circleSegments: 32);

        var profile = ProfileBuilder.Build(model.Context, model.Entity(1));
        Assert.That(profile.Outer, Has.Count.EqualTo(32));
        Assert.That(profile.Triangulate(), Has.Count.EqualTo(30));
    }

    /// <summary>The extrusion of the example.ifc square tube (TKT-65) holds only the wall material:
    /// profile area 2133 times depth 1000, not the filled square's 9807 times 1000.</summary>
    [Test]
    public void ExtrudedAreaSolid_ArbitraryProfileWithVoids_VolumeIsTheWall()
    {
        using var model = MicroIfc.Parse(
            "#23=IFCDIRECTION((1.,0.));\n#90=IFCDIRECTION((0.,0.,1.));\n"
            + "#91=IFCEXTRUDEDAREASOLID(#12509,$,#90,1000.);\n" + ProfileTests.SquareTubeProfile);

        var mesh = SweptSolids.BuildExtrudedAreaSolid(model.Context, model.Entity(91));

        Assert.That(Math.Abs(MeshHelpers.SignedVolume(mesh)), Is.EqualTo(2.1329e6).Within(1e5));
    }
}
