using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Models;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Whole-model builds of the corpus models: instance counts, entity ids, per-entity bounds
/// against the web-ifc oracle, and instance colours.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusModelBuildTests
{
    [Test]
    public void SteelPlates_InstanceEntityIdsUseOracleProductIdSpace()
    {
        TestFiles.RequireExists(TestFiles.SteelPlates);
        var candidate = ModelComparer.LoadCandidate(TestFiles.SteelPlates);
        var oracle = ModelComparer.LoadOracle(TestFiles.SteelPlates);
        var oracleMap = OracleEntityMap.Build(TestFiles.SteelPlates, oracle);

        var productIds = oracleMap.ProductRepresentationTrees
            .Select(t => t.EntityId)
            .ToHashSet();
        var oracleIds = oracle.Instances.Select(i => i.EntityIndex).ToHashSet();
        var candidateIds = candidate.Instances.Select(i => i.EntityIndex).ToHashSet();
        var shared = oracleIds.Intersect(candidateIds).Count();

        Assert.That(candidateIds, Is.SubsetOf(productIds),
            () => $"Candidate entity ids must be IFC product express ids, not representation items: " +
                  string.Join(", ", candidateIds.Except(productIds)));
        Assert.That(shared, Is.GreaterThanOrEqualTo(11),
            () => $"Expected strong product-id overlap with oracle; shared={shared}");
    }

    [Test]
    [Category("Slow")]
    public void OpenHouse_BuildsMultipleProductInstances()
    {
        TestFiles.RequireExists(TestFiles.IfcOpenHouse);
        using var file = new IfcFile(TestFiles.IfcOpenHouse, includeGeometry: false);
        var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);

        Assert.That(model.Instances, Has.Count.GreaterThan(10),
            () => $"Expected broad OpenHouse coverage; instances={model.Instances.Count}; " +
                  $"unsupported={string.Join(", ", diagnostics.EntityStatus.Where(kv => kv.Value == GeometrySupportStatus.Unsupported).Select(kv => kv.Key))}");
        Assert.That(model.Meshes.Sum(m => m.FaceIndices.Count), Is.GreaterThan(100));
    }

    [Test]
    [Category("Slow")]
    public void Small_AttDrivenExtrudedSolid_ProducesGeometry()
    {
        TestFiles.RequireExists(TestFiles.Small);
        using var file = TestFiles.LoadStep(TestFiles.Small);
        var ctx = new MeshingContext(file.EntityResolver);
        var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(42));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Slow")]
    public void DentalClinic_OpenProfiles_DoNotAbortModelBuild()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
        var triangleCount = model.Meshes.Sum(m => m.FaceIndices.Count);
        Assert.That(triangleCount, Is.GreaterThan(0));
        TestContext.WriteLine($"Meshes: {model.Meshes.Count}, Triangles: {triangleCount}");
        TestContext.WriteLine($"Open profiles seen: {diagnostics.EntityCounts.GetValueOrDefault("IFCARBITRARYOPENPROFILEDEF")}");
        TestContext.WriteLine($"Surface extrusions seen: {diagnostics.EntityCounts.GetValueOrDefault("IFCSURFACEOFLINEAREXTRUSION")}");
    }

    [Test]
    [Category("Slow")]
    public void IfcOpenHouse_BuildsModelWithInstances()
    {
        TestFiles.RequireExists(TestFiles.IfcOpenHouse);
        using var file = TestFiles.LoadStep(TestFiles.IfcOpenHouse);
        var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Instances.Count, Is.GreaterThan(0));
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
        TestContext.WriteLine($"Instances: {model.Instances.Count}, Meshes: {model.Meshes.Count}");
        TestContext.WriteLine($"Diagnostics: {diagnostics.EntityCounts.Count} entity types");
    }

    [Test]
    public void SteelPlates_SharedEntityBoundingBoxesMatchOracle()
    {
        TestFiles.RequireExists(TestFiles.SteelPlates);
        var candidate = ModelComparer.LoadCandidate(TestFiles.SteelPlates);
        var oracle = ModelComparer.LoadOracle(TestFiles.SteelPlates);
        var candBounds = EntityBounds(candidate);
        var oracleBounds = EntityBounds(oracle);
        var shared = candBounds.Keys.Intersect(oracleBounds.Keys).ToList();
        var matched = shared.Count(id => OracleComparison.BoundsClose(candBounds[id], oracleBounds[id], 0.05));
        var mismatched = shared.Where(id => !OracleComparison.BoundsClose(candBounds[id], oracleBounds[id], 0.05)).ToList();
        if (mismatched.Count > 0)
            TestContext.WriteLine("Mismatched: " + string.Join(", ", mismatched.Select(id => $"#{id}")));

        Assert.That(shared, Has.Count.GreaterThanOrEqualTo(10));
        Assert.That(matched, Is.GreaterThanOrEqualTo(11),
            () => $"Expected ≥11 shared entity bboxes within tolerance; matched={matched}/{shared.Count}; mismatched=[{string.Join(",", mismatched)}]");
    }

    [Test]
    public void IfcOpenHouse_SharedEntityBoundingBoxesMatchOracle()
    {
        TestFiles.RequireExists(TestFiles.IfcOpenHouse);
        var candidate = ModelComparer.LoadCandidate(TestFiles.IfcOpenHouse);
        var oracle = ModelComparer.LoadOracle(TestFiles.IfcOpenHouse);
        var candBounds = EntityBounds(candidate);
        var oracleBounds = EntityBounds(oracle);
        var shared = candBounds.Keys.Intersect(oracleBounds.Keys).ToList();
        var matched = shared.Count(id => OracleComparison.BoundsClose(candBounds[id], oracleBounds[id], 0.05));
        var mismatched = shared.Where(id => !OracleComparison.BoundsClose(candBounds[id], oracleBounds[id], 0.05)).ToList();
        if (mismatched.Count > 0)
            TestContext.WriteLine("Mismatched: " + string.Join(", ", mismatched.Select(id => $"#{id}")));

        Assert.That(shared, Has.Count.GreaterThanOrEqualTo(33));
        Assert.That(matched, Is.GreaterThanOrEqualTo(33),
            () => $"Expected ≥33 shared entity bboxes within tolerance; matched={matched}/{shared.Count}; mismatched=[{string.Join(",", mismatched)}]");
    }

    [Test]
    [Category("Slow")]
    public void Example_PrintOracleOnlyGaps()
    {
        TestFiles.RequireExists(TestFiles.Example);
        var oracle = ModelComparer.LoadOracle(TestFiles.Example);
        var candidate = ModelComparer.LoadCandidate(TestFiles.Example);
        var oracleIds = oracle.Instances.Select(i => i.EntityIndex).ToHashSet();
        var candidateIds = candidate.Instances.Select(i => i.EntityIndex).ToHashSet();
        var oracleOnly = oracleIds.Except(candidateIds).OrderBy(x => x).ToList();
        TestContext.WriteLine($"oracleOnly={oracleOnly.Count} candidate={candidate.Instances.Count} oracle={oracle.Instances.Count}");

        using var file = new IfcFile(TestFiles.Example, includeGeometry: false);
        var byType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var id in oracleOnly)
        {
            var name = file.EntityResolver.GetEntity(id).GetEntityName();
            if (!byType.TryGetValue(name, out var list))
            {
                list = [];
                byType[name] = list;
            }
            list.Add(id);
        }
        foreach (var kv in byType.OrderByDescending(kv => kv.Value.Count))
            TestContext.WriteLine($"{kv.Key}: {kv.Value.Count} [{string.Join(", ", kv.Value.Take(8))}]");

        foreach (var id in oracleOnly.Take(20))
        {
            var tree = OracleEntityMap.Build(TestFiles.Example).ProductRepresentationTrees
                .FirstOrDefault(t => t.EntityId == id);
            var geom = tree is null ? "?" : SummarizeTree(tree.Children.FirstOrDefault());
            TestContext.WriteLine($"  #{id} {file.EntityResolver.GetEntity(id).GetEntityName()}: {geom}");
        }

        var (_, diag) = ModelAssembler.BuildModel(file.EntityResolver);
        TestContext.WriteLine("--- unsupported ---");
        foreach (var kv in diag.EntityStatus.Where(kv => kv.Value == GeometrySupportStatus.Unsupported)
                     .OrderByDescending(kv => diag.EntityCounts.GetValueOrDefault(kv.Key)).Take(12))
            TestContext.WriteLine($"{kv.Key}: {diag.EntityCounts.GetValueOrDefault(kv.Key)}");
        Assert.Pass();
    }

    [Test]
    [Category("Slow")]
    public void Example_SharedEntityBoundingBoxesMatchOracle()
    {
        TestFiles.RequireExists(TestFiles.Example);
        var candidate = ModelComparer.LoadCandidate(TestFiles.Example);
        var oracle = ModelComparer.LoadOracle(TestFiles.Example);
        var candBounds = EntityBounds(candidate);
        var oracleBounds = EntityBounds(oracle);
        var shared = candBounds.Keys.Intersect(oracleBounds.Keys).ToList();
        var matched = shared.Count(id => OracleComparison.BoundsClose(candBounds[id], oracleBounds[id], 0.05));

        Assert.That(shared, Has.Count.GreaterThanOrEqualTo(20));
        Assert.That(matched, Is.EqualTo(shared.Count),
            () => $"Expected all shared entity bboxes to match oracle; matched={matched}/{shared.Count}");
    }

    [Test]
    [Category("Slow")]
    public void IfcOpenHouse_BoundsRoughlyMatchOracle()
    {
        TestFiles.RequireExists(TestFiles.IfcOpenHouse);
        var (mine, oracle) = OracleComparison.CompareFile(TestFiles.IfcOpenHouse);
        TestContext.WriteLine(OracleComparison.FormatComparison("IfcOpenHouse", mine, oracle));
        Assert.That(mine.TriangleCount, Is.GreaterThan(0));
        Assert.That(OracleComparison.BoundsOverlap(mine.Bounds, oracle.Bounds));
    }

    [Test]
    [Category("Slow")]
    public void AiscSculpture_MappedBrepCoverage()
    {
        var ifcPath = TestFiles.AiscSculptureBrep;
        TestFiles.RequireExists(ifcPath);

        using var file = new IfcFile(ifcPath, includeGeometry: false);
        var (model, _) = ModelAssembler.BuildModel(file.EntityResolver);
        var oracle = ModelComparer.LoadOracle(ifcPath);

        var candIds = model.Instances.Select(i => i.EntityIndex).ToHashSet();
        var oracleIds = oracle.Instances.Select(i => i.EntityIndex).ToHashSet();
        var oracleOnly = oracleIds.Except(candIds).Count();

        TestContext.WriteLine(
            $"inst {model.Instances.Count}/{oracle.Instances.Count}, " +
            $"meshes {model.Meshes.Count}/{oracle.Meshes.Count}, " +
            $"tris {model.Meshes.Sum(m => m.FaceIndices.Count)}/{oracle.Meshes.Sum(m => m.FaceIndices.Count)}, " +
            $"oracleOnly={oracleOnly}");

        Assert.That(oracleOnly, Is.EqualTo(0), "Every oracle product id should emit at least one instance");
        Assert.That(model.Meshes.Count, Is.GreaterThanOrEqualTo(100));
        Assert.That(model.Meshes.Sum(m => m.FaceIndices.Count), Is.GreaterThan(4000));
    }

    [Test]
    [Category("Slow")]
    public void ExampleIfc_HasNonDefaultInstanceColors()
    {
        TestFiles.RequireExists(TestFiles.Example);
        using var file = new IfcFile(TestFiles.Example, includeGeometry: false);
        var (built, _) = ModelAssembler.BuildModel(file.EntityResolver);

        Assert.That(built.Instances, Has.Count.GreaterThan(0));
        var defaultPacked = new InstanceStruct(-1, Matrix4x4.Identity, 0, Material.Default).PackedColor;
        var nonDefault = built.Instances.Count(i => i.PackedColor != defaultPacked);
        Assert.That(nonDefault, Is.GreaterThan(0),
            () => $"Expected some styled instances on example.ifc; total={built.Instances.Count}, nonDefault={nonDefault}");
    }

    static string SummarizeTree(RepresentationTreeNode? node)
    {
        if (node is null) return "no-rep";
        if (node.Children.Count == 0)
            return node.Notes ?? node.EntityName;
        return $"{node.EntityName}({string.Join(",", node.Children.Select(SummarizeTree))})";
    }

    static Dictionary<int, Bounds3D> EntityBounds(Model3D model)
    {
        var bounds = new Dictionary<int, Bounds3D>();
        foreach (var inst in model.Instances)
        {
            if (inst.EntityIndex < 0 || inst.MeshIndex < 0 || inst.MeshIndex >= model.Meshes.Count)
                continue;
            var transformed = MeshHelpers.Transform(model.Meshes[inst.MeshIndex], inst.Matrix4x4);
            var meshBounds = MeshHelpers.GetBounds(transformed);
            if (bounds.TryGetValue(inst.EntityIndex, out var existing))
                bounds[inst.EntityIndex] = existing.Merge(meshBounds);
            else
                bounds[inst.EntityIndex] = meshBounds;
        }
        return bounds;
    }
}
