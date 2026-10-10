using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Usd.Tests;

/// <summary>
/// Writes committed samples from samples/public as .usda and checks the stage three ways:
/// the summary against counts taken straight from the BOS tables, the text against the same
/// counts, and (when usd-core is installed) usd-core's own reading and validation.
/// </summary>
[TestFixture]
public sealed class SampleExportTests
{
    private const string DoorPrim = "/Model/E_1hOSvn6df7F8_7GcBWlRH8";
    private const string DescriptorsPrim = "/Model/" + UsdNames.DescriptorsScope;

    private static string OutputPath(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "bos-usd-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ".usda");
    }

    private static readonly Lazy<(BimData Data, UsdExportSummary Summary, string Path, TimeSpan Elapsed)> Duplex
        = new(() => Export("duplex"));

    private static (BimData Data, UsdExportSummary Summary, string Path, TimeSpan Elapsed) Export(string sample)
    {
        var data = new FilePath(RepoPaths.Samples("public", sample + ".bos")).ReadBimDataFromParquetZip();
        var path = OutputPath(sample);
        var clock = Stopwatch.StartNew();
        var summary = data.WriteUsda(path);
        return (data, summary, path, clock.Elapsed);
    }

    /// <summary>Elements, instances, and meshes as the BOS tables define them, counted without the exporter.</summary>
    private static (int Elements, int Instances, int Meshes, int Materials) Expected(BimData data)
    {
        var g = data.Geometry;
        var withMesh = Enumerable.Range(0, g.GetNumInstances())
            .Where(i => g.InstanceMeshIndex[i] >= 0 && g.InstanceMeshIndex[i] < g.GetNumMeshes())
            .ToList();
        var elements = withMesh.Select(i => g.InstanceEntityIndex[i])
            .Where(e => e >= 0 && e < data.Entities.Length)
            .Distinct()
            .Count();
        return (elements, withMesh.Count, g.GetNumMeshes(), g.GetNumMaterials());
    }

    /// <summary>Size and write time in the test output: the source of the numbers in README.md.</summary>
    private static void Report(string path, UsdExportSummary summary, TimeSpan elapsed)
        => TestContext.Out.WriteLine(
            $"{Path.GetFileName(path)}: {new FileInfo(path).Length:N0} bytes, written in {elapsed.TotalMilliseconds:N0} ms; {summary}");

    private static int CountLines(string path, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        return File.ReadLines(path).Count(regex.IsMatch);
    }

    [Test]
    public void Duplex_CountsMatchTheBosTables()
    {
        var (data, summary, path, elapsed) = Duplex.Value;
        var expected = Expected(data);
        Report(path, summary, elapsed);
        Assert.That(expected.Instances, Is.GreaterThan(0), "the fixture has geometry");
        Assert.Multiple(() =>
        {
            Assert.That(summary.Elements, Is.EqualTo(expected.Elements), "elements");
            Assert.That(summary.Instances, Is.EqualTo(expected.Instances), "instances");
            Assert.That(summary.Prototypes, Is.EqualTo(expected.Meshes), "prototypes");
            Assert.That(summary.Materials, Is.EqualTo(expected.Materials), "materials");
            Assert.That(CountLines(path, "^\\s*def Mesh \""), Is.EqualTo(expected.Meshes), "mesh prims in the text");
            Assert.That(CountLines(path, "^\\s*instanceable = true$"), Is.EqualTo(expected.Instances), "instanceable prims in the text");
            Assert.That(CountLines(path, "^\\s*kind = \"component\"$"), Is.EqualTo(expected.Elements), "element prims in the text");
            Assert.That(CountLines(path, "^\\s*def Material \""), Is.EqualTo(expected.Materials), "material prims in the text");
        });
    }

    [Test]
    public void Duplex_StageMetadataIsZUpMetres()
    {
        var (_, _, path, _) = Duplex.Value;
        var header = File.ReadLines(path).Take(8).ToList();
        Assert.That(header[0], Is.EqualTo("#usda 1.0"));
        Assert.That(header, Does.Contain("    upAxis = \"Z\""));
        Assert.That(header, Does.Contain("    metersPerUnit = 1"));
        Assert.That(header, Does.Contain($"    defaultPrim = \"{UsdNames.Root}\""));
    }

    [Test]
    public void Duplex_OpensInUsdCoreWithoutErrors()
    {
        var (data, summary, path, _) = Duplex.Value;
        var expected = Expected(data);
        var check = UsdCheck.Run(path, DoorPrim);

        var findings = check.GetProperty("findings").EnumerateArray().ToList();
        foreach (var f in findings)
            TestContext.Out.WriteLine($"{f.GetProperty("type")} {f.GetProperty("name")}: {f.GetProperty("message")}");
        var counts = check.GetProperty("counts");
        Assert.Multiple(() =>
        {
            Assert.That(check.GetProperty("compositionErrors").GetArrayLength(), Is.Zero, "composition errors");
            Assert.That(findings, Is.Empty, "validation findings");
            Assert.That(check.GetProperty("defaultPrim").GetString(), Is.EqualTo(UsdNames.Root));
            Assert.That(check.GetProperty("upAxis").GetString(), Is.EqualTo("Z"));
            Assert.That(check.GetProperty("metersPerUnit").GetDouble(), Is.EqualTo(1.0));
            Assert.That(counts.GetProperty("meshes").GetInt32(), Is.EqualTo(expected.Meshes), "Mesh prims");
            Assert.That(counts.GetProperty("instances").GetInt32(), Is.EqualTo(expected.Instances), "instance prims");
            Assert.That(counts.GetProperty("elements").GetInt32(), Is.EqualTo(expected.Elements), "component prims");
            Assert.That(counts.GetProperty("materials").GetInt32(), Is.EqualTo(expected.Materials), "Material prims");
            Assert.That(check.GetProperty("prototypes").GetInt32(), Is.LessThanOrEqualTo(summary.Prototypes), "USD prototypes are shared meshes");
        });
    }

    /// <summary>A Duplex door read back through usd-core: identity attributes, a number parameter
    /// whose BOS name is not an identifier, and a string parameter whose BOS group is not one;
    /// the original strings are on the attribute's declaration under /Model/Descriptors.</summary>
    [Test]
    public void Duplex_DoorKeepsItsElementData()
    {
        var (_, _, path, _) = Duplex.Value;
        var prims = UsdCheck.Run(path, DoorPrim, DescriptorsPrim).GetProperty("prims");
        var door = prims.GetProperty(DoorPrim);
        var descriptors = prims.GetProperty(DescriptorsPrim);
        Assert.That(door.ValueKind, Is.EqualTo(JsonValueKind.Object), $"{DoorPrim} exists");

        JsonElement Attr(string name) => door.GetProperty(name);
        Assert.Multiple(() =>
        {
            Assert.That(Attr("bim:globalId").GetProperty("value").GetString(), Is.EqualTo("1hOSvn6df7F8_7GcBWlRH8"));
            Assert.That(Attr("bim:category").GetProperty("value").GetString(), Is.EqualTo("IFCDOOR"));
            Assert.That(Attr("bim:name").GetProperty("value").GetString(), Does.StartWith("M_Single-Flush:1250mm x 2010mm"));

            var width = Attr("bim:param:IFCDOOR:Ifc_OverallWidth");
            Assert.That(width.GetProperty("type").GetString(), Is.EqualTo("float"));
            Assert.That(width.GetProperty("value").GetDouble(), Is.EqualTo(1.25).Within(1e-6));
            Assert.That(descriptors.GetProperty("bim:param:IFCDOOR:Ifc_OverallWidth").GetProperty("displayName").GetString(), Is.EqualTo("Ifc:OverallWidth"));

            var mark = Attr("bim:param:PSet_Revit_Identity_Data:Mark");
            Assert.That(mark.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(mark.GetProperty("value").GetString(), Is.EqualTo("B101"));
            Assert.That(descriptors.GetProperty("bim:param:PSet_Revit_Identity_Data:Mark").GetProperty("displayGroup").GetString(), Is.EqualTo("PSet_Revit_Identity Data"));
        });
    }

    /// <summary>Schependomlaan (5,978 instances, 335,475 parameters) as a size and speed probe:
    /// the numbers in README.md come from this test's output.</summary>
    [Test]
    public void Schependomlaan_WritesAndOpens()
    {
        var (data, summary, path, elapsed) = Export("schependomlaan");
        var expected = Expected(data);
        Report(path, summary, elapsed);
        Assert.Multiple(() =>
        {
            Assert.That(summary.Elements, Is.EqualTo(expected.Elements), "elements");
            Assert.That(summary.Instances, Is.EqualTo(expected.Instances), "instances");
            Assert.That(summary.Prototypes, Is.EqualTo(expected.Meshes), "prototypes");
        });

        var clock = Stopwatch.StartNew();
        var check = UsdCheck.Run(path);
        TestContext.Out.WriteLine($"usd-core open and validate: {clock.Elapsed.TotalMilliseconds:N0} ms");
        Assert.Multiple(() =>
        {
            Assert.That(check.GetProperty("compositionErrors").GetArrayLength(), Is.Zero, "composition errors");
            Assert.That(check.GetProperty("findings").GetArrayLength(), Is.Zero, "validation findings");
            Assert.That(check.GetProperty("counts").GetProperty("instances").GetInt32(), Is.EqualTo(expected.Instances));
        });
    }
}
