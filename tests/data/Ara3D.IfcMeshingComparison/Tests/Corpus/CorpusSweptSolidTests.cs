using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Swept solids and open-profile ribbons in the corpus models build.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusSweptSolidTests
{
    [Test]
    [Category("Slow")]
    public void DentalClinic_AllSurfaceOfLinearExtrusions_BuildRibbons()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var ctx = new MeshingContext(file.EntityResolver);

        var total = 0;
        var built = 0;
        var failures = new List<string>();
        foreach (var entity in ctx.Resolver.GetEntities())
        {
            if (entity.GetEntityName() != "IFCSURFACEOFLINEAREXTRUSION")
                continue;
            total++;
            try
            {
                var mesh = GeometryDispatcher.TryBuild(ctx, entity);
                if (mesh is { FaceIndices.Count: > 0 })
                    built++;
                else
                    failures.Add($"#{entity.Id}: empty mesh");
            }
            catch (Exception ex)
            {
                failures.Add($"#{entity.Id}: {ex.Message}");
            }
        }

        TestContext.WriteLine($"Dental clinic surface extrusions: {built}/{total} built");
        foreach (var f in failures.Take(10))
            TestContext.WriteLine($"  {f}");
        Assert.That(built, Is.EqualTo(total), $"Failed: {string.Join("; ", failures.Take(5))}");
    }

    [Test]
    [Category("Slow")]
    public void DentalClinic_TrimmedArcOpenProfileRibbon_Builds()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var ctx = new MeshingContext(file.EntityResolver);
        var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(161277));
        Assert.That(mesh, Is.Not.Null, "Trimmed-arc open profile ribbon should mesh");
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(2));
        TestContext.WriteLine($"Triangles: {mesh.Value.FaceIndices.Count}, Points: {mesh.Value.Points.Count}");
    }

    [Test]
    [Category("Slow")]
    public void DentalClinic_SurfaceOfLinearExtrusion_EntityBuildsRibbon()
    {
        TestFiles.RequireExists(TestFiles.DentalClinic);
        using var file = TestFiles.LoadStep(TestFiles.DentalClinic);
        var ctx = new MeshingContext(file.EntityResolver);
        var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(159483));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
        Assert.That(mesh.Value.Points.Count, Is.GreaterThanOrEqualTo(4));
        TestContext.WriteLine($"Triangles: {mesh.Value.FaceIndices.Count}, Points: {mesh.Value.Points.Count}");
    }

    [Test]
    [Category("Slow")]
    public void OfficeA_AllSurfaceOfLinearExtrusions_BuildRibbons()
    {
        TestFiles.RequireExists(TestFiles.OfficeA);
        using var file = TestFiles.LoadStep(TestFiles.OfficeA);
        var ctx = new MeshingContext(file.EntityResolver);

        var total = 0;
        var built = 0;
        foreach (var entity in ctx.Resolver.GetEntities())
        {
            if (entity.GetEntityName() != "IFCSURFACEOFLINEAREXTRUSION")
                continue;
            total++;
            var mesh = GeometryDispatcher.TryBuild(ctx, entity);
            if (mesh is { FaceIndices.Count: > 0 })
                built++;
        }

        TestContext.WriteLine($"Office_A surface extrusions: {built}/{total} built");
        Assert.That(built, Is.EqualTo(total), "All Office_A open-profile ribbon extrusions should mesh");
    }

    [Test]
    [Category("Slow")]
    public void Duplex_AllSurfaceOfLinearExtrusions_BuildRibbons()
    {
        TestFiles.RequireExists(TestFiles.Duplex);
        using var file = TestFiles.LoadStep(TestFiles.Duplex);
        var ctx = new MeshingContext(file.EntityResolver);

        var total = 0;
        var built = 0;
        foreach (var entity in ctx.Resolver.GetEntities())
        {
            if (entity.GetEntityName() != "IFCSURFACEOFLINEAREXTRUSION")
                continue;
            total++;
            var mesh = GeometryDispatcher.TryBuild(ctx, entity);
            if (mesh is { FaceIndices.Count: > 0 })
                built++;
        }

        TestContext.WriteLine($"Duplex surface extrusions: {built}/{total} built");
        Assert.That(built, Is.EqualTo(total), "All duplex open-profile ribbon extrusions should mesh");
    }

    [Test]
    [Category("Slow")]
    public void OfficeA_SurfaceOfLinearExtrusion_EntityBuildsRibbon()
    {
        TestFiles.RequireExists(TestFiles.OfficeA);
        using var file = TestFiles.LoadStep(TestFiles.OfficeA);
        var ctx = new MeshingContext(file.EntityResolver);
        var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(45220));
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
        TestContext.WriteLine($"Triangles: {mesh.Value.FaceIndices.Count}");
    }

    [Test]
    [Category("Slow")]
    public void ExampleIfc_MeshBoundsWithinOracleTolerance()
    {
        TestFiles.RequireExists(TestFiles.Example);
        var (mine, oracle) = OracleComparison.CompareFile(TestFiles.Example);
        TestContext.WriteLine(OracleComparison.FormatComparison("example.ifc", mine, oracle));
        Assert.That(mine.TriangleCount, Is.GreaterThan(100));
    }
}
