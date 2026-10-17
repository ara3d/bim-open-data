using System.Text.RegularExpressions;
using Ara3D.MCP;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>TKT-49: SpaceOfElement maps an element to the IfcSpace it sits in, and ZoneOfSpace maps
/// a space to its IfcZone groups. The first fixture is small enough to state every row; the second
/// is the Duplex, where some elements sit in rooms and some in storeys.</summary>
[TestFixture]
public sealed partial class SpaceZoneViewTests
{
    /// <summary>One storey with spaces S1 and S2. Wall In Space sits in S1, Wall In Storey in the
    /// storey, Stair in S2 with Flight aggregated under it. Zone Z1 groups S1 and S2, zone Z2 groups
    /// S1 only (a space in two zones). The project carries a unit assignment because the geometry
    /// load the conversion runs does not return on a model without one.</summary>
    private static readonly string TwoSpacesTwoZones = MiniIfc.Document("""
        #4=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #5=IFCUNITASSIGNMENT((#4));
        #1=IFCPROJECT('0000000000000000000001',$,'P',$,$,$,$,$,#5);
        #2=IFCBUILDINGSTOREY('0000000000000000000002',$,'E00',$,$,$,$,$,.ELEMENT.,0.);
        #3=IFCRELAGGREGATES('0000000000000000000003',$,$,$,#1,(#2));
        #10=IFCSPACE('0000000000000000000010',$,'S1',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        #11=IFCSPACE('0000000000000000000011',$,'S2',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        #12=IFCRELAGGREGATES('0000000000000000000012',$,$,$,#2,(#10,#11));
        #20=IFCWALL('0000000000000000000020',$,'Wall In Space',$,$,$,$,$,.NOTDEFINED.);
        #21=IFCWALL('0000000000000000000021',$,'Wall In Storey',$,$,$,$,$,.NOTDEFINED.);
        #22=IFCSTAIR('0000000000000000000022',$,'Stair',$,$,$,$,$,.NOTDEFINED.);
        #23=IFCSTAIRFLIGHT('0000000000000000000023',$,'Flight',$,$,$,$,$,$,$,$,$,.NOTDEFINED.);
        #24=IFCRELAGGREGATES('0000000000000000000024',$,$,$,#22,(#23));
        #25=IFCRELCONTAINEDINSPATIALSTRUCTURE('0000000000000000000025',$,$,$,(#20),#10);
        #26=IFCRELCONTAINEDINSPATIALSTRUCTURE('0000000000000000000026',$,$,$,(#22),#11);
        #27=IFCRELCONTAINEDINSPATIALSTRUCTURE('0000000000000000000027',$,$,$,(#21),#2);
        #30=IFCZONE('0000000000000000000030',$,'Z1',$,$,$);
        #31=IFCZONE('0000000000000000000031',$,'Z2',$,$,$);
        #32=IFCRELASSIGNSTOGROUP('0000000000000000000032',$,$,$,(#10,#11),$,#30);
        #33=IFCRELASSIGNSTOGROUP('0000000000000000000033',$,$,$,(#10),$,#31);
        """, MiniIfc.Ifc4, "space-zone-views.ifc");

    private IfcSessionCache _cache = null!;
    private McpServer _mcp = null!;
    private string _scratch = null!;
    private string _miniPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _cache = new IfcSessionCache();
        _mcp = IfcMcpServer.Create(_cache, McpTransport.Stdio);
        _scratch = Path.Combine(Path.GetTempPath(), "bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
        _miniPath = Path.Combine(_scratch, "space-zone-views.ifc");
        File.WriteAllText(_miniPath, TwoSpacesTwoZones, System.Text.Encoding.ASCII);
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

    private List<string> Pairs(string path, string sql)
        => _mcp.Rows(path, sql).Select(r => string.Join("|", r!.AsArray().Select(c => c!.ToString()))).ToList();

    [Test]
    public void SpaceOfElement_ReachesElementsInRooms_AndLeavesTheRestOut()
    {
        var pairs = Pairs(_miniPath, """
            SELECT e.Name, s.SpaceName, s.Depth
            FROM SpaceOfElement s JOIN EntityText e USING (EntityIndex)
            ORDER BY e.Name
            """);

        // The stair flight reaches S2 through its stair (Depth 2); Wall In Storey has no room.
        Assert.That(pairs, Is.EqualTo(new[] { "Flight|S2|2", "Stair|S2|1", "Wall In Space|S1|1" }));
    }

    [Test]
    public void ZoneOfSpace_ListsEachZoneOfEachSpace_AndNoStorey()
    {
        var pairs = Pairs(_miniPath, "SELECT SpaceName, ZoneName FROM ZoneOfSpace ORDER BY SpaceName, ZoneName");

        Assert.That(pairs, Is.EqualTo(new[] { "S1|Z1", "S1|Z2", "S2|Z1" }));
    }

    [Test]
    public void ZoneOfSpace_IsEmptyOnAModelWithoutZones()
    {
        var rows = _mcp.Rows(DuplexBase, "SELECT count(*) FROM ZoneOfSpace");

        Assert.That(rows[0]![0]!.GetValue<long>(), Is.EqualTo(0));
    }

    [Test]
    public void SpaceOfElement_OnTheDuplex_ReachesEveryElementTheModelContainsInARoom()
    {
        var text = File.ReadAllText(DuplexBase, System.Text.Encoding.Latin1);
        var spaceIds = SpaceLine().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
        Assert.That(spaceIds, Has.Count.EqualTo(21));
        var inRooms = new HashSet<string>();
        foreach (Match m in ContainedLine().Matches(text))
            if (spaceIds.Contains(m.Groups[2].Value))
                foreach (var id in m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    inRooms.Add(id.TrimStart('#'));

        var reached = _mcp.Rows(DuplexBase, """
            SELECT DISTINCT e.StepId FROM SpaceOfElement s JOIN EntityText e USING (EntityIndex) ORDER BY e.StepId
            """).Select(r => r![0]!.GetValue<long>().ToString()).ToHashSet();

        Assert.That(inRooms, Is.Not.Empty);
        Assert.That(reached, Is.SupersetOf(inRooms));
        Assert.That(reached.Count, Is.EqualTo(ExpectedReached), "elements the Duplex conversion places in a room");
        var self = _mcp.Rows(DuplexBase, "SELECT count(*) FROM SpaceOfElement WHERE EntityIndex = SpaceIndex");
        Assert.That(self[0]![0]!.GetValue<long>(), Is.EqualTo(0));
    }

    /// <summary>The 61 furnishing elements; every other Duplex element sits in a storey, not a room.</summary>
    private const int ExpectedReached = 61;

    private static string DuplexBase => RepoPaths.Samples("nrc", "duplex-base.ifc");

    [GeneratedRegex(@"^#(\d+)=IFCSPACE\(", RegexOptions.Multiline)]
    private static partial Regex SpaceLine();

    [GeneratedRegex(@"^#\d+=IFCRELCONTAINEDINSPATIALSTRUCTURE\('[^']*',#\d+,\$,\$,\(([^)]*)\),#(\d+)\)", RegexOptions.Multiline)]
    private static partial Regex ContainedLine();
}
