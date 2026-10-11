using System.Diagnostics;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

/// <summary>The five meshers against each other on the quick files. The external meshers are read
/// from the committed dumps under bench/ifc-mesher/dumps; see bench/ifc-mesher/README.md.</summary>
[TestFixture]
public sealed class MesherMatrixTests
{
    static FilePath MeshersJsonPath => TestFiles.BenchDir.RelativeFile("meshers.json");

    [Test]
    public void QuickFiles_EveryPairScores_Within30Seconds()
    {
        var files = TestFiles.QuickComparisonFiles().ToList();
        foreach (var file in files)
            TestFiles.RequireExists(file);
        var missing = MesherCatalog.Dumped
            .SelectMany(d => files.Where(f => d.FindDump(f) is null).Select(f => $"{d.Name}/{f.GetFileName()}"))
            .ToList();
        if (missing.Count > 0)
            Assert.Ignore("No mesh dump for: " + string.Join(", ", missing) + ". Run bench/ifc-mesher/meshers/run.mjs.");

        var sw = Stopwatch.StartNew();
        var document = new MesherMatrixDocument(DateTime.UtcNow, files.Select(f => MesherMatrix.Build(f, MesherCatalog.All)).ToList());
        sw.Stop();
        TestContext.WriteLine(MesherMatrix.FormatMarkdown(document));
        TestContext.WriteLine($"{sw.Elapsed.TotalSeconds:F1} s");

        var n = MesherCatalog.All.Count;
        Assert.Multiple(() =>
        {
            foreach (var file in document.Files)
            {
                Assert.That(file.Meshers.Where(m => m.Available).ToList(), Has.Count.EqualTo(n), file.FileName);
                Assert.That(file.Pairs, Has.Count.EqualTo(n * (n - 1)), file.FileName);
                Assert.That(file.Pairs.Select(p => p.Parity), Has.All.InRange(0.0, 1.0), file.FileName);
            }
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)));
        });
    }

    [Test]
    [Explicit("Writes bench/ifc-mesher/meshers.json and prints the RESULTS.md section")]
    public void WriteMeshersJson()
    {
        var files = TestFiles.QuickComparisonFiles().ToList();
        foreach (var file in files)
            TestFiles.RequireExists(file);
        var document = new MesherMatrixDocument(DateTime.UtcNow, files.Select(f => MesherMatrix.Build(f, MesherCatalog.All)).ToList());
        MesherMatrix.WriteJson(MeshersJsonPath, document);
        TestContext.WriteLine(MesherMatrix.FormatMarkdown(document));
        TestContext.WriteLine($"Wrote {MeshersJsonPath}");
        Assert.That(MeshersJsonPath.Exists(), Is.True);
    }
}
