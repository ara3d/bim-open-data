using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>TKT-145, IFC-Bench questions 213 and 236: the DigitalHub Revit models attach two
/// property sets of the same name, with the same values, to every pipe and duct, so a sum of
/// 'Abmessungen.Länge' joined to StoreyOfElement came out twice the truth. The fixture repeats
/// that shape on two pipes in one storey; the repeated set must count once everywhere, and a
/// same-named property whose values differ must keep both values.</summary>
[TestFixture]
public sealed class DuplicatePropertySetTests
{
    /// <summary>The project carries a unit assignment because the geometry load the conversion
    /// runs does not return on a model without one.</summary>
    private static readonly string TwoPipesWithRepeatedSets = MiniIfc.Document("""
        #4=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #5=IFCUNITASSIGNMENT((#4));
        #1=IFCPROJECT('0000000000000000000001',$,'P',$,$,$,$,$,#5);
        #2=IFCBUILDINGSTOREY('0000000000000000000002',$,'E00',$,$,$,$,$,.ELEMENT.,0.);
        #3=IFCRELAGGREGATES('0000000000000000000003',$,$,$,#1,(#2));
        #10=IFCPIPESEGMENT('0000000000000000000010',$,'Pipe A',$,$,$,$,$,.NOTDEFINED.);
        #11=IFCPIPESEGMENT('0000000000000000000011',$,'Pipe B',$,$,$,$,$,.NOTDEFINED.);
        #12=IFCRELCONTAINEDINSPATIALSTRUCTURE('0000000000000000000012',$,$,$,(#10,#11),#2);
        #20=IFCPROPERTYSINGLEVALUE('L\X2\00E4\X0\nge',$,IFCLENGTHMEASURE(2500.),$);
        #21=IFCPROPERTYSINGLEVALUE('Versatz',$,IFCLENGTHMEASURE(10.),$);
        #22=IFCPROPERTYSET('0000000000000000000022',$,'Abmessungen',$,(#20,#21));
        #23=IFCRELDEFINESBYPROPERTIES('0000000000000000000023',$,$,$,(#10),#22);
        #24=IFCPROPERTYSINGLEVALUE('L\X2\00E4\X0\nge',$,IFCLENGTHMEASURE(2500.),$);
        #25=IFCPROPERTYSINGLEVALUE('Versatz',$,IFCLENGTHMEASURE(20.),$);
        #26=IFCPROPERTYSET('0000000000000000000026',$,'Abmessungen',$,(#24,#25));
        #27=IFCRELDEFINESBYPROPERTIES('0000000000000000000027',$,$,$,(#10),#26);
        #30=IFCPROPERTYSINGLEVALUE('L\X2\00E4\X0\nge',$,IFCLENGTHMEASURE(1500.),$);
        #31=IFCPROPERTYSET('0000000000000000000031',$,'Abmessungen',$,(#30));
        #32=IFCRELDEFINESBYPROPERTIES('0000000000000000000032',$,$,$,(#11),#31);
        #33=IFCPROPERTYSET('0000000000000000000033',$,'Abmessungen',$,(#30));
        #34=IFCRELDEFINESBYPROPERTIES('0000000000000000000034',$,$,$,(#11),#33);
        """, MiniIfc.Ifc4, "repeated-property-sets.ifc");

    private IfcSessionCache _cache = null!;
    private McpServer _mcp = null!;
    private string _scratch = null!;
    private string _path = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _cache = new IfcSessionCache();
        _mcp = IfcMcpServer.Create(_cache, McpTransport.Stdio);
        _scratch = Path.Combine(Path.GetTempPath(), "bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
        _path = Path.Combine(_scratch, "repeated-property-sets.ifc");
        File.WriteAllText(_path, TwoPipesWithRepeatedSets, System.Text.Encoding.ASCII);
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

    /// <summary>The query the agent wrote for question 213, reduced to the fixture.</summary>
    [Test]
    public void LengthSummedThroughStoreyOfElement_CountsEachPipeOnce()
    {
        var rows = _mcp.Rows(_path, """
            SELECT s.StoreyName, count(*) AS n, sum(CAST(p.Value AS DOUBLE)) AS total
            FROM ParameterText p JOIN StoreyOfElement s USING (EntityIndex)
            WHERE p.ParameterGroup = 'Abmessungen' AND p.Name = 'Länge'
            GROUP BY s.StoreyName
            """);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]![0]!.GetValue<string>(), Is.EqualTo("E00"));
        Assert.That(rows[0]![1]!.GetValue<long>(), Is.EqualTo(2));
        Assert.That(rows[0]![2]!.GetValue<double>(), Is.EqualTo(4000.0));
    }

    [Test]
    public void ParameterText_HasOneRowPerEntityNameAndValue()
    {
        var rows = _mcp.Rows(_path,
            "SELECT count(*) FROM (SELECT EntityIndex, ParameterGroup, Name, Value, count(*) AS c FROM ParameterText GROUP BY ALL) WHERE c > 1");
        Assert.That(rows[0]![0]!.GetValue<long>(), Is.EqualTo(0));
    }

    /// <summary>Pipe A's two sets disagree on Versatz; neither value is dropped.</summary>
    [Test]
    public void DifferingValuesOfOneProperty_AreBothKept()
    {
        var rows = _mcp.Rows(_path,
            "SELECT Value FROM ParameterText WHERE Name = 'Versatz' ORDER BY CAST(Value AS DOUBLE)");
        Assert.That(rows.Select(r => r![0]!.GetValue<string>()), Is.EqualTo(new[] { "10.0", "20.0" }));
    }

    [Test]
    public void FindByParameter_ListsEachPipeOnce()
    {
        var data = _mcp.CallData("ifc_find_by_parameter", new JsonObject
        {
            ["path"] = _path,
            ["name"] = "Abmessungen.Länge",
        });
        Assert.That(data["matches"]!["total"]!.GetValue<int>(), Is.EqualTo(2), data.ToJsonString());
    }

    [Test]
    public void Properties_ListTheRepeatedSetOnce()
    {
        var data = _mcp.CallData("ifc_properties", new JsonObject { ["path"] = _path, ["id"] = 11 });
        Assert.That(data["properties"]!["total"]!.GetValue<int>(), Is.EqualTo(1), data.ToJsonString());
    }

    /// <summary>A list() column was reported as "System.Collections.Generic.List`1[...]".</summary>
    [Test]
    public void ListColumns_ComeBackAsJsonArrays()
    {
        var rows = _mcp.Rows(_path, "SELECT list(Value ORDER BY Value) FROM ParameterText WHERE Name = 'Versatz'");
        Assert.That(rows[0]![0]!.ToJsonString(), Is.EqualTo("[\"10.0\",\"20.0\"]"));
    }
}
