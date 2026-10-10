using System.Globalization;

namespace Ara3D.BimOpenSchema.IO.Usd.Tests;

[TestFixture]
public sealed class UsdaWriterTests
{
    private static string Write(Action<UsdaWriter> write)
    {
        using var text = new StringWriter(CultureInfo.GetCultureInfo("sv-SE"));
        write(new UsdaWriter(text));
        return text.ToString();
    }

    [TestCase(0, "0")]
    [TestCase(1, "0.0001")]
    [TestCase(-5, "-0.0005")]
    [TestCase(500, "0.05")]
    [TestCase(10_500, "1.05")]
    [TestCase(12_345, "1.2345")]
    [TestCase(20_000, "2")]
    [TestCase(-20_000, "-2")]
    [TestCase(int.MinValue, "-214748.3648")]
    [TestCase(int.MaxValue, "214748.3647")]
    public void Fixed4_IsTheExactDecimalInMetres(int value, string expected)
        => Assert.That(Write(w => w.Fixed4(value)), Is.EqualTo(expected));

    [TestCase(0.1f, "0.1")]
    [TestCase(-1.5f, "-1.5")]
    [TestCase(1e-9f, "1E-09")]
    [TestCase(float.NaN, "nan")]
    [TestCase(float.PositiveInfinity, "inf")]
    [TestCase(float.NegativeInfinity, "-inf")]
    public void Float_IsShortestRoundTripInvariant(float value, string expected)
        => Assert.That(Write(w => w.Float(value)), Is.EqualTo(expected));

    [Test]
    public void Int_IgnoresTheWritersCulture()
        => Assert.That(Write(w => w.Int(-42)), Is.EqualTo("-42"), "sv-SE writes a Unicode minus sign");

    [Test]
    public void Quoted_EscapesQuotesBackslashesAndControlCharacters()
        => Assert.That(Write(w => w.Quoted("a\"b\\c\nd\te\u0001é")), Is.EqualTo(@"""a\""b\\c\nd\te\x01é"""));

    [Test]
    public void Blocks_IndentByFourSpaces()
        => Assert.That(Write(w => w.Line("def Scope \"S\"").Open().Line("x").Close()),
            Is.EqualTo("def Scope \"S\"\n{\n    x\n}\n"));

    [TestCase("Pset_WallCommon", "Pset_WallCommon")]
    [TestCase("PSet_Revit_Identity Data", "PSet_Revit_Identity_Data")]
    [TestCase("Ifc:ObjectType", "Ifc_ObjectType")]
    [TestCase("2D Area", "_2D_Area")]
    [TestCase("", "_")]
    [TestCase("Höhe", "H_he")]
    public void ToIdentifier_MakesAValidUsdIdentifier(string text, string expected)
    {
        Assert.That(UsdNames.ToIdentifier(text), Is.EqualTo(expected));
        Assert.That(UsdNames.IsIdentifier(UsdNames.ToIdentifier(text)), Is.True);
    }
}
