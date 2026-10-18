using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.Memory;

namespace Ara3D.Step.Mesher.Tests.Model;

public static class ColourTests
{
    static string Style(int id, string target, string colour)
        => $"""
            #{id}={colour};
            #{id + 1}=FILL_AREA_STYLE_COLOUR('',#{id});
            #{id + 2}=FILL_AREA_STYLE('',(#{id + 1}));
            #{id + 3}=SURFACE_STYLE_FILL_AREA(#{id + 2});
            #{id + 4}=SURFACE_SIDE_STYLE('',(#{id + 3}));
            #{id + 5}=SURFACE_STYLE_USAGE(.BOTH.,#{id + 4});
            #{id + 6}=PRESENTATION_STYLE_ASSIGNMENT((#{id + 5}));
            #{id + 7}=STYLED_ITEM('color',(#{id + 6}),{target});
            """;

    static StepPlacedItem CubeWith(string styleLines, StepMeshingDiagnostics? diagnostics = null)
    {
        var text = File.ReadAllText(StepText.Fixture("unit-cube.stp")).Replace("ENDSEC;\nEND-ISO", styleLines + "\nENDSEC;\nEND-ISO");
        Assert.That(text, Does.Contain(styleLines));
        using var document = new StepDocument(Encoding.ASCII.GetBytes(text).Fix());
        var collector = new StepShapeCollector(new StepModel(document), diagnostics ?? new StepMeshingDiagnostics());
        collector.AddProducts();
        return collector.Placed.Single();
    }

    [Test]
    public static void SolidColourIsRead()
    {
        var c = CubeWith(Style(200, "#126", "COLOUR_RGB('',0.2,0.4,0.6)")).Colour!.Value;
        Assert.That(new[] { (float)c.R, (float)c.G, (float)c.B, (float)c.A }, Is.EqualTo(new[] { 0.2f, 0.4f, 0.6f, 1f }).Within(1e-6f));
    }

    [Test]
    public static void PreDefinedColourIsRead()
        => Assert.That((float)CubeWith(Style(200, "#126", "DRAUGHTING_PRE_DEFINED_COLOUR('red')")).Colour!.Value.R, Is.EqualTo(1f));

    [Test]
    public static void MostCommonFaceColourIsUsedAndRecorded()
    {
        var diagnostics = new StepMeshingDiagnostics();
        var placed = CubeWith(Style(200, "#119", "COLOUR_RGB('',1.,0.,0.)") + Style(210, "#120", "COLOUR_RGB('',0.,0.,1.)")
            + Style(220, "#121", "COLOUR_RGB('',0.,0.,1.)"), diagnostics);
        Assert.That((float)placed.Colour!.Value.B, Is.EqualTo(1f));
        Assert.That(diagnostics.Messages, Has.Some.Contains("faces carry 2 colours"));
    }

    [Test]
    public static void UnstyledCubeHasNoColour()
        => Assert.That(CubeWith("").Colour, Is.Null);
}
