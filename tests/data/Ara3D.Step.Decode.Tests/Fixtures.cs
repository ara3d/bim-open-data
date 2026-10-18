using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.Memory;
using Ara3D.StepBrep;

namespace Ara3D.Step.Decode.Tests;

/// <summary>The fixtures decoded, hand-written STEP decoded from memory, and a description of where two raw models
/// differ, so a failed comparison says which table and row.</summary>
public static class Fixtures
{
    public static string PathOf(string name)
        => Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", name);

    public static RawDecodedModel Decode(string name)
    {
        using var document = new StepDocument(PathOf(name));
        return document.DecodeRaw();
    }

    public static RawDecodedModel DecodeText(string dataLines)
    {
        var text = "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\n"
            + "FILE_NAME('inline.stp','2026-10-17T00:00:00',(''),(''),'','','');\n"
            + "FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));\nENDSEC;\nDATA;\n"
            + dataLines + "\nENDSEC;\nEND-ISO-10303-21;\n";
        using var document = new StepDocument(Encoding.ASCII.GetBytes(text).Fix());
        return document.DecodeRaw();
    }

    public static IReadOnlyList<string> Codes(this RawDecodedModel raw)
        => raw.Diagnostics.Select(d => (string)d.Code).ToList();

    /// <summary>Every part of the raw models that differs, with the first differing row of each table.</summary>
    public static string Differences(RawDecodedModel actual, RawDecodedModel expected)
    {
        var found = new List<string>();
        var a = actual.Model;
        var e = expected.Model;
        Compare(found, "curves", a.Geometry.Curves, e.Geometry.Curves);
        Compare(found, "surfaces", a.Geometry.Surfaces, e.Geometry.Surfaces);
        Compare(found, "curves2D", a.Geometry.Curves2D, e.Geometry.Curves2D);
        Compare(found, "pcurves", a.Geometry.Pcurves, e.Geometry.Pcurves);
        Compare(found, "vertices", a.Vertices, e.Vertices);
        Compare(found, "edges", a.Edges, e.Edges);
        Compare(found, "loops", a.Loops, e.Loops);
        Compare(found, "faces", a.Faces, e.Faces);
        Compare(found, "shells", a.Shells, e.Shells);
        Compare(found, "bodies", a.Bodies, e.Bodies);
        Compare(found, "occurrences", actual.Occurrences, expected.Occurrences);
        Compare(found, "sources", actual.Sources.AllSources(), expected.Sources.AllSources());
        Compare(found, "folds", actual.Folds, expected.Folds);
        Compare(found, "repairs", actual.Repairs, expected.Repairs);
        Compare(found, "units", actual.Units, expected.Units);
        if (!a.Tolerances.Equals(e.Tolerances) || !a.Unit.Equals(e.Unit))
            found.Add($"tolerances or unit: {a.Tolerances} {a.Unit} vs {e.Tolerances} {e.Unit}");
        if (actual.LengthFactor != expected.LengthFactor || actual.AngleFactor != expected.AngleFactor)
            found.Add($"factors: {actual.LengthFactor}, {actual.AngleFactor} vs {expected.LengthFactor}, {expected.AngleFactor}");
        return found.Count == 0 ? "no difference found row by row" : string.Join("\n", found);
    }

    static void Compare<T>(List<string> found, string name, IReadOnlyList<T> actual, IReadOnlyList<T> expected)
    {
        if (actual.Count != expected.Count)
        {
            found.Add($"{name}: {actual.Count} rows vs {expected.Count}");
            return;
        }
        for (var i = 0; i < actual.Count; i++)
            if (!Equals(actual[i], expected[i]))
            {
                found.Add($"{name}[{i}]: {actual[i]}\n  expected {expected[i]}");
                return;
            }
    }
}
