using System.Text.RegularExpressions;

namespace Ara3D.Step.Decode.Tests;

/// <summary>TKT-422: a file written with ISO 10303 entity short names (CRTPNT) decodes as its long-name twin does.</summary>
public static class ShortNameTests
{
    /// <summary>The names unit-cube.stp writes, each with its short name (hand-copied from ISO 10303 via OCCT).</summary>
    private static readonly (string Long, string Short)[] Names =
    [
        ("ADVANCED_BREP_SHAPE_REPRESENTATION", "ABSR"), ("ADVANCED_FACE", "ADVFC"), ("APPLICATION_CONTEXT", "APPCNT"),
        ("APPLICATION_PROTOCOL_DEFINITION", "APPRDF"), ("AXIS2_PLACEMENT_3D", "A2PL3D"), ("CARTESIAN_POINT", "CRTPNT"),
        ("CLOSED_SHELL", "CLSSHL"), ("DIRECTION", "DRCTN"), ("EDGE_CURVE", "EDGCRV"), ("EDGE_LOOP", "EDGLP"),
        ("FACE_OUTER_BOUND", "FCOTBN"), ("MANIFOLD_SOLID_BREP", "MNSLBR"), ("ORIENTED_EDGE", "ORNEDG"), ("PRODUCT", "PRDCT"),
        ("PRODUCT_CONTEXT", "PRDCNT"), ("PRODUCT_DEFINITION", "PRDDFN"), ("PRODUCT_DEFINITION_CONTEXT", "PRDFCN"),
        ("PRODUCT_DEFINITION_FORMATION", "PRDFFR"), ("PRODUCT_DEFINITION_SHAPE", "PRDFSH"),
        ("SHAPE_DEFINITION_REPRESENTATION", "SHDFRP"), ("UNCERTAINTY_MEASURE_WITH_UNIT", "UMWU"), ("VERTEX_POINT", "VRTPNT"),
    ];

    private static string DataOf(string file)
    {
        var text = File.ReadAllText(Fixtures.PathOf(file));
        var start = text.IndexOf("DATA;", StringComparison.Ordinal) + 5;
        return text[start..text.IndexOf("ENDSEC;", start, StringComparison.Ordinal)];
    }

    private static string WithShortNames(string data)
        => Names.Aggregate(data, (d, n) => Regex.Replace(d, $@"(?<=[=(])\s*{n.Long}\(", $"{n.Short}("));

    [Test]
    public static void ShortNameFileAndLongNameTwinDecodeToTheSameRawModel()
    {
        var data = DataOf("unit-cube.stp");
        var shortData = WithShortNames(data);
        Assert.That(shortData, Does.Contain("CRTPNT(").And.Not.Contain("CARTESIAN_POINT("));

        var expected = Fixtures.DecodeText(data);
        var actual = Fixtures.DecodeText(shortData);

        Assert.That((int)expected.Model.Faces.Count, Is.EqualTo(6));
        Assert.That(Fixtures.Differences(actual, expected), Is.EqualTo("no difference found row by row"));
    }

    [Test]
    public static void OnlyTheShortNameFileReportsShortNames()
    {
        var data = DataOf("unit-cube.stp");
        Assert.That(Fixtures.DecodeText(data).Codes(), Does.Not.Contain("STEP-SHORT-NAMES"));
        var report = Fixtures.DecodeText(WithShortNames(data)).Diagnostics.Single(d => (string)d.Code == "STEP-SHORT-NAMES");
        Assert.That((double)report.Measure, Is.GreaterThan(0));
    }
}
