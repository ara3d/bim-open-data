namespace Ara3D.BimOpenSchema.Tests;

/// <summary>TKT-145: the fantasy office models (Revit 2025) name materials and layers with the
/// 8-bit escape, which has no closing backslash; 'W\X\E4rmed\X\E4mmung' was left undecoded.</summary>
[TestFixture]
public static class IfcStringDecoderTests
{
    [TestCase(@"W\X\E4rmed\X\E4mmung - Hart", "Wärmedämmung - Hart")]
    [TestCase(@"D\X\E4mmung", "Dämmung")]
    [TestCase(@"Caf\X2\00E9\X0\", "Café")]
    [TestCase(@"Gr\X2\00F600DF\X0\e", "Größe")]
    [TestCase(@"\X\C4", "Ä")]
    [TestCase(@"back\\slash", @"back\slash")]
    [TestCase(@"plain", "plain")]
    public static void DecodesStepEscapes(string encoded, string decoded)
        => Assert.That(encoded.DecodeIfc(), Is.EqualTo(decoded));

    [TestCase(@"\X\E")]
    [TestCase(@"\X\ZZ")]
    public static void KeepsAMalformedEscapeAsWritten(string encoded)
        => Assert.That(encoded.DecodeIfc(), Is.EqualTo(encoded));
}
