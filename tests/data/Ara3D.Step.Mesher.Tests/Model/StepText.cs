using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.Memory;

namespace Ara3D.Step.Mesher.Tests.Model;

/// <summary>Hand-written STEP for tests: data lines wrapped in an AP214 header, parsed from memory, and the
/// committed fixtures by name.</summary>
public static class StepText
{
    public static string Wrap(string dataLines)
        => "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\n"
            + "FILE_NAME('inline.stp','2026-10-17T00:00:00',(''),(''),'','','');\n"
            + "FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));\nENDSEC;\nDATA;\n"
            + dataLines + "\nENDSEC;\nEND-ISO-10303-21;\n";

    /// <summary>A document over the wrapped lines; the caller disposes it.</summary>
    public static StepDocument Parse(string dataLines)
        => new(Encoding.ASCII.GetBytes(Wrap(dataLines)).Fix());

    public static string Fixture(string name)
        => Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", name);

    /// <summary>The instance with the given id, read through a new model over the document.</summary>
    public static StepEntity Entity(this StepDocument document, int id)
        => new StepModel(document).Get(id);
}
