using System.Text.Json;
using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
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
    public void QuickFiles_ParityAtLeastCommittedFloor()
    {
        var floors = Ratchets.Load().ParityFloors;
        foreach (var ifcPath in TestFiles.QuickComparisonFiles())
        {
            TestFiles.RequireExists(ifcPath);
            var name = ifcPath.GetFileName();
            Assert.That(floors.TryGetValue(name, out var floor), Is.True, $"{name} has no floor in ratchets.json");
            var result = ModelComparer.CompareFile(ifcPath);
            TestContext.WriteLine(
                $"{name}: parity={result.ParityScore:F3} floor={floor:F3} margin={result.ParityScore - floor:F3}");
            Assert.That(result.ParityScore, Is.GreaterThanOrEqualTo(floor), name);
        }
    }

    [Test]
    [Category("IfcMesherParity")]
    public void SupportedCoverageCount_AtLeastCommittedFloor()
    {
        var floor = Ratchets.Load().SupportedCoverageCount;
        var count = GeometryCoverage.KnownItems.Count(i => i.Support == GeometryCreationSupport.Supported);
        TestContext.WriteLine($"supported rows={count} floor={floor}");
        Assert.That(count, Is.GreaterThanOrEqualTo(floor));
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

/// <summary>The committed floors in <c>bench/ifc-mesher/ratchets.json</c>.</summary>
public readonly record struct Ratchets(
    IReadOnlyDictionary<string, double> ParityFloors,
    int SupportedCoverageCount)
{
    public static Ratchets Load()
    {
        var path = TestFiles.BenchDir.RelativeFile("ratchets.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Ratchets>(File.ReadAllText(path), options);
    }
}
