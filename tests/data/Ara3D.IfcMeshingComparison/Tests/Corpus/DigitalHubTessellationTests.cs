using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Tessellation density on FM_ARC_DigitalHub, whose advanced breps sit on cylindrical
/// surfaces: the merged triangle count against web-ifc.</summary>
[TestFixture]
public sealed class DigitalHubTessellationTests
{
    [Test]
    [Category("Slow")]
    public void DigitalHub_MergedTriangleRatio_AtLeast075()
    {
        var result = DigitalHubComparison.Result;
        TestContext.WriteLine(ModelComparer.FormatResult(result));
        var triRatio = result.MergedMesh.CandidateTriangleCount
            / (double)Math.Max(1, result.MergedMesh.OracleTriangleCount);
        TestContext.WriteLine(
            $"merged tris {result.MergedMesh.CandidateTriangleCount}/{result.MergedMesh.OracleTriangleCount} " +
            $"(ratio {triRatio:F3}, score {result.MergedMesh.Score:F3})");

        Assert.That(triRatio, Is.GreaterThanOrEqualTo(0.75));
        Assert.That(result.MergedMesh.Score, Is.GreaterThanOrEqualTo(0.65));
    }
}
