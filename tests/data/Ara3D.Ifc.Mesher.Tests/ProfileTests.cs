using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class ProfileTests
{
    [Test]
    public void RectangleProfile_AreaAndBounds()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'R',$,4.,2.);
            """);

        var ctx = model.Context;
        var profile = ProfileBuilder.Build(ctx, model.Entity(1));

        Assert.That(profile.Outer, Has.Count.EqualTo(4));
        Assert.That(profile.Area, Is.EqualTo(8.0).Within(1e-4));
        Assert.That((float)profile.Bounds.Min.X.Value, Is.EqualTo(-2f).Within(1e-5f));
        Assert.That((float)profile.Bounds.Max.X.Value, Is.EqualTo(2f).Within(1e-5f));
        Assert.That((float)profile.Bounds.Min.Y.Value, Is.EqualTo(-1f).Within(1e-5f));
        Assert.That((float)profile.Bounds.Max.Y.Value, Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void CircleProfile_ApproximateArea()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCIRCLEPROFILEDEF(.AREA.,'C',$,2.);
            """);

        var ctx = model.Context;
        var profile = ProfileBuilder.Build(ctx, model.Entity(1));
        var expected = Math.PI * 4;
        Assert.That(profile.Area, Is.EqualTo(expected).Within(0.2));
    }

    [Test]
    public void ArbitraryClosedProfile_WithPolyline()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.));
            #3=IFCCARTESIANPOINT((0.,1.));
            #4=IFCPOLYLINE((#1,#2,#3,#1));
            #5=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'Tri',#4);
            """);

        var ctx = model.Context;
        var profile = ProfileBuilder.Build(ctx, model.Entity(5));
        Assert.That(profile.Area, Is.EqualTo(1.0).Within(1e-4));
    }

    [Test]
    public void CircleHollowProfile_InnerRadiusFromWallThickness()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCIRCLEHOLLOWPROFILEDEF(.AREA.,'Pipe',$,10.,2.);
            """);

        var ctx = model.Context;
        var profile = ProfileBuilder.Build(ctx, model.Entity(1));
        var outerArea = Math.PI * 100;
        var innerArea = Math.PI * 64;
        var expected = outerArea - innerArea;
        Assert.That(profile.Area, Is.EqualTo(expected).Within(2.0));
    }

    [Test]
    public void ArbitraryOpenProfile_ReturnsOpenPath()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((3.,0.));
            #3=IFCPOLYLINE((#1,#2));
            #4=IFCARBITRARYOPENPROFILEDEF(.CURVE.,'Path',#3);
            """);

        var ctx = model.Context;
        var path = ProfileBuilder.BuildOpen(ctx, model.Entity(4));
        Assert.That(path, Has.Count.EqualTo(2));
        Assert.That((float)path[1].X, Is.EqualTo(3f).Within(1e-5f));
    }

    [Test]
    public void ArbitraryProfileDef_ClosedCurve_BuildsArea()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.));
            #2=IFCCARTESIANPOINT((2.,0.));
            #3=IFCCARTESIANPOINT((0.,2.));
            #4=IFCPOLYLINE((#1,#2,#3,#1));
            #5=IFCARBITRARYPROFILEDEF(.AREA.,'Tri',#4);
            """);

        var ctx = model.Context;
        var profile = ProfileBuilder.Build(ctx, model.Entity(5));
        Assert.That(profile.Area, Is.EqualTo(2.0).Within(1e-4));
    }

    [Test]
    public void SteelProfiles_BuildWithoutError()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCLSHAPEPROFILEDEF(.AREA.,'L',$,2.5,2.5,0.375,$,$,$,$);
            #2=IFCTSHAPEPROFILEDEF(.AREA.,'T',$,6.,8.,0.37,0.64,$,$,$,$,$,$);
            #3=IFCUSHAPEPROFILEDEF(.AREA.,'C',$,10.,2.6,0.24,0.436,$,$,$,$);
            #4=IFCRECTANGLEHOLLOWPROFILEDEF(.AREA.,'RHS',$,4.,6.,0.25,$,$);
            """);

        var ctx = model.Context;
        Assert.That(ProfileBuilder.Build(ctx, model.Entity(1)).Area, Is.GreaterThan(0));
        Assert.That(ProfileBuilder.Build(ctx, model.Entity(2)).Area, Is.GreaterThan(0));
        Assert.That(ProfileBuilder.Build(ctx, model.Entity(3)).Area, Is.GreaterThan(0));
        Assert.That(ProfileBuilder.Build(ctx, model.Entity(4)).Area, Is.GreaterThan(0));
    }

    [Test]
    public void Profile_TriangulationProducesTriangles()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'R',$,2.,2.);
            """);

        var ctx = model.Context;
        var tris = ProfileBuilder.Build(ctx, model.Entity(1)).Triangulate();
        Assert.That(tris, Has.Count.EqualTo(2));
    }

    /// <summary>A square hollow section as example.ifc writes it (TKT-65): both boundaries are
    /// composite curves of lines and trimmed circles, and the inner arcs carry SameSense .F.</summary>
    [Test]
    public void ArbitraryProfileWithVoids_RoundedSquareTube_KeepsInnerBoundary()
    {
        using var model = MicroIfc.Parse("#23=IFCDIRECTION((1.,0.));\n" + SquareTubeProfile);

        var profile = ProfileBuilder.Build(model.Context, model.Entity(12509));

        Assert.That(profile.Holes, Has.Count.EqualTo(1));
        Assert.That(profile.Area, Is.EqualTo(2132.9).Within(40));
        var tris = profile.Triangulate();
        var covered = tris.Sum(t => Math.Abs((t.B.X.Value - t.A.X.Value) * (t.C.Y.Value - t.A.Y.Value) - (t.C.X.Value - t.A.X.Value) * (t.B.Y.Value - t.A.Y.Value)) / 2);
        Assert.That(covered, Is.EqualTo(profile.Area).Within(1.0), "triangles overlap or leave gaps");
    }

    internal const string SquareTubeProfile = """
#12369=IFCCARTESIANPOINT((-35.,-35.));
#12371=IFCAXIS2PLACEMENT2D(#12369,#23);
#12372=IFCCIRCLE(#12371,15.);
#12373=IFCTRIMMEDCURVE(#12372,(IFCPARAMETERVALUE(180.)),(IFCPARAMETERVALUE(270.)),.T.,.PARAMETER.);
#12376=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12373);
#12377=IFCCARTESIANPOINT((-35.,-50.));
#12379=IFCCARTESIANPOINT((35.,-50.));
#12381=IFCPOLYLINE((#12377,#12379));
#12383=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12381);
#12384=IFCCARTESIANPOINT((35.,-35.));
#12386=IFCAXIS2PLACEMENT2D(#12384,#23);
#12387=IFCCIRCLE(#12386,15.);
#12388=IFCTRIMMEDCURVE(#12387,(IFCPARAMETERVALUE(270.)),(IFCPARAMETERVALUE(5.08888749034163E-14)),.T.,.PARAMETER.);
#12391=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12388);
#12392=IFCCARTESIANPOINT((50.,-35.));
#12394=IFCCARTESIANPOINT((50.,35.));
#12396=IFCPOLYLINE((#12392,#12394));
#12398=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12396);
#12399=IFCCARTESIANPOINT((35.,35.));
#12401=IFCAXIS2PLACEMENT2D(#12399,#23);
#12402=IFCCIRCLE(#12401,15.);
#12403=IFCTRIMMEDCURVE(#12402,(IFCPARAMETERVALUE(360.)),(IFCPARAMETERVALUE(89.9999999999997)),.T.,.PARAMETER.);
#12406=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12403);
#12407=IFCCARTESIANPOINT((35.,50.));
#12409=IFCCARTESIANPOINT((-35.,50.));
#12411=IFCPOLYLINE((#12407,#12409));
#12413=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12411);
#12414=IFCCARTESIANPOINT((-35.,35.));
#12416=IFCAXIS2PLACEMENT2D(#12414,#23);
#12417=IFCCIRCLE(#12416,15.);
#12418=IFCTRIMMEDCURVE(#12417,(IFCPARAMETERVALUE(90.0000000000001)),(IFCPARAMETERVALUE(180.)),.T.,.PARAMETER.);
#12421=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12418);
#12422=IFCCARTESIANPOINT((-50.,35.));
#12424=IFCCARTESIANPOINT((-50.,-35.));
#12426=IFCPOLYLINE((#12422,#12424));
#12428=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12426);
#12429=IFCCOMPOSITECURVE((#12376,#12383,#12391,#12398,#12406,#12413,#12421,#12428),.F.);
#12439=IFCCARTESIANPOINT((35.,-44.));
#12441=IFCCARTESIANPOINT((-35.,-44.));
#12443=IFCPOLYLINE((#12439,#12441));
#12445=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12443);
#12446=IFCCARTESIANPOINT((-35.,-35.));
#12448=IFCAXIS2PLACEMENT2D(#12446,#23);
#12449=IFCCIRCLE(#12448,8.99999999999957);
#12450=IFCTRIMMEDCURVE(#12449,(IFCPARAMETERVALUE(180.)),(IFCPARAMETERVALUE(270.)),.T.,.PARAMETER.);
#12453=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.F.,#12450);
#12454=IFCCARTESIANPOINT((-44.,-35.));
#12456=IFCCARTESIANPOINT((-44.,35.));
#12458=IFCPOLYLINE((#12454,#12456));
#12460=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12458);
#12461=IFCCARTESIANPOINT((-35.,35.));
#12463=IFCAXIS2PLACEMENT2D(#12461,#23);
#12464=IFCCIRCLE(#12463,8.99999999999955);
#12465=IFCTRIMMEDCURVE(#12464,(IFCPARAMETERVALUE(90.0000000000005)),(IFCPARAMETERVALUE(180.)),.T.,.PARAMETER.);
#12468=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.F.,#12465);
#12469=IFCCARTESIANPOINT((-35.,44.));
#12471=IFCCARTESIANPOINT((35.,44.));
#12473=IFCPOLYLINE((#12469,#12471));
#12475=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12473);
#12476=IFCCARTESIANPOINT((35.,35.));
#12478=IFCAXIS2PLACEMENT2D(#12476,#23);
#12479=IFCCIRCLE(#12478,8.99999999999979);
#12480=IFCTRIMMEDCURVE(#12479,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(90.0000000000001)),.T.,.PARAMETER.);
#12483=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.F.,#12480);
#12484=IFCCARTESIANPOINT((44.,35.));
#12486=IFCCARTESIANPOINT((44.,-35.));
#12488=IFCPOLYLINE((#12484,#12486));
#12490=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#12488);
#12491=IFCCARTESIANPOINT((35.,-35.));
#12493=IFCAXIS2PLACEMENT2D(#12491,#23);
#12494=IFCCIRCLE(#12493,8.9999999999998);
#12495=IFCTRIMMEDCURVE(#12494,(IFCPARAMETERVALUE(270.)),(IFCPARAMETERVALUE(3.56222124323914E-13)),.T.,.PARAMETER.);
#12498=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.F.,#12495);
#12499=IFCCOMPOSITECURVE((#12445,#12453,#12460,#12468,#12475,#12483,#12490,#12498),.F.);
#12509=IFCARBITRARYPROFILEDEFWITHVOIDS(.AREA.,'100x6.0SHS',#12429,(#12499));
""";
}
