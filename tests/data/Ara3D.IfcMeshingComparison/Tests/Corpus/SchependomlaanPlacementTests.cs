using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcTypes;
using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Placement of Schependomlaan's instances against the web-ifc oracle: a diagnosis dump of
/// the worst entities, and a gate on how many entity boxes match.</summary>
[TestFixture]
public sealed class SchependomlaanPlacementTests
{
    static void DumpEntityTransforms(int entityId, Model3D candidate, Model3D oracle)
    {
        var cand = candidate.Instances.Where(i => i.EntityIndex == entityId).ToList();
        var orac = oracle.Instances.Where(i => i.EntityIndex == entityId).ToList();
        TestContext.WriteLine($"Entity #{entityId}: candidate={cand.Count} oracle={orac.Count}");
        for (var i = 0; i < Math.Min(cand.Count, orac.Count); i++)
        {
            var cd = TransformComparison.CompareMatrices(cand[i].Matrix4x4, orac[i].Matrix4x4);
            TestContext.WriteLine(
                $"  [{i}] candT={FormatT(cand[i].Matrix4x4)} oracleT={FormatT(orac[i].Matrix4x4)} " +
                $"tri={candidate.Meshes[cand[i].MeshIndex].FaceIndices.Count} " +
                $"fro={cd.Frobenius:F3} trans={cd.TranslationDistance:F3} rot={cd.RotationAngleDeg:F2}");
        }
    }

    static string FormatT(Matrix4x4 m) => $"({m.M41:F3},{m.M42:F3},{m.M43:F3})";

    [Test]
    [Explicit("Diagnose placement for worst schependomlaan entities")]
    public void Schependomlaan_WorstEntities_PlacementDiagnosis()
    {
        var ifcPath = TestFiles.Schependomlaan;
        TestFiles.RequireExists(ifcPath);

        var candidate = ModelComparer.LoadCandidate(ifcPath);
        var oracle = ModelComparer.LoadOracle(ifcPath);

        foreach (var id in new[] { 44513, 771571, 27461, 452074 })
            DumpEntityTransforms(id, candidate, oracle);

        using var file = new IfcFile(ifcPath, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        var entity = ctx.GetEntity(44513);
        var placement = MeshHelpers.ResolveRequired(ctx, entity, IfcProduct.Instance.ObjectPlacement);
        var productFrame = Placements.ReadLocalPlacement(ctx, placement);
        TestContext.WriteLine($"#44513 product placement origin: {productFrame.Origin}");

        var rep = MeshHelpers.ResolveRequired(ctx, entity, IfcProduct.Instance.Representation);
        var parts = new List<CollectedPart>();
        GeometryPartCollector.CollectParts(ctx, rep, Matrix4x4.Identity, 44513, parts);
        foreach (var (part, idx) in parts.Select((p, i) => (p, i)))
            TestContext.WriteLine(
                $"  part[{idx}] T=({part.Transform.M41:F3},{part.Transform.M42:F3},{part.Transform.M43:F3}) tris={part.Mesh.FaceIndices.Count}");

        if (GeometryDispatcher.TryGetMappedItemTransform(ctx, ctx.GetEntity(44499), out var mapping))
        {
            var productM = productFrame.Matrix;
            TestContext.WriteLine(
                $"  part*product T=({(mapping * productM).M41:F3},{(mapping * productM).M42:F3},{(mapping * productM).M43:F3})");
        }

        var candInst = candidate.Instances.First(i => i.EntityIndex == 44513);
        var oracInst = oracle.Instances.First(i => i.EntityIndex == 44513);
        var candMesh = candidate.Meshes[candInst.MeshIndex];
        var oracMesh = oracle.Meshes[oracInst.MeshIndex];
        var candCenter = MeshHelpers.GetBounds(MeshHelpers.Transform(candMesh, candInst.Matrix4x4)).Center;
        var oracCenter = MeshHelpers.GetBounds(MeshHelpers.Transform(oracMesh, oracInst.Matrix4x4)).Center;
        TestContext.WriteLine($"  cand bounds center=({candCenter.X:F3},{candCenter.Y:F3},{candCenter.Z:F3})");
        TestContext.WriteLine($"  orac bounds center=({oracCenter.X:F3},{oracCenter.Y:F3},{oracCenter.Z:F3})");
    }

    [Test]
    [Explicit("Gate: schependomlaan placement should improve after fix")]
    [Category("Slow")]
    public void Schependomlaan_PlacementGate()
    {
        var ifcPath = TestFiles.Schependomlaan;
        TestFiles.RequireExists(ifcPath);

        var candidate = ModelComparer.LoadCandidate(ifcPath);
        var oracle = ModelComparer.LoadOracle(ifcPath);
        var summary = TransformComparison.Compare(candidate, oracle);
        var modelResult = ModelComparer.Compare(candidate, oracle, ifcPath.GetFileName());

        TestContext.WriteLine(
            $"bbox matched {modelResult.EntityBoundingBox.MatchedCount}/{modelResult.EntityBoundingBox.ComparedCount}, " +
            $"meanCenterΔ={summary.MeanBoundsCenterDistance:F4}, maxCenterΔ={summary.MaxBoundsCenterDistance:F4}");

        Assert.That(modelResult.EntityBoundingBox.MatchedCount, Is.GreaterThanOrEqualTo(520),
            "entity bbox match should improve materially on schependomlaan");
        Assert.That(summary.MeanBoundsCenterDistance, Is.LessThan(0.65),
            "mean world-space center offset should drop near duplex-control quality");
    }
}
