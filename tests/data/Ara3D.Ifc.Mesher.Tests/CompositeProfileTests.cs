using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>A plate profile as some steel exporters write it: a polyline, a quarter arc with
/// SenseAgreement false, and a DISCONTINUOUS closing segment.</summary>
[TestFixture]
public sealed class CompositeProfileTests
{
    const string PlateProfile = """
        #1=IFCCARTESIANPOINT((0.,0.));
        #2=IFCCARTESIANPOINT((140.,0.));
        #3=IFCCARTESIANPOINT((140.,100.));
        #4=IFCCARTESIANPOINT((0.,50.));
        #5=IFCPOLYLINE((#1,#2,#3,#4));
        #6=IFCCARTESIANPOINT((-10.,50.));
        #7=IFCDIRECTION((0.,1.));
        #8=IFCAXIS2PLACEMENT2D(#6,#7);
        #9=IFCCIRCLE(#8,10.);
        #10=IFCTRIMMEDCURVE(#9,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(1.5707963267949)),.F.,.PARAMETER.);
        #11=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#5);
        #12=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#10);
        #13=IFCCARTESIANPOINT((0.,40.));
        #14=IFCPOLYLINE((#4,#13,#1));
        #15=IFCCOMPOSITECURVESEGMENT(.DISCONTINUOUS.,.T.,#14);
        #16=IFCCOMPOSITECURVE((#11,#12,#15),.F.);
        #17=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'plate',#16);
        #18=IFCDIRECTION((0.,0.,1.));
        #19=IFCEXTRUDEDAREASOLID(#17,$,#18,0.015);
        """;

    [Test]
    [Explicit("Diagnostic: composite profile point dump")]
    public void CompositePlateProfile_Diagnosis()
    {
        using var model = MicroIfc.Parse(PlateProfile);

        var curvePts = CurveEvaluator.Evaluate2D(model.Context, model.Entity(16), dropClosure: true);
        TestContext.WriteLine($"curve points ({curvePts.Count}):");
        foreach (var p in curvePts)
            TestContext.WriteLine($"  ({p.X:F4},{p.Y:F4})");
        TestContext.WriteLine($"self-intersect={PolygonWithHoles.HasSelfIntersection(curvePts)}");

        try
        {
            var profile = ProfileBuilder.Build(model.Context, model.Entity(17));
            TestContext.WriteLine($"profile outer={profile.Outer.Count} area={profile.Area:F6}");
            TestContext.WriteLine($"tris={profile.Triangulate().Count}");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"profile build: {ex.GetType().Name}: {ex.Message}");
        }

        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(19));
        TestContext.WriteLine($"extrusion mesh null={mesh is null}");
        foreach (var note in model.Context.Diagnostics.Messages.Take(5))
            TestContext.WriteLine($"diag: {note}");
    }

    [Test]
    public void CompositePlateProfile_WithDiscontinuousClose_Builds()
    {
        using var model = MicroIfc.Parse(PlateProfile);

        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(19));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
    }
}
