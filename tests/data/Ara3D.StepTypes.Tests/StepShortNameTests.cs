using System.Text;
using Ara3D.Memory;
using Ara3D.IO.StepParser;

namespace Ara3D.StepTypes.Tests;

/// <summary>The entity short names (StepShortNames.tsv) as a file writes them resolve to the long-name descriptor.</summary>
public static class StepShortNameTests
{
    /// <summary>Every entity name STEP Tools' clip.stp (AP203, data/step-samples/steptools-ap203) writes.</summary>
    private const string ClipNames = "A2PL3D APDTTM APPCNT APPRDF APPRL APPROR APPRVL APPSTT CCDSAP CDDATA CDPAOA CDSC CIRCLE CLNDT CLSSHL "
        + "CRTPNT CUTO DMNEXP DRCTN DSGCNT DTANTM DTTMRL FBSR FCOTBN FCSRF FCTBR GBSSR GMTST LCLTM LINE LMWU MCHCNT ORGNZT PAMWU PAOR "
        + "PDFWSS PERSON PLANE PLYLP PRANOR PRCTRL PRDCT PRDCTG PRDDFN PRDFSH PRPC PRSADD SCCLLV SCRCLS SHDFRP SHPRPR SHRPRL TRMCRV "
        + "UMWU VECTOR";

    private static readonly Dictionary<string, string> Known = new()
    {
        ["CRTPNT"] = "CARTESIAN_POINT", ["DRCTN"] = "DIRECTION", ["PLYLP"] = "POLY_LOOP", ["FCSRF"] = "FACE_SURFACE",
        ["FCOTBN"] = "FACE_OUTER_BOUND", ["TRMCRV"] = "TRIMMED_CURVE", ["PLANE"] = "PLANE", ["LINE"] = "LINE",
    };

    private static List<(string Written, StepToken Token)> Tokens(IEnumerable<string> names, out StepDocument document)
    {
        var data = string.Concat(names.Select((n, i) => $"#{i + 1}={n}($);\n"));
        var text = "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\nFILE_NAME('n.stp','2026-10-22T00:00:00',(''),(''),'','','');\n"
            + "FILE_SCHEMA(('CONFIG_CONTROL_DESIGN'));\nENDSEC;\nDATA;\n" + data + "ENDSEC;\nEND-ISO-10303-21;\n";
        var opened = new StepDocument(Encoding.ASCII.GetBytes(text).Fix());
        document = opened;
        return names.Select((n, i) => (n, opened.Definitions[i].NameToken)).ToList();
    }

    [Test]
    public static void EveryNameClipWritesResolvesToADescriptor()
    {
        var t = Tokens(ClipNames.Split(' '), out var document);
        using (document)
            Assert.That(t.Where(x => StepSchemaIndex.Find(x.Token) == null).Select(x => x.Written), Is.Empty);
    }

    [Test]
    public static void ShortNamesGiveTheLongNamesDescriptor()
    {
        var t = Tokens(Known.Keys, out var document);
        using (document)
            foreach (var (written, token) in t)
                Assert.That(StepSchemaIndex.Find(token)?.EntityName, Is.EqualTo(Known[written]), written);
    }

    [Test]
    public static void OnlyAShortNameThatIsNotAlsoALongNameIsReportedAsShort()
    {
        var t = Tokens(["CRTPNT", "CARTESIAN_POINT", "LINE", "NOT_AN_ENTITY"], out var document);
        using (document)
            Assert.That(t.Select(x => StepSchemaIndex.IsShortName(x.Token)), Is.EqualTo(new[] { true, false, false, false }));
    }
}
