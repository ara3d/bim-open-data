using System.IO.Compression;
using System.Text.Json.Nodes;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.BimOpenSchema.IO.Fragments;
using Ara3D.MCP;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>Drives the BOS file tools through tools/call against the committed Duplex sample and
/// its Fragments fixture. The format libraries test their own output in depth; these tests check
/// the tool wiring: arguments reach the library, a file appears, and the counts come back.</summary>
[TestFixture]
public sealed class BosFileToolTests
{
    private IfcSessionCache _cache = null!;
    private McpServer _mcp = null!;
    private string _bos = null!;
    private string _scratch = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _bos = RepoPaths.Samples("public", "duplex.bos");
        _cache = new IfcSessionCache();
        _mcp = IfcMcpServer.Create(_cache, McpTransport.Stdio);
        _scratch = RepoPaths.Artifacts("bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _mcp.Dispose();
        _cache.Dispose();
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void ToolsList_CoversTheBosFileSurface()
    {
        var result = _mcp.HandlePost("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");
        var names = JsonNode.Parse(result.JsonBody!)!["result"]!["tools"]!
            .AsArray()
            .Select(tool => tool!["name"]!.GetValue<string>())
            .ToList();

        Assert.That(names, Is.SupersetOf(new[] { "frag_to_bos", "bos_table", "bos_sql", "bos_export_glb", "bos_export_usd", "bos_export_bcf" }));
    }

    [Test]
    public void FragToBos_WritesBosAndDatabase()
    {
        var frag = Path.Combine(RepoPaths.Root, "tests", "data", "Ara3D.BimOpenSchema.IO.Fragments.Tests", "Fixtures", "duplex.frag");
        var output = Path.Combine(_scratch, "duplex-from-frag.bos");
        var data = _mcp.CallData("frag_to_bos", new JsonObject { ["path"] = frag, ["outputPath"] = output });

        Assert.That(data["bosPath"]!.GetValue<string>(), Is.EqualTo(output));
        Assert.That(File.Exists(data["databasePath"]!.GetValue<string>()), Is.True);
        Assert.That(data["entities"]!.GetValue<int>(), Is.GreaterThan(0));
        Assert.That(data["instances"]!.GetValue<int>(), Is.GreaterThan(0));
        var saved = new FilePath(output).ReadBimDataFromParquetZip().Geometry;
        Assert.That(saved.InstanceEntityIndex, Has.Length.EqualTo(data["instances"]!.GetValue<int>()));
        Assert.That(saved.IndexBuffer, Has.Length.EqualTo(FragmentsToBos.Read(frag).Geometry.IndexBuffer.Length));
        var doors = _mcp.CallData("bos_sql", new JsonObject
        {
            ["bosPath"] = output,
            ["sql"] = "SELECT count(*) AS n FROM EntityText WHERE Category = 'IFCDOOR'",
        });
        Assert.That(System.Convert.ToInt32(doors["rows"]![0]![0]!.ToString()), Is.EqualTo(14));
        Assert.That(_cache.BosSessions.IsOpen(output), Is.True, "frag_to_bos holds the model it read as a session");
    }

    [Test]
    public void Table_ListsTheViewsAndMetricCatalog()
    {
        var items = _mcp.CallData("bos_table", new JsonObject { ["bosPath"] = _bos, ["take"] = 100 })["items"]!.AsArray();
        var names = items.Select(item => item!["table"]!.GetValue<string>()).ToList();

        Assert.That(names, Is.SupersetOf(new[] { "Entities", "EntityText", "ParameterText", "StoreyOfElement", "MetricCatalog" }));
        var entities = _mcp.CallData("bos_table", new JsonObject { ["bosPath"] = _bos, ["table"] = "Entities" })["items"]![0]!;
        Assert.That(entities["rowCount"]!.GetValue<long>(), Is.EqualTo(_cache.BosSessions.Get(_bos).Data.Entities.Length));
    }

    [Test]
    public void IfcConversion_IsTheSessionTheBosToolsGetForItsBosPath()
    {
        var ifc = TestModel.RequirePath(TestModel.FzkHaus);
        var bosPath = _mcp.CallData("ifc_to_bos", new JsonObject { ["path"] = ifc })["bosPath"]!.GetValue<string>();

        Assert.That(_cache.BosSessions.IsOpen(bosPath), Is.True);
        Assert.That(_cache.BosSessions.Get(bosPath), Is.SameAs(_cache.Get(ifc).Bos));
        const string count = "SELECT count(*) FROM EntityText";
        var viaBos = _mcp.CallData("bos_sql", new JsonObject { ["bosPath"] = bosPath, ["sql"] = count })["rows"]![0]![0]!.ToString();
        Assert.That(viaBos, Is.EqualTo(_mcp.Rows(ifc, count)[0]![0]!.ToString()));
    }

    [Test]
    public void Sql_QueriesTheTextViewsOfACommittedSample()
    {
        var data = _mcp.CallData("bos_sql", new JsonObject
        {
            ["bosPath"] = _bos,
            ["sql"] = "SELECT GlobalId FROM EntityText WHERE Category = 'IFCDOOR'",
            ["take"] = 5,
        });

        Assert.That(data["total"]!.GetValue<long>(), Is.EqualTo(14));
        Assert.That(data["rows"]!.AsArray(), Has.Count.EqualTo(5));
    }

    [Test]
    public void FragToBos_NamesTheProblemWithAFileThatIsNotFragments()
    {
        var notFrag = Path.Combine(_scratch, "not.frag");
        File.WriteAllBytes(notFrag, [1, 2, 3, 4, 5, 6, 7, 8]);
        var payload = _mcp.Call("frag_to_bos", new JsonObject { ["path"] = notFrag });

        Assert.That(payload["ok"]!.GetValue<bool>(), Is.False);
        Assert.That(payload["error"]!.GetValue<string>(), Does.Contain("Fragments").IgnoreCase);
    }

    [Test]
    public void ExportGlb_LimitsToTheGivenEntities()
    {
        var whole = _mcp.CallData("bos_export_glb", new JsonObject { ["bosPath"] = _bos, ["outputPath"] = Path.Combine(_scratch, "all.glb") });
        var some = _mcp.CallData("bos_export_glb", new JsonObject
        {
            ["bosPath"] = _bos,
            ["outputPath"] = Path.Combine(_scratch, "some.glb"),
            ["entityIndices"] = string.Join(',', DoorEntityIndices()),
        });

        Assert.That(whole["summary"]!["nodes"]!.GetValue<int>(), Is.EqualTo(660));
        Assert.That(some["summary"]!["nodes"]!.GetValue<int>(), Is.InRange(1, 659), "the 14 doors have fewer instances than the whole model");
        Assert.That(some["summary"]!["unmatchedEntityIndices"]!.GetValue<int>(), Is.Zero);
    }

    [Test]
    public void ExportGlb_FailsWhenNoGivenIndexDrawsAnything()
    {
        var output = Path.Combine(_scratch, "none.glb");
        var payload = _mcp.Call("bos_export_glb", new JsonObject
        {
            ["bosPath"] = _bos,
            ["outputPath"] = output,
            ["entityIndices"] = "900000,900001",
        });

        Assert.That(payload["ok"]!.GetValue<bool>(), Is.False);
        Assert.That(payload["error"]!.GetValue<string>(), Does.Contain("not STEP ids"));
        Assert.That(File.Exists(output), Is.False);
    }

    [Test]
    public void ExportUsd_WritesAStage()
    {
        var output = Path.Combine(_scratch, "duplex.usda");
        var data = _mcp.CallData("bos_export_usd", new JsonObject { ["bosPath"] = _bos, ["outputPath"] = output });

        Assert.That(data["bytes"]!.GetValue<long>(), Is.GreaterThan(0));
        Assert.That(File.ReadLines(output).First(), Does.StartWith("#usda 1.0"));
    }

    [Test]
    public void ExportBcf_MakesOneTopicPerTitle()
    {
        var output = Path.Combine(_scratch, "doors.bcf");
        var data = _mcp.CallData("bos_export_bcf", new JsonObject
        {
            ["bosPath"] = _bos,
            ["outputPath"] = output,
            ["sql"] = "SELECT GlobalId, Name AS Title FROM EntityText WHERE Category = 'IFCDOOR'",
            ["guidSeed"] = "doors",
        });

        var topics = data["summary"]!["topics"]!.GetValue<int>();
        Assert.That(topics, Is.GreaterThan(0));
        Assert.That(data["summary"]!["elements"]!.GetValue<int>(), Is.EqualTo(14));
        using var zip = ZipFile.OpenRead(output);
        Assert.That(zip.Entries.Count(e => e.FullName.EndsWith("/markup.bcf", StringComparison.Ordinal)), Is.EqualTo(topics));
    }

    /// <summary>Entity indices of the Duplex doors, from the same EntityText view the tools query.</summary>
    private IEnumerable<int> DoorEntityIndices()
    {
        using var conn = ParquetUtils.ReadBimDataFromParquetZip(new FilePath(_bos)).ToDuckDb();
        var table = conn.Query("SELECT EntityIndex FROM EntityText WHERE Category = 'IFCDOOR'");
        return Enumerable.Range(0, table.Rows.Count).Select(row => System.Convert.ToInt32(table[0, row])).ToList();
    }
}
