using Ara3D.MCP;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>TKT-145, IFC-Bench questions 387, 421, 434, and 457: the layers of a wall or roof type
/// and their thicknesses. The fixture holds both ways an exporter writes them. Wall 'W1' has an
/// IfcMaterialLayerSetUsage whose layers carry LayerThickness. Wall 'W2' is shaped like the Revit
/// reference-view export of the fantasy office models: an IfcMaterialConstituentSet named after the
/// wall type, and each layer's width as an IfcPhysicalComplexQuantity in Qto_WallBaseQuantities.</summary>
[TestFixture]
public sealed class MaterialLayerTests
{
    private static readonly string TwoLayeredWalls = MiniIfc.Document("""
        #4=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #5=IFCUNITASSIGNMENT((#4));
        #1=IFCPROJECT('0000000000000000000001',$,'P',$,$,$,$,$,#5);
        #10=IFCMATERIAL('Brick',$,'Masonry');
        #11=IFCMATERIAL('Insulation',$,'Generic');
        #12=IFCMATERIALLAYER(#10,115.,.F.,'Outer leaf',$,'LoadBearing',$);
        #13=IFCMATERIALLAYER(#11,80.,.F.,$,$,$,$);
        #14=IFCMATERIALLAYERSET((#12,#13),'Cavity 195',$);
        #15=IFCMATERIALLAYERSETUSAGE(#14,.AXIS2.,.POSITIVE.,0.,$);
        #20=IFCWALL('0000000000000000000020',$,'W1',$,$,$,$,$,.STANDARD.);
        #21=IFCRELASSOCIATESMATERIAL('0000000000000000000021',$,$,$,(#20),#15);
        #30=IFCMATERIAL('StB - Ortbeton',$,'Beton');
        #31=IFCMATERIAL('W\X2\00E4\X0\rmed\X2\00E4\X0\mmung',$,'Putz');
        #32=IFCMATERIALCONSTITUENT('StB - Ortbeton',$,#30,$,'Beton');
        #33=IFCMATERIALCONSTITUENT('W\X2\00E4\X0\rmed\X2\00E4\X0\mmung',$,#31,$,'Putz');
        #34=IFCMATERIALCONSTITUENTSET('Basiswand:STB 25.0 WD 12.0',$,(#32,#33));
        #40=IFCWALL('0000000000000000000040',$,'W2',$,$,$,$,$,.STANDARD.);
        #41=IFCRELASSOCIATESMATERIAL('0000000000000000000041',$,$,$,(#40),#34);
        #42=IFCQUANTITYLENGTH('Width',$,$,250.,$);
        #43=IFCPHYSICALCOMPLEXQUANTITY('StB - Ortbeton',$,(#42),'Layer',$,$);
        #44=IFCQUANTITYLENGTH('Width',$,$,120.,$);
        #45=IFCPHYSICALCOMPLEXQUANTITY('W\X2\00E4\X0\rmed\X2\00E4\X0\mmung',$,(#44),'Layer',$,$);
        #46=IFCQUANTITYLENGTH('Width',$,$,370.,$);
        #47=IFCELEMENTQUANTITY('0000000000000000000047',$,'Qto_WallBaseQuantities',$,$,(#43,#45,#46));
        #48=IFCRELDEFINESBYPROPERTIES('0000000000000000000048',$,$,$,(#40),#47);
        """, MiniIfc.Ifc4, "layered-walls.ifc");

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
        _path = Path.Combine(_scratch, "layered-walls.ifc");
        File.WriteAllText(_path, TwoLayeredWalls, System.Text.Encoding.ASCII);
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

    /// <summary>IfcMaterial's third attribute is its Category; it was read as the name, so every
    /// material in fantasy office 2 came back as 'Generisch'.</summary>
    [Test]
    public void Materials_AreNamedByTheirName_NotTheirCategory()
    {
        var rows = _mcp.Rows(_path, "SELECT Name FROM EntityText WHERE Category = 'IFCMATERIAL' ORDER BY Name");
        Assert.That(Cells(rows), Is.EqualTo(new[]
        {
            new[] { "Brick" }, new[] { "Insulation" }, new[] { "StB - Ortbeton" }, new[] { "Wärmedämmung" },
        }));
    }

    [Test]
    public void LayerSetUsage_GivesEachLayerItsPositionThicknessAndMaterial()
    {
        var rows = _mcp.Rows(_path, """
            SELECT max(CASE WHEN p.Name = 'Ifc:LayerSet' THEN p.Value END) AS LayerSet,
                   max(CASE WHEN p.Name = 'Ifc:LayerIndex' THEN p.Value END) AS Position,
                   l.Name AS Layer,
                   max(CASE WHEN p.Name = 'Ifc:Material' THEN p.Value END) AS Material,
                   max(CASE WHEN p.Name = 'Ifc:LayerThickness' THEN p.Value END) AS Thickness
            FROM RelationText r
            JOIN EntityText w ON w.EntityIndex = r.EntityIndexA
            JOIN EntityText l ON l.EntityIndex = r.EntityIndexB
            JOIN ParameterText p ON p.EntityIndex = l.EntityIndex
            WHERE w.Name = 'W1' AND r.RelationType = 'HasLayer'
            GROUP BY l.Name
            ORDER BY Position
            """);

        Assert.That(Cells(rows), Is.EqualTo(new[]
        {
            new[] { "Cavity 195", "1", "Outer leaf", "Brick", "115.0" },
            new[] { "Cavity 195", "2", "#13", "Insulation", "80.0" },
        }));
    }

    [Test]
    public void ConstituentSet_GivesEachConstituentItsSetAndPosition()
    {
        var rows = _mcp.Rows(_path, """
            SELECT max(CASE WHEN p.Name = 'Ifc:ConstituentSet' THEN p.Value END) AS ConstituentSet,
                   max(CASE WHEN p.Name = 'Ifc:ConstituentIndex' THEN p.Value END) AS Position,
                   c.Name,
                   max(CASE WHEN p.Name = 'Ifc:Material' THEN p.Value END) AS Material
            FROM EntityText c JOIN ParameterText p USING (EntityIndex)
            WHERE c.Category = 'IFCMATERIALCONSTITUENT'
            GROUP BY c.Name
            ORDER BY Position
            """);

        Assert.That(Cells(rows), Is.EqualTo(new[]
        {
            new[] { "Basiswand:STB 25.0 WD 12.0", "1", "StB - Ortbeton", "StB - Ortbeton" },
            new[] { "Basiswand:STB 25.0 WD 12.0", "2", "Wärmedämmung", "Wärmedämmung" },
        }));
    }

    /// <summary>A layer's width stays apart from the wall's own Width.</summary>
    [Test]
    public void ComplexQuantities_AreReadAsOneRowPerLayer()
    {
        var rows = _mcp.Rows(_path, """
            SELECT p.Name, p.Value
            FROM ParameterText p JOIN EntityText w USING (EntityIndex)
            WHERE w.Name = 'W2' AND p.ParameterGroup = 'Qto_WallBaseQuantities'
            ORDER BY p.Name
            """);

        Assert.That(Cells(rows), Is.EqualTo(new[]
        {
            new[] { "StB - Ortbeton/Width", "250.0" },
            new[] { "Width", "370.0" },
            new[] { "Wärmedämmung/Width", "120.0" },
        }));
    }

    private static string?[][] Cells(System.Text.Json.Nodes.JsonArray rows)
        => rows.Select(r => r!.AsArray().Select(c => c?.ToString()).ToArray()).ToArray();
}
