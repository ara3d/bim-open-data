using System.Text;
using Ara3D.IO.StepParser;

namespace Ara3D.StepTypes.Tests;

/// <summary>The string and typed-measure readers, on small hand-written files.</summary>
public static class StepValueReadTests
{
    private const string Header = "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'1');\nFILE_NAME('t','',(''),(''),'','','');\nFILE_SCHEMA(('AP242'));\nENDSEC;\nDATA;\n";

    private static string Name(string raw)
    {
        var path = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, $"value-{Guid.NewGuid():N}.step");
        File.WriteAllText(path, Header + $"#1=DRAUGHTING_PRE_DEFINED_COLOUR({raw});\nENDSEC;\nEND-ISO-10303-21;\n", Encoding.ASCII);
        using var document = new StepDocument(path);
        return new StepModel(document).Get(1).Text(DraughtingPreDefinedColour.Instance.Name)!;
    }

    private static double? Measure(string value)
    {
        var path = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, $"value-{Guid.NewGuid():N}.step");
        File.WriteAllText(path, Header + $"#1=MEASURE_WITH_UNIT({value},#2);\nENDSEC;\nEND-ISO-10303-21;\n", Encoding.ASCII);
        using var document = new StepDocument(path);
        return new StepModel(document).Get(1).Measure(MeasureWithUnit.Instance.ValueComponent);
    }

    [TestCase("'plain'", "plain")]
    [TestCase("'it''s'", "it's")]
    [TestCase(@"'a\b'", @"a\b")]
    [TestCase(@"'caf\S\i'", "café")]
    [TestCase(@"'\PA\caf\S\i'", "café")]
    [TestCase(@"'\X\E9t\X\E9'", "été")]
    [TestCase(@"'\X2\00E900E8\X0\!'", "éè!")]
    [TestCase(@"'\X2\D83DDE00\X0\'", "\U0001F600")]
    [TestCase(@"'\X4\0001F600\X0\'", "\U0001F600")]
    [TestCase(@"'\PB\x\S\y'", @"\PB\x\S\y")]
    [TestCase(@"'\X2\00E\X0\'", @"\X2\00E\X0\")]
    [TestCase(@"'\X2\00E9'", @"\X2\00E9")]
    public static void DecodesEscapeDirectives(string literal, string expected)
        => Assert.That(Name(literal), Is.EqualTo(expected));

    [Test]
    public static void LineBreaksInAStringAreDropped()
        => Assert.That(Name("'ab\ncd'"), Is.EqualTo("abcd"));

    [Test]
    public static void UnsetStringReadsAsNull()
        => Assert.That(Name("$"), Is.Null);

    [Test]
    public static void NonStringValueThrows()
        => Assert.Throws<InvalidDataException>(() => Name("12"));

    [TestCase("LENGTH_MEASURE(25.4)", 25.4)]
    [TestCase("POSITIVE_LENGTH_MEASURE(1.E-07)", 1e-7)]
    [TestCase("0.5", 0.5)]
    public static void MeasureReadsTypedAndBareNumbers(string value, double expected)
        => Assert.That(Measure(value), Is.EqualTo(expected).Within(1e-15));

    [Test]
    public static void UnsetMeasureReadsAsNull()
        => Assert.That(Measure("$"), Is.Null);
}
