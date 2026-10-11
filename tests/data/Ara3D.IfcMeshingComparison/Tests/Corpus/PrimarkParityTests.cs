using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>The PRIMARK model (private; ignored when absent): which products only the oracle meshes,
/// and two discrete accessories whose plates must mesh.</summary>
[TestFixture]
public sealed class PrimarkParityTests
{
    static FilePath PrimarkIfc => TestFiles.Primark;

    [Test]
    [Explicit("Diagnosis: PRIMARK oracle-only products")]
    [Category("Slow")]
    public void ScorePrimarkStretch_Diagnosis()
    {
        var ifcPath = PrimarkIfc;
        TestFiles.RequireExists(ifcPath);

        var bfastPath = WebIfcBfastOracle.OraclePath(ifcPath);
        if (!bfastPath.Exists() || WebIfcBfastOracle.NeedsRegeneration(ifcPath, bfastPath))
            WebIfcBfastOracle.Generate(ifcPath, TestContext.WriteLine);

        OracleEntityMap.Write(ifcPath);
        var map = OracleEntityMap.Build(ifcPath);
        TestContext.WriteLine($"Oracle map: {map.OracleInstanceCount} instances, {map.OracleMeshCount} meshes");

        using var stepFile = new IfcFile(ifcPath, includeGeometry: false);
        var (model, diagnostics) = ModelAssembler.BuildModel(stepFile.EntityResolver);
        TestContext.WriteLine(
            $"Candidate: {model.Instances.Count} instances, {model.Meshes.Count} meshes, " +
            $"{model.Meshes.Sum(m => m.FaceIndices.Count)} tris");

        var result = ModelComparer.Compare(model, ModelComparer.LoadOracle(ifcPath), ifcPath.GetFileName());
        TestContext.WriteLine(ModelComparer.FormatResult(result));

        var candidateEntities = model.Instances
            .Where(i => i.EntityIndex >= 0)
            .Select(i => i.EntityIndex)
            .ToHashSet();
        var oracleInstByEntity = map.OracleInstances
            .GroupBy(i => i.EntityIndex)
            .ToDictionary(g => g.Key, g => g.Count());

        var oracleOnlyProducts = map.ProductRepresentationTrees
            .Where(t => !candidateEntities.Contains(t.EntityId) && oracleInstByEntity.ContainsKey(t.EntityId))
            .ToList();
        TestContext.WriteLine($"Oracle-only products: {oracleOnlyProducts.Count}");

        TestContext.WriteLine("Oracle-only products by type:");
        foreach (var group in oracleOnlyProducts
                     .GroupBy(t => t.EntityName)
                     .OrderByDescending(g => g.Count()))
            TestContext.WriteLine($"  {group.Key}: {group.Count()}");

        TestContext.WriteLine("Diagnostics unsupported:");
        foreach (var (name, count) in diagnostics.EntityCounts
                     .Where(kv => diagnostics.EntityStatus.GetValueOrDefault(kv.Key) == GeometrySupportStatus.Unsupported)
                     .OrderByDescending(kv => kv.Value)
                     .Take(20))
            TestContext.WriteLine($"  {name}: {count}");
    }

    /// <summary>#92859 is the largest oracle-only IFCDISCRETEACCESSORY (644 oracle triangles); its profile
    /// #92854 is a multi-segment IFCCOMPOSITECURVE with trimmed arcs and a DISCONTINUOUS closure.</summary>
    [Test]
    [Category("Slow")]
    public void Primark_DiscreteAccessory_CompositePlateProfile_Builds()
    {
        TestFiles.RequireExists(PrimarkIfc);
        using var file = new IfcFile(PrimarkIfc, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        const int accessoryId = 92859;
        var accessory = ctx.GetEntity(accessoryId);
        Assert.That(accessory.GetEntityName(), Is.EqualTo("IFCDISCRETEACCESSORY"));

        var mesh = ModelAssembler.BuildEntityMesh(ctx, accessory);
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0),
            $"#{accessoryId} PL15*220 composite-profile plate should mesh (catalog: 644 oracle tris)");
    }

    /// <summary>A clipping representation over a simple polyline profile.</summary>
    [Test]
    [Category("Slow")]
    public void Primark_DiscreteAccessory_ClippedPlate_Builds()
    {
        TestFiles.RequireExists(PrimarkIfc);
        using var file = new IfcFile(PrimarkIfc, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        const int accessoryId = 40020;
        var mesh = ModelAssembler.BuildEntityMesh(ctx, ctx.GetEntity(accessoryId));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0),
            $"#{accessoryId} clipped PL15*180 plate should mesh");
    }
}
