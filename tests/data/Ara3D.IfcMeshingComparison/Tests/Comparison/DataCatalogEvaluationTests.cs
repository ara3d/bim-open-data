using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Reporting;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

[TestFixture]
public sealed class DataCatalogEvaluationTests
{
    static FilePath JsonReportPath => TestFiles.ReportsDir.RelativeFile("catalog_evaluation.json");
    static FilePath MarkdownReportPath => TestFiles.ReportsDir.RelativeFile("catalog_evaluation.md");

    [Test]
    [Explicit("Meshes every IFC in the corpus (about 90 s) and regenerates its evaluation reports")]
    [Category("IfcMesherCatalog")]
    [Category("Slow")]
    public void EvaluateCorpus()
    {
        var files = TestFiles.AllKnownFiles().ToList();
        if (files.Count == 0)
            Assert.Ignore($"No IFC files found in {TestFiles.DescribeSearchFolders()}");

        void Log(string message) => TestContext.WriteLine(message);
        Log($"Evaluating {files.Count} files from {TestFiles.DescribeSearchFolders()}");

        var document = CatalogEvaluationReport.Run(files, "corpus", Log);
        var jsonPath = CatalogEvaluationReport.WriteJson(document, JsonReportPath);
        var mdPath = CatalogEvaluationReport.WriteMarkdown(document, MarkdownReportPath);

        Log($"Wrote {jsonPath}");
        Log($"Wrote {mdPath}");

        PrintSummary(document);

        Assert.That(jsonPath.Exists(), Is.True);
        Assert.That(document.Files, Has.Count.EqualTo(files.Count));
        Assert.That(document.WorkPriorities, Is.Not.Empty);

        var successful = document.Files.Count(f => !f.ComparisonFailed);
        Assert.That(successful, Is.GreaterThan(0), "At least one file should compare successfully");
    }

    static void PrintSummary(CatalogEvaluationDocument document)
    {
        TestContext.WriteLine("");
        TestContext.WriteLine("=== Corpus summary ===");
        TestContext.WriteLine(
            "| File | Parity | Inst c/o | Tris c/o | Oracle-only |");
        foreach (var file in document.Files.OrderByDescending(f => f.Comparison?.ParityScore ?? -1))
        {
            var parity = file.ComparisonFailed ? "FAIL" : $"{file.Comparison!.ParityScore:F3}";
            var oracleOnly = file.GapAnalysis.OracleOnlyByProductType.Values.Sum();
            TestContext.WriteLine(
                $"| {file.FileName} | {parity} | " +
                $"{file.CandidateStats.Instances}/{file.OracleStats.Instances} | " +
                $"{file.CandidateStats.Triangles}/{file.OracleStats.Triangles} | {oracleOnly} |");
        }

        TestContext.WriteLine("");
        TestContext.WriteLine("=== Top work priorities ===");
        foreach (var item in document.WorkPriorities.Take(15))
        {
            TestContext.WriteLine(
                $"  {item.EntityName}: priority={item.PriorityScore}, catalog={item.TotalCatalogCount}, " +
                $"files={item.FilesAffected}, oracleOnly={item.OracleOnlyProductCount}, " +
                $"triLoss={item.OracleOnlyTriangleLoss}, support={item.CoverageSupport}");
        }
    }
}
