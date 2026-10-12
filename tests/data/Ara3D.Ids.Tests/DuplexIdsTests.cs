using System.Diagnostics;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.Ids.Tests;

/// <summary>The committed IDS files over the public Duplex (<c>samples/public/duplex.bos</c>):
/// rule DC-W1 as an IDS, and the NRC analytics specification, which the unenriched Duplex fails
/// everywhere. The parity test runs IfcOpenShell's ifctester over the source IFC when Python
/// and ifctester are installed, and skips otherwise; its last recorded run is in README.md.</summary>
[TestFixture]
public sealed class DuplexIdsTests
{
    private static readonly string IdsDir = Path.Combine(AppContext.BaseDirectory, "ids");
    private static readonly Lazy<IdsModel> Duplex = new(() =>
        IdsModel.FromBimData(new FilePath(RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip()));

    [Test]
    public void DcW1_EightDoorsPassAndSixFail()
    {
        var report = IdsEvaluator.Evaluate(IdsFile.Load(Path.Combine(IdsDir, "dc-w1.ids")), Duplex.Value);
        Assert.That(report.Warnings, Is.Empty);
        Assert.That(report.Verdicts, Has.Count.EqualTo(14), "one row per IFCDOOR");
        Assert.That(report.Verdicts.Count(v => v.Verdict == Verdict.Pass), Is.EqualTo(8));
        Assert.That(report.Verdicts.Count(v => v.Verdict == Verdict.Fail), Is.EqualTo(6));
        Assert.That(report.Verdicts.Select(v => v.CheckId), Has.All.EqualTo("DC-W1"));
        Assert.That(report.Verdicts.Select(v => v.GlobalId), Is.Unique);
    }

    /// <summary>Every Duplex door is of an IFCDOORSTYLE, and the styles carry no property values
    /// (their only HasPropertySets member is an empty lining), so a door inherits nothing.</summary>
    [Test]
    public void DoorStyles_AreFoundAndAddNothing()
    {
        var doors = Duplex.Value.Entities.Where(e => e.Category == "IFCDOOR").ToList();
        Assert.That(doors, Has.Count.EqualTo(14));
        Assert.That(doors.Select(d => d.Type?.Category), Has.All.EqualTo("IFCDOORSTYLE"));
        Assert.That(doors.Select(d => d.Properties.Count() == d.OwnProperties.Count()), Has.All.True);
    }

    [Test]
    public void NrcAnalytics_TheUnenrichedDuplexFailsEveryElement()
    {
        var report = IdsEvaluator.Evaluate(IdsFile.Load(Path.Combine(IdsDir, "nrc-analytics.ids")), Duplex.Value);
        Assert.That(report.Verdicts.Select(v => v.CheckId).Distinct(), Is.EquivalentTo(new[] { "IDS-OC", "IDS-EC" }));
        Assert.That(report.Verdicts, Has.Count.GreaterThan(100));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.Fail));
        Assert.That(report.Verdicts.Select(v => v.Reason), Has.All.Contain("no Pset_NRC"));
    }

    [Test]
    public void PartOf_EveryDoorIsContainedInAStorey()
    {
        var report = IdsEvaluator.Evaluate(IdsFile.Parse(IdsXml.Document(IdsXml.Specification("DOOR-STOREY", IdsXml.Entity("IFCDOOR"),
            IdsXml.PartOf("IFCBUILDINGSTOREY", "IFCRELCONTAINEDINSPATIALSTRUCTURE")))), Duplex.Value);
        Assert.That(report.Warnings, Is.Empty);
        Assert.That(report.Verdicts, Has.Count.EqualTo(14));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.Pass));
    }

    /// <summary>56 standard-case walls, each with a layer set; 15 have a concrete block layer. The
    /// Duplex's doors have no material association at all.</summary>
    [Test]
    public void Material_ConcreteBlockWalls()
    {
        var ids = IdsFile.Parse(IdsXml.Document(
            IdsXml.Specification("CMU", IdsXml.Entity("IFCWALLSTANDARDCASE"), IdsXml.Material("Masonry - Concrete Block")),
            IdsXml.Specification("DOOR-MAT", IdsXml.Entity("IFCDOOR"), IdsXml.MaterialOf(IdsXml.Pattern(".*")))));
        var report = IdsEvaluator.Evaluate(ids, Duplex.Value);
        Assert.That(report.Warnings, Is.Empty);
        var walls = report.Verdicts.Where(v => v.CheckId == "CMU").ToList();
        Assert.That(walls, Has.Count.EqualTo(56));
        Assert.That(walls.Count(v => v.Verdict == Verdict.Pass), Is.EqualTo(15));
        Assert.That(walls.Count(v => v.Verdict == Verdict.Fail), Is.EqualTo(41));
        Assert.That(walls.First(v => v.Verdict == Verdict.Fail).Reason, Does.Contain("Basic Wall:"), "the layer set's name is among those read");
        var doors = report.Verdicts.Where(v => v.CheckId == "DOOR-MAT").ToList();
        Assert.That(doors.Select(v => v.Reason), Has.All.EqualTo("no material"));
    }

    /// <summary>Pass and fail counts per specification, against ifctester over the same IFC.</summary>
    [Test]
    public void DcW1_AgreesWithIfctester()
    {
        var ifc = RepoPaths.Samples("public", "sources", "Duplex_A_20110907.ifc");
        if (!File.Exists(ifc))
            Assert.Ignore("the Duplex source IFC is not fetched (samples/public/fetch.mjs)");
        var script = Path.Combine(AppContext.BaseDirectory, "parity", "run-ifctester.py");
        var info = new ProcessStartInfo("python", $"-I \"{script}\" \"{ifc}\" \"{Path.Combine(IdsDir, "dc-w1.ids")}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("python did not start");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode == 3)
            Assert.Ignore("ifctester is not installed: " + error.Trim());
        Assert.That(process.ExitCode, Is.EqualTo(0), error);

        var report = IdsEvaluator.Evaluate(IdsFile.Load(Path.Combine(IdsDir, "dc-w1.ids")), Duplex.Value);
        var ours = $"DC-W1 pass={report.Verdicts.Count(v => v.Verdict == Verdict.Pass)} fail={report.Verdicts.Count(v => v.Verdict == Verdict.Fail)}";
        Assert.That(output.Trim(), Is.EqualTo(ours));
    }
}
