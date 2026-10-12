using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>Drives ifc_ids_check and ifc_ids_specifications through tools/call over the committed
/// Duplex and rule DC-W1 as an IDS (<c>ids/dc-w1.ids</c>, a copy of Ara3D.Ids.Tests' fixture).
/// Ara3D.Ids tests the evaluation; these check the wiring and the answer's shape.</summary>
[TestFixture]
public sealed class IdsToolTests
{
    private static readonly string DcW1 = Path.Combine(RepoPaths.Root, "tests", "mcp", "BimOpenMcp.Ifc.Tests", "ids", "dc-w1.ids");
    private IfcSessionCache _cache = null!;
    private McpServer _mcp = null!;
    private string _bos = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _bos = RepoPaths.Samples("public", "duplex.bos");
        _cache = new IfcSessionCache();
        _mcp = IfcMcpServer.Create(_cache, McpTransport.Stdio);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _mcp.Dispose();
        _cache.Dispose();
    }

    [Test]
    public void Check_DcW1OnTheDuplex_EightDoorsPassAndSixFail()
    {
        var data = _mcp.CallData("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = DcW1 });

        var summary = Records(data["summary"]!);
        Assert.That(summary, Has.Count.EqualTo(1));
        Assert.That(summary[0]["CheckId"]!.GetValue<string>(), Is.EqualTo("DC-W1"));
        Assert.That(summary[0]["Pass"]!.GetValue<long>(), Is.EqualTo(8));
        Assert.That(summary[0]["Fail"]!.GetValue<long>(), Is.EqualTo(6));
        Assert.That(summary[0]["InfoNotAvailable"]!.GetValue<long>(), Is.EqualTo(0));
        Assert.That(summary[0]["Rows"]!.GetValue<long>(), Is.EqualTo(14));
        Assert.That(data["warnings"], Is.InstanceOf<JsonArray>());
        Assert.That(data["warnings"]!.AsArray(), Is.Empty);
        Assert.That(data["verdicts"]!["total"]!.GetValue<long>(), Is.EqualTo(14));
    }

    [Test]
    public void Check_VerdictFail_PagesTheFailuresOnly()
    {
        var data = _mcp.CallData("ifc_ids_check", new JsonObject
        {
            ["path"] = _bos,
            ["idsPath"] = DcW1,
            ["verdict"] = "fail",
            ["take"] = 4,
        });

        var verdicts = data["verdicts"]!;
        Assert.That(verdicts["total"]!.GetValue<long>(), Is.EqualTo(6));
        Assert.That(verdicts["count"]!.GetValue<int>(), Is.EqualTo(4));
        Assert.That(Records(verdicts).Select(r => r["Verdict"]!.GetValue<string>()), Has.All.EqualTo("Fail"));
        Assert.That(Records(verdicts).Select(r => r["GlobalId"]!.GetValue<string>()), Has.All.Not.Empty);
        Assert.That(Records(data["summary"]!)[0]["Pass"]!.GetValue<long>(), Is.EqualTo(8), "the summary counts every row");
    }

    [Test]
    public void Check_MisspelledFacet_ComesBackAsAWarning()
    {
        var misspelled = RepoPaths.Artifacts("bimopenmcp-ifc-tests", $"dc-w1-misspelled-{Guid.NewGuid():N}.ids");
        Directory.CreateDirectory(Path.GetDirectoryName(misspelled)!);
        File.WriteAllText(misspelled, File.ReadAllText(DcW1).Replace("attribute>", "atribute>"));
        try
        {
            var data = _mcp.CallData("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = misspelled });

            Assert.That(data["warnings"]!.AsArray().Select(w => w!.GetValue<string>()), Has.Some.Contains("atribute"));
            Assert.That(Records(data["summary"]!)[0]["Pass"]!.GetValue<long>(), Is.EqualTo(14), "no requirement survived; the warning is the only signal");
        }
        finally
        {
            File.Delete(misspelled);
        }
    }

    [Test]
    public void Check_TwiceOverOneModel_ReusesTheSessionsIdsModel()
    {
        using var cache = new IfcSessionCache();
        using var mcp = IfcMcpServer.Create(cache, McpTransport.Stdio);
        mcp.CallData("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = DcW1, ["take"] = 4 });
        using var lease = cache.LeaseBos(_bos);
        Assert.That(lease.Session.IsIdsModelBuilt, Is.True, "the check builds the model on the session");
        var built = lease.Session.IdsModel;

        mcp.CallData("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = DcW1, ["skip"] = 4, ["take"] = 4 });

        using var again = cache.LeaseBos(_bos);
        Assert.That(again.Session.IdsModel, Is.SameAs(built));
    }

    [Test]
    public void Check_UnknownVerdict_NamesTheChoices()
    {
        var payload = _mcp.Call("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = DcW1, ["verdict"] = "Failed" });

        Assert.That(payload["ok"]!.GetValue<bool>(), Is.False);
        Assert.That(payload["error"]!.GetValue<string>(), Does.Contain("InfoNotAvailable"));
    }

    [Test]
    public void Check_MissingIdsFile_IsAToolError()
    {
        var missing = Path.Combine(RepoPaths.Root, "tests", "mcp", "BimOpenMcp.Ifc.Tests", "ids", "no-such.ids");
        var payload = _mcp.Call("ifc_ids_check", new JsonObject { ["path"] = _bos, ["idsPath"] = missing });

        Assert.That(payload["ok"]!.GetValue<bool>(), Is.False);
        Assert.That(payload["error"]!.GetValue<string>(), Does.Contain("IDS file not found"));
    }

    [Test]
    public void Specifications_ListsDcW1WithoutAModel()
    {
        var data = _mcp.CallData("ifc_ids_specifications", new JsonObject { ["idsPath"] = DcW1 });

        var specifications = Records(data["specifications"]!);
        Assert.That(specifications, Has.Count.EqualTo(1));
        Assert.That(specifications[0]["CheckId"]!.GetValue<string>(), Is.EqualTo("DC-W1"));
        Assert.That(specifications[0]["Supported"]!.GetValue<bool>(), Is.True);
        Assert.That(data["warnings"]!.AsArray(), Is.Empty);
    }

    /// <summary>The rows of a columns-and-rows answer as objects keyed by column name.</summary>
    private static IReadOnlyList<JsonObject> Records(JsonNode table)
    {
        var columns = table["columns"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
        return table["rows"]!.AsArray()
            .Select(row => new JsonObject(columns.Select((c, i) => KeyValuePair.Create(c, row![i]?.DeepClone()))))
            .ToList();
    }
}
