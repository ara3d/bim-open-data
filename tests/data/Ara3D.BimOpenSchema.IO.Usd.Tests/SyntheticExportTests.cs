using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ara3D.BimOpenSchema.IO.Usd.Tests;

/// <summary>
/// A hand-built BOS model small enough to read whole, with the awkward cases the samples
/// lack: missing values, a duplicate parameter, an instance with no entity, one with no mesh,
/// a hidden one, a transformed one, and strings that need escaping.
/// </summary>
[TestFixture]
public sealed class SyntheticExportTests
{
    // Strings. Index -1 (missing) is used as well as "" (stored empty).
    private const string Tricky = "Wall \"A\"\\1\nline two\ttab\u0001 é";
    private static readonly string[] Strings =
        ["", "gid$1", Tricky, "IFCWALL", "Pset Common", "Width", "Note", "Count", "Origin", "Level 1", "IFCBUILDINGSTOREY"];

    private const int Category = 0, Wall = 1, Bare = 2, Storey = 3, StoreyCategory = 4;
    private const int Width = 0, Note = 1, Count = 2, Origin = 3, OnLevel = 4;

    private static BimData Model() => new()
    {
        Strings = Strings,
        Numbers = [2.5f],
        Points = [new Point(1, 2, 3)],
        Entities =
        [
            new(10, (StringIndex)0, (DocumentIndex)(-1), (StringIndex)3, (EntityIndex)(-1), (EntityIndex)(-1)),
            new(11, (StringIndex)1, (DocumentIndex)(-1), (StringIndex)2, (EntityIndex)Category, (EntityIndex)(-1)),
            new(12, (StringIndex)(-1), (DocumentIndex)(-1), (StringIndex)0, (EntityIndex)(-1), (EntityIndex)(-1)),
            new(13, (StringIndex)0, (DocumentIndex)(-1), (StringIndex)9, (EntityIndex)StoreyCategory, (EntityIndex)(-1)),
            new(14, (StringIndex)0, (DocumentIndex)(-1), (StringIndex)10, (EntityIndex)(-1), (EntityIndex)(-1)),
        ],
        Descriptors =
        [
            new((StringIndex)5, (StringIndex)0, (StringIndex)4, ParameterType.Number),
            new((StringIndex)6, (StringIndex)0, (StringIndex)4, ParameterType.String),
            new((StringIndex)7, (StringIndex)0, (StringIndex)4, ParameterType.Int),
            new((StringIndex)8, (StringIndex)0, (StringIndex)4, ParameterType.Point),
            new((StringIndex)9, (StringIndex)0, (StringIndex)4, ParameterType.Entity),
        ],
        Parameters =
        [
            new((EntityIndex)Wall, (DescriptorIndex)Width, 0),
            new((EntityIndex)Wall, (DescriptorIndex)Width, 0),     // duplicate: left out
            new((EntityIndex)Wall, (DescriptorIndex)Origin, 0),
            new((EntityIndex)Wall, (DescriptorIndex)OnLevel, Storey),
            new((EntityIndex)Wall, (DescriptorIndex)Note, 0),      // stored empty: kept as ""
            new((EntityIndex)Bare, (DescriptorIndex)Width, -1),    // missing number
            new((EntityIndex)Bare, (DescriptorIndex)Note, 99),     // string index past the end
            new((EntityIndex)Bare, (DescriptorIndex)Count, 7),
        ],
        Geometry = new BimGeometry
        {
            VertexX = [0, 10_000, 0],
            VertexY = [0, 0, 5_000],
            VertexZ = [0, 0, -1],
            IndexBuffer = [0, 1, 2],
            MeshVertexOffset = [0],
            MeshIndexOffset = [0],
            MaterialRed = [255], MaterialGreen = [128], MaterialBlue = [0], MaterialAlpha = [255],
            MaterialRoughness = [128], MaterialMetallic = [0],
            TransformTX = [0, 1], TransformTY = [0, 2], TransformTZ = [0, 3],
            TransformQX = [0, 0], TransformQY = [0, 0], TransformQZ = [0, 0.70710677f], TransformQW = [1, 0.70710677f],
            TransformSX = [1, 1], TransformSY = [1, 1], TransformSZ = [1, -1],
            // wall, wall (transformed), bare (hidden, no material), no entity, no mesh
            InstanceEntityIndex = [Wall, Wall, Bare, -1, Wall],
            InstanceMeshIndex = [0, 0, 0, 0, 5],
            InstanceMaterialIndex = [0, 0, -1, 0, 0],
            InstanceTransformIndex = [0, 1, 0, 0, 0],
            InstanceFlags = [0, 0, 1, 0, 0],
        },
    };

    private static (UsdExportSummary Summary, string Text, string Path) Export()
    {
        var path = Path.Combine(Path.GetTempPath(), "bos-usd-tests", "synthetic.usda");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var summary = Model().WriteUsda(path);
        return (summary, File.ReadAllText(path), path);
    }

    [Test]
    public void Summary_CountsEveryCase()
    {
        var (summary, _, _) = Export();
        Assert.That(summary, Is.EqualTo(new UsdExportSummary(
            Materials: 1, Prototypes: 1, Descriptors: 5, Elements: 2, Instances: 4, UnassignedInstances: 1, InstancesWithoutMesh: 1,
            // wall: index, local id, GlobalId, name, category, four parameters; bare: index, local id, one parameter
            Attributes: 9 + 3, ParametersWithoutValue: 2, DuplicateParameters: 1)));
    }

    [Test]
    public void MissingValues_AreLeftOut()
    {
        var (_, text, _) = Export();
        var bare = Prim(text, "E2");
        Assert.Multiple(() =>
        {
            Assert.That(bare, Does.Not.Contain("bim:globalId"), "no GlobalId");
            Assert.That(bare, Does.Not.Contain("bim:name"), "empty name");
            Assert.That(bare, Does.Not.Contain("bim:category"), "no category");
            Assert.That(bare, Does.Not.Contain("Width"), "missing number");
            Assert.That(bare, Does.Not.Contain("Note"), "string index past the end");
            Assert.That(bare, Does.Contain("custom int bim:param:Pset_Common:Count = 7"));
        });
    }

    [Test]
    public void Wall_KeepsTypedParameters()
    {
        var (_, text, _) = Export();
        var wall = Prim(text, "E_gid_1");
        Assert.Multiple(() =>
        {
            Assert.That(wall, Does.Contain("custom string bim:globalId = \"gid$1\""));
            Assert.That(wall, Does.Contain("custom string bim:category = \"IFCWALL\""));
            Assert.That(wall, Does.Contain("custom float bim:param:Pset_Common:Width = 2.5\n"));
            Assert.That(wall, Does.Not.Contain("displayGroup"), "descriptor strings are written once, under Descriptors");
            Assert.That(wall, Does.Contain("custom point3f bim:param:Pset_Common:Origin = (1, 2, 3)"));
            Assert.That(wall, Does.Contain("custom string bim:param:Pset_Common:Level_1 = \"Level 1\""));
            Assert.That(wall, Does.Contain("custom string bim:param:Pset_Common:Note = \"\""));
            Assert.That(wall.Split("bim:param:Pset_Common:Width =").Length - 1, Is.EqualTo(1), "duplicate dropped");
        });
    }

    [Test]
    public void Descriptors_DeclareEachParameterOnceWithItsBosStrings()
    {
        var (_, text, _) = Export();
        var descriptors = Prim(text, UsdNames.DescriptorsScope);
        Assert.Multiple(() =>
        {
            Assert.That(descriptors, Does.Contain("custom float bim:param:Pset_Common:Width (\n"));
            Assert.That(descriptors, Does.Contain("displayGroup = \"Pset Common\""));
            Assert.That(descriptors, Does.Contain("displayName = \"Level 1\""));
            Assert.That(descriptors, Does.Contain("string bosType = \"Entity\""));
            Assert.That(descriptors, Does.Not.Contain("units"), "empty units are left out");
            Assert.That(descriptors, Does.Not.Contain(" = 2.5"), "declarations carry no values");
        });
    }

    [Test]
    public void Instances_CarryTransformMaterialAndVisibility()
    {
        var (_, text, _) = Export();
        Assert.Multiple(() =>
        {
            Assert.That(Prim(text, "I0"), Does.Not.Contain("xformOp"), "identity transform writes no ops");
            Assert.That(Prim(text, "I1"), Does.Contain("float3 xformOp:translate = (1, 2, 3)"));
            Assert.That(Prim(text, "I1"), Does.Contain("quatf xformOp:orient = (0.70710677, 0, 0, 0.70710677)"));
            Assert.That(Prim(text, "I1"), Does.Contain("uniform token[] xformOpOrder = [\"xformOp:translate\", \"xformOp:orient\", \"xformOp:scale\"]"));
            Assert.That(Prim(text, "I1"), Does.Contain("rel material:binding = </Model/Materials/M0>"));
            Assert.That(Prim(text, "I2"), Does.Contain("token visibility = \"invisible\""));
            Assert.That(Prim(text, "I2"), Does.Not.Contain("material:binding"), "no material, no binding");
            Assert.That(Prim(text, "Unassigned"), Does.Contain("def Xform \"I3\""));
            Assert.That(text, Does.Not.Contain("\"I4\""), "an instance without a mesh is not written");
            Assert.That(text, Does.Contain("point3f[] points = [(0, 0, 0), (1, 0, 0), (0, 0.5, -0.0001)]"), "vertices in metres, exact");
        });
    }

    [Test]
    public void UsdCore_ReadsWhatWasWritten()
    {
        var (_, _, path) = Export();
        var check = UsdCheck.Run(path, "/Model/E_gid_1", "/Model/E2", "/Model/Descriptors");
        var counts = check.GetProperty("counts");
        var wall = check.GetProperty("prims").GetProperty("/Model/E_gid_1");
        Assert.Multiple(() =>
        {
            Assert.That(check.GetProperty("compositionErrors").GetArrayLength(), Is.Zero);
            Assert.That(check.GetProperty("findings").GetArrayLength(), Is.Zero, check.GetProperty("findings").ToString());
            Assert.That(counts.GetProperty("instances").GetInt32(), Is.EqualTo(4));
            Assert.That(counts.GetProperty("elements").GetInt32(), Is.EqualTo(2));
            Assert.That(counts.GetProperty("invisible").GetInt32(), Is.EqualTo(1));
            Assert.That(wall.GetProperty("bim:name").GetProperty("value").GetString(), Is.EqualTo(Tricky), "escaped string reads back exactly");
            Assert.That(wall.GetProperty("bim:localId").GetProperty("type").GetString(), Is.EqualTo("int64"));
            Assert.That(wall.GetProperty("bim:param:Pset_Common:Origin").GetProperty("type").GetString(), Is.EqualTo("point3f"));
            Assert.That(wall.GetProperty("bim:param:Pset_Common:Width").GetProperty("value").GetDouble(), Is.EqualTo(2.5));
            Assert.That(check.GetProperty("prims").GetProperty("/Model/E2").TryGetProperty("bim:globalId", out _), Is.False);
            var width = check.GetProperty("prims").GetProperty("/Model/Descriptors").GetProperty("bim:param:Pset_Common:Width");
            Assert.That(width.GetProperty("displayGroup").GetString(), Is.EqualTo("Pset Common"));
            Assert.That(width.GetProperty("value").ValueKind, Is.EqualTo(JsonValueKind.Null), "a declaration has no value");
        });
    }

    /// <summary>The text of the prim named <paramref name="name"/>, from its def line to the
    /// matching close brace.</summary>
    private static string Prim(string text, string name)
    {
        var match = Regex.Match(text, $"def \\w+ \"{Regex.Escape(name)}\"");
        Assert.That(match.Success, Is.True, $"prim {name} written");
        var start = match.Index;
        var open = text.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[start..(i + 1)];
        }
        return text[start..];
    }
}
