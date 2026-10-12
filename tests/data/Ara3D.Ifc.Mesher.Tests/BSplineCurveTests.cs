using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class BSplineCurveTests
{
    static readonly double HalfRoot2 = Math.Sqrt(2) / 2;

    [Test]
    public void RationalQuadratic_QuarterCircle_StaysOnUnitCircle()
    {
        var curve = new SplineDefinition(
            2,
            [0, 0, 0, 1, 1, 1],
            [new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0)],
            [1, HalfRoot2, 1]);

        var points = BSplineEvaluator.Sample(curve, new SampleCount(24));

        Assert.That(points, Has.Count.EqualTo(25));
        foreach (var p in points)
            Assert.That(Math.Sqrt(p.X.Value * p.X.Value + p.Y.Value * p.Y.Value), Is.EqualTo(1.0).Within(1e-4));
        Assert.That(points[0].X.Value, Is.EqualTo(1f).Within(1e-6f));
        Assert.That(points[^1].Y.Value, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void ClampedUniformCubic_AtInteriorKnot_MatchesDeBoor()
    {
        // Knots 0 0 0 0 1 2 2 2 2. By hand at t = 1: 0.25 P1 + 0.5 P2 + 0.25 P3 = (2.75, 1.5).
        var curve = SplineDefinition.UniformClamped(3,
        [
            new Vector3(0, 0, 0), new Vector3(1, 2, 0), new Vector3(3, 2, 0), new Vector3(4, 0, 0), new Vector3(8, 0, 0),
        ]);

        var p = BSplineEvaluator.Evaluate(curve, 1.0);

        Assert.That(curve.IsValid);
        Assert.That(p.X.Value, Is.EqualTo(2.75f).Within(1e-5f));
        Assert.That(p.Y.Value, Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(BSplineEvaluator.Evaluate(curve, 0).X.Value, Is.EqualTo(0f).Within(1e-6f));
        Assert.That(BSplineEvaluator.Evaluate(curve, 2).X.Value, Is.EqualTo(8f).Within(1e-6f));
    }

    [Test]
    public void SampleCount_GrowsWithTurningAndNeverDropsBelowSpans()
    {
        var gentle = SampleCount.FromTurning(0.1f, 32, spans: 1, degree: 3);
        var tight = SampleCount.FromTurning(MathF.PI * 2, 32, spans: 1, degree: 3);

        Assert.That(tight.Segments, Is.GreaterThan(gentle.Segments));
        Assert.That(tight.Segments, Is.EqualTo(32));
        Assert.That(gentle.Segments, Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    [GeometryCoverage("IFCBSPLINECURVEWITHKNOTS")]
    public void KnotCurveProfile_ExtrudesToTheCurveBounds()
    {
        using var model = ParseLoop("#5=IFCBSPLINECURVEWITHKNOTS(3,(#1,#2,#3,#1),.UNSPECIFIED.,.T.,.F.,(4,4),(0.,1.),.UNSPECIFIED.);");

        AssertLoopBounds(model);
    }

    [Test]
    [GeometryCoverage("IFCBSPLINECURVE")]
    public void PlainCurveProfile_UsesUniformKnots()
    {
        using var model = ParseLoop("#5=IFCBSPLINECURVE(3,(#1,#2,#3,#1),.UNSPECIFIED.,.T.,.F.);");

        AssertLoopBounds(model);
    }

    [Test]
    [GeometryCoverage("IFCRATIONALBSPLINECURVEWITHKNOTS")]
    public void RationalCircleProfile_ExtrudesToTheUnitCircleBounds()
    {
        // Four rational quarter arcs: nine control points, weights 1 and sqrt(2)/2 alternating.
        using var model = MicroIfc.Parse(string.Join(Environment.NewLine,
            "#1=IFCCARTESIANPOINT((1.,0.));",
            "#2=IFCCARTESIANPOINT((1.,1.));",
            "#3=IFCCARTESIANPOINT((0.,1.));",
            "#4=IFCCARTESIANPOINT((-1.,1.));",
            "#5=IFCCARTESIANPOINT((-1.,0.));",
            "#6=IFCCARTESIANPOINT((-1.,-1.));",
            "#7=IFCCARTESIANPOINT((0.,-1.));",
            "#8=IFCCARTESIANPOINT((1.,-1.));",
            "#9=IFCRATIONALBSPLINECURVEWITHKNOTS(2,(#1,#2,#3,#4,#5,#6,#7,#8,#1),.UNSPECIFIED.,.T.,.F.,"
                + "(3,2,2,2,3),(0.,0.25,0.5,0.75,1.),.UNSPECIFIED.,"
                + "(1.,0.7071067811865476,1.,0.7071067811865476,1.,0.7071067811865476,1.,0.7071067811865476,1.));",
            "#10=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'Circle',#9);",
            "#11=IFCDIRECTION((0.,0.,1.));",
            "#12=IFCEXTRUDEDAREASOLID(#10,$,#11,2.);"));

        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(12));

        Assert.That(mesh, Is.Not.Null, string.Join("; ", model.Context.Diagnostics.Messages.TakeLast(3)));
        var points = mesh!.Value.Points;
        Assert.Multiple(() =>
        {
            Assert.That(points.Min(p => p.X.Value), Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(points.Max(p => p.X.Value), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(points.Min(p => p.Y.Value), Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(points.Max(p => p.Y.Value), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(points.Max(p => p.Z.Value), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(points.Where(p => p.Z.Value == 0f).Max(p => p.X.Value * p.X.Value + p.Y.Value * p.Y.Value),
                Is.EqualTo(1f).Within(1e-3f));
        });
    }

    [Test]
    public void RationalEntity_QuarterCircle_ReadsWeightsAndKnots()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((1.,0.));
            #2=IFCCARTESIANPOINT((1.,1.));
            #3=IFCCARTESIANPOINT((0.,1.));
            #4=IFCRATIONALBSPLINECURVEWITHKNOTS(2,(#1,#2,#3),.UNSPECIFIED.,.F.,.F.,(3,3),(0.,1.),.UNSPECIFIED.,(1.,0.7071067811865476,1.));
            """);

        var points = BSplineCurves.Evaluate2D(model.Context, model.Entity(4));

        Assert.That(points, Has.Count.GreaterThan(8));
        foreach (var p in points)
            Assert.That(Math.Sqrt(p.X.Value * p.X.Value + p.Y.Value * p.Y.Value), Is.EqualTo(1.0).Within(1e-4));
    }

    // A cubic Bezier loop: P0 = P3 = (0,0), P1 = (3,0), P2 = (3,3).
    // x(t) = 9t(1-t) peaks at 2.25; y(t) = 9t^2(1-t) peaks at 4/3 (t = 2/3). The control polygon reaches (3,3).
    const string LoopPoints = """
        #1=IFCCARTESIANPOINT((0.,0.));
        #2=IFCCARTESIANPOINT((3.,0.));
        #3=IFCCARTESIANPOINT((3.,3.));
        """;

    const string ExtrudeCurve = """
        #6=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'Loop',#5);
        #7=IFCDIRECTION((0.,0.,1.));
        #8=IFCEXTRUDEDAREASOLID(#6,$,#7,2.);
        """;

    static MicroIfcModel ParseLoop(string curveLine)
        => MicroIfc.Parse(string.Join(Environment.NewLine, LoopPoints, curveLine, ExtrudeCurve));

    static void AssertLoopBounds(MicroIfcModel model)
    {
        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(8));
        Assert.That(mesh, Is.Not.Null, string.Join("; ", model.Context.Diagnostics.Messages.TakeLast(3)));
        var points = mesh!.Value.Points;
        Assert.Multiple(() =>
        {
            Assert.That(points.Min(p => p.X.Value), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(points.Max(p => p.X.Value), Is.EqualTo(2.25f).Within(0.01f));
            Assert.That(points.Min(p => p.Y.Value), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(points.Max(p => p.Y.Value), Is.EqualTo(4f / 3f).Within(0.02f));
            Assert.That(points.Max(p => p.Z.Value), Is.EqualTo(2f).Within(1e-5f));
        });
    }
}
