using Ara3D.Memory;
using Ara3D.StepBrep;

namespace Ara3D.Step.Decode.Tests;

/// <summary>TKT-312's third criterion: what the decoder cannot represent becomes a diagnostic, never an exception.</summary>
public static class DiagnosticTests
{
    [Test]
    public static void TruncatedFileKeepsItsPrefixAndReportsTheSyntaxError()
    {
        var raw = Fixtures.Decode("unit-cube-truncated.stp");
        var syntax = raw.Diagnostics.Single(d => (string)d.Code == "P21-SYNTAX");
        Assert.That((bool)syntax.Where.Equals(Provenance.AtInstance(new StepInstanceId(125))), Is.True);
        Assert.That(raw.Codes(), Does.Contain("STEP-NO-SHAPE"));
        Assert.That((int)raw.Model.Faces.Count, Is.Zero);
    }

    [Test]
    public static void TextThatIsNotStepIsDiagnosed()
    {
        using var document = new IO.StepParser.StepDocument(System.Text.Encoding.ASCII.GetBytes("not a step file").Fix());
        Assert.That(document.DecodeRaw().Codes(), Does.Contain("P21-NOT-A-FILE"));
    }

    [Test]
    public static void ZeroDirectionSkipsTheFaceAndItsShellKeepsTheRest()
    {
        var lines = File.ReadAllText(Fixtures.PathOf("unit-cube.stp"));
        var data = lines[(lines.IndexOf("DATA;", StringComparison.Ordinal) + 5)..lines.IndexOf("ENDSEC;", lines.IndexOf("DATA;", StringComparison.Ordinal), StringComparison.Ordinal)];
        var raw = Fixtures.DecodeText(data.Replace("#106=DIRECTION('',(0.,0.,-1.));", "#106=DIRECTION('',(0.,0.,0.));"));
        Assert.That(raw.Codes(), Does.Contain("STEP-ZERO-DIRECTION"));
        Assert.That(raw.Codes(), Does.Contain("STEP-INSTANCE-SKIPPED"));
        Assert.That(((int)raw.Model.Faces.Count, (int)raw.Model.Geometry.Surfaces.Count, (int)raw.Model.Shells[0].Faces.Count), Is.EqualTo((5, 5, 5)));
        Assert.That(raw.Repairs.Select(r => (int)r.Source.Instance.Value), Is.EquivalentTo(new[] { 113, 119 }));
    }

    [Test]
    public static void DanglingReferenceIsDiagnosed()
    {
        var raw = Fixtures.DecodeText("""
            #1=CARTESIAN_POINT('',(0.,0.,0.));
            #2=VERTEX_POINT('',#1);
            #3=EDGE_CURVE('',#2,#2,#99,.T.);
            #4=EDGE_LOOP('',(#5));
            #5=ORIENTED_EDGE('',*,*,#3,.T.);
            #6=FACE_OUTER_BOUND('',#4,.T.);
            #7=ADVANCED_FACE('',(#6),#98,.T.);
            #8=OPEN_SHELL('',(#7));
            #9=SHELL_BASED_SURFACE_MODEL('',(#8));
            #10=SHAPE_REPRESENTATION('',(#9),#11);
            #11=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT((#12))REPRESENTATION_CONTEXT('',''));
            #12=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
            """);
        Assert.That(raw.Codes(), Does.Contain("P21-DANGLING-REFERENCE"));
        Assert.That(((int)raw.Model.Bodies.Count, (int)raw.Model.Shells.Count, (int)raw.Model.Faces.Count), Is.EqualTo((1, 1, 0)));
    }

    [Test]
    public static void UnsupportedEntityIsCountedOnce()
    {
        var raw = Fixtures.DecodeText("#1=TESSELLATED_SHELL('',(),$);\n#2=TESSELLATED_SHELL('',(),$);");
        var unsupported = raw.Diagnostics.Single(d => (string)d.Code == "STEP-UNSUPPORTED-TYPE");
        Assert.That((double)unsupported.Measure, Is.EqualTo(2));
    }
}
