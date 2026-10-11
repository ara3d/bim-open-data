using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Parity against web-ifc on the two Institute models (private files; ignored when absent).</summary>
[TestFixture]
public sealed class InstituteParityTests
{
    [Test]
    [Explicit("C20-Institute-Var-2 parity and triangle ratio")]
    [Category("Slow")]
    public void ScoreC20InstituteStretch()
    {
        TestFiles.RequireExists(TestFiles.C20Institute);
        EnsureOracle(TestFiles.C20Institute);

        var result = ModelComparer.CompareFile(TestFiles.C20Institute);
        TestContext.WriteLine(ModelComparer.FormatResult(result));

        var mergedRatio = SafeRatio(result.MergedMesh.CandidateTriangleCount, result.MergedMesh.OracleTriangleCount);
        TestContext.WriteLine(
            $"C20 gates: parity={result.ParityScore:F3} entityShape={result.EntityShape.Score:F3} " +
            $"mergedTri={result.MergedMesh.CandidateTriangleCount}/{result.MergedMesh.OracleTriangleCount} ({mergedRatio:F3}) " +
            $"inst={result.InstanceCount.Candidate}/{result.InstanceCount.Oracle}");

        Assert.That(result.ParityScore, Is.GreaterThanOrEqualTo(0.85));
        Assert.That(result.EntityShape.Score, Is.GreaterThanOrEqualTo(0.85));
        Assert.That(mergedRatio, Is.GreaterThanOrEqualTo(0.35));
    }

    [Test]
    [Explicit("AC11 Institute parity and triangle ratio")]
    [Category("Slow")]
    public void ScoreAc11InstituteStretch()
    {
        TestFiles.RequireExists(TestFiles.Ac11Institute);
        EnsureOracle(TestFiles.Ac11Institute);

        var result = ModelComparer.CompareFile(TestFiles.Ac11Institute);
        TestContext.WriteLine(ModelComparer.FormatResult(result));

        var mergedRatio = SafeRatio(result.MergedMesh.CandidateTriangleCount, result.MergedMesh.OracleTriangleCount);
        TestContext.WriteLine(
            $"AC11 gates: parity={result.ParityScore:F3} entityShape={result.EntityShape.Score:F3} " +
            $"mergedTri={result.MergedMesh.CandidateTriangleCount}/{result.MergedMesh.OracleTriangleCount} ({mergedRatio:F3}) " +
            $"inst={result.InstanceCount.Candidate}/{result.InstanceCount.Oracle}");

        Assert.That(result.ParityScore, Is.GreaterThanOrEqualTo(0.85));
        Assert.That(result.EntityShape.Score, Is.GreaterThanOrEqualTo(0.85));
    }

    static void EnsureOracle(FilePath ifcPath)
    {
        var bfastPath = WebIfcBfastOracle.OraclePath(ifcPath);
        if (!bfastPath.Exists() || WebIfcBfastOracle.NeedsRegeneration(ifcPath, bfastPath))
            WebIfcBfastOracle.Generate(ifcPath, TestContext.WriteLine);
    }

    static float SafeRatio(int candidate, int oracle)
        => oracle <= 0 ? 0f : (float)candidate / oracle;
}
