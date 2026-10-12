using System.Diagnostics;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

/// <summary>Writes bench/ifc-mesher/discrepancies/&lt;file&gt;.md from the committed dumps and the two
/// in-process meshers.</summary>
[TestFixture]
public sealed class DiscrepancyTableTests
{
    static DirectoryPath OutputDir => TestFiles.BenchDir.RelativeFolder("discrepancies");

    [Test] public void IfcOpenHouse() => WriteTable(TestFiles.IfcOpenHouse);
    [Test] public void Example() => WriteTable(TestFiles.Example);
    [Test] public void SteelPlates() => WriteTable(TestFiles.SteelPlates);

    static void WriteTable(FilePath ifc)
    {
        TestFiles.RequireExists(ifc);
        foreach (var d in MesherCatalog.Dumped)
            if (d.FindDump(ifc) is null)
                Assert.Ignore($"No {d.Name} dump for {ifc.GetFileName()}. Run bench/ifc-mesher/meshers/run.mjs.");

        var results = MesherCatalog.All.Select(b => b.Build(ifc)).ToList();
        var sw = Stopwatch.StartNew();
        var rows = DiscrepancyTable.FromResults(ifc, results);
        sw.Stop();

        var names = results.Where(r => r.Success).Select(r => r.BackendName).ToList();
        OutputDir.Create();
        File.WriteAllText(OutputDir.RelativeFile(ifc.GetFileNameWithoutExtension() + ".md"),
            DiscrepancyTableMarkdown.Format(ifc.GetFileName(), names, rows));
        TestContext.WriteLine($"{rows.Count} products in {sw.Elapsed.TotalSeconds:F1} s");

        var union = results.Where(r => r.Success)
            .SelectMany(r => ModelComparer.EntityMeshes(r.Model!).Keys).Distinct().Count();
        Assert.Multiple(() =>
        {
            Assert.That(names, Has.Count.EqualTo(MesherCatalog.All.Count));
            Assert.That(rows, Has.Count.EqualTo(union), "one row per product in the union");
            Assert.That(rows.Select(r => r.ExpressId).Distinct().Count(), Is.EqualTo(rows.Count));
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        });
    }
}
