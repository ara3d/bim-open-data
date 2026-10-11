using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

/// <summary>The <c>IfcMesherParity</c> gates over the quick comparison files (and the duplex placement
/// control): what a change to the mesher must not regress. <c>bench/ifc-mesher/README.md</c> says how
/// to run them; <see cref="ScorecardTests.ScoreQuickComparisonFiles"/> writes the committed numbers.</summary>
[TestFixture]
public sealed class ParityGates
{
    [Test]
    [Category("IfcMesherParity")]
    public void QuickFiles_ParityAtLeast085()
    {
        foreach (var ifcPath in TestFiles.QuickComparisonFiles())
        {
            TestFiles.RequireExists(ifcPath);
            var result = ModelComparer.CompareFile(ifcPath);
            TestContext.WriteLine($"{ifcPath.GetFileName()}: parity={result.ParityScore:F3}");
            Assert.That(result.ParityScore, Is.GreaterThanOrEqualTo(0.85), ifcPath.GetFileName());
        }
    }

    [Test]
    [Category("IfcMesherParity")]
    public void QuickFiles_NoInstanceJaccardRegression()
    {
        foreach (var ifcPath in TestFiles.QuickComparisonFiles())
        {
            TestFiles.RequireExists(ifcPath);
            var result = ModelComparer.CompareFile(ifcPath);
            TestContext.WriteLine(
                $"{ifcPath.GetFileName()}: parity={result.ParityScore:F3} instJ={result.EntityInstances.KeyJaccard:F3}");
            Assert.That(result.EntityInstances.KeyJaccard, Is.GreaterThanOrEqualTo(0.99),
                $"{ifcPath.GetFileName()} instance Jaccard regressed");
        }
    }

    [Test]
    [Category("IfcMesherParity")]
    public void WriteOracleMapsForQuickComparisonFiles()
    {
        foreach (var ifcPath in TestFiles.QuickComparisonFiles())
        {
            TestFiles.RequireExists(ifcPath);
            var bfastPath = WebIfcBfastOracle.OraclePath(ifcPath);
            if (!bfastPath.Exists() || WebIfcBfastOracle.NeedsRegeneration(ifcPath, bfastPath))
                WebIfcBfastOracle.Generate(ifcPath, TestContext.WriteLine);

            var mapPath = OracleEntityMap.MapPath(ifcPath);
            OracleEntityMap.Write(ifcPath, mapPath);
            TestContext.WriteLine($"Wrote {mapPath}");

            var document = OracleEntityMap.Build(ifcPath);
            Assert.That(document.OracleInstances, Is.Not.Empty);
            Assert.That(document.ProductRepresentationTrees, Is.Not.Empty);
            Assert.That(mapPath.Exists(), Is.True);
        }
    }

    [Test]
    [Category("IfcMesherParity")]
    public void BfastRoundTrip_PreservesEntityAssignment_QuickFiles()
    {
        foreach (var ifcPath in TestFiles.QuickComparisonFiles())
        {
            TestFiles.RequireExists(ifcPath);
            using var file = TestFiles.LoadWithOracleGeometry(ifcPath);
            var report = WebIfcBfastOracle.CompareRoundTrip(file.ToModel3D());
            Assert.That(report.EntityAssignmentMatches, Is.True, $"{ifcPath.GetFileName()} BFAST I/O drift");
        }
    }

    [Test]
    [Category("IfcMesherParity")]
    public void Duplex_Control_TransformsMostlyMatchOracle()
    {
        var ifcPath = TestFiles.Duplex;
        TestFiles.RequireExists(ifcPath);

        var bfastOraclePath = WebIfcBfastOracle.OraclePath(ifcPath);
        if (!bfastOraclePath.Exists())
            WebIfcBfastOracle.Generate(ifcPath, TestContext.WriteLine);

        var candidate = ModelComparer.LoadCandidate(ifcPath);
        var oracle = ModelComparer.LoadOracle(ifcPath);
        var summary = TransformComparison.Compare(candidate, oracle);

        TestContext.WriteLine(TransformComparison.FormatSummary("duplex control: mesher vs web-ifc oracle", summary));

        var candStats = OracleComparison.ComputeStats(candidate);
        var oracleStats = OracleComparison.ComputeStats(oracle);
        var refBounds = candStats.Bounds.Merge(oracleStats.Bounds);
        var refLen = (refBounds.Max - refBounds.Min).Length();
        var relCenter = summary.MeanBoundsCenterDistance / Math.Max(refLen, 1e-3f);
        TestContext.WriteLine(
            $"duplex relative mean center delta: {relCenter:F4} (refLen={refLen:F2}, " +
            $"meanCenterΔ={summary.MeanBoundsCenterDistance:F4})");

        Assert.That(summary.PairedInstanceCount, Is.GreaterThan(0));
        Assert.That(summary.MeanBoundsCenterDistance, Is.LessThan(2.0),
            "duplex control mean world-space center offset should stay modest");
    }
}
