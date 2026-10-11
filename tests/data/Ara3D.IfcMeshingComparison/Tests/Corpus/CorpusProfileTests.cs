using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcTypes;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Profiles read from the corpus models: steel sections, L angles, hollow sections.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusProfileTests
{
    [Test]
    [Category("Slow")]
    public void AiscSculpture_SteelProfiles_ProduceExtrusions()
    {
        TestFiles.RequireExists(TestFiles.AiscSculptureBrep);
        using var file = TestFiles.LoadStep(TestFiles.AiscSculptureBrep);
        var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);
        Assert.That(model.Meshes.Count, Is.GreaterThan(0));
        Assert.That(diagnostics.EntityStatus.GetValueOrDefault("IFCLSHAPEPROFILEDEF"),
            Is.EqualTo(GeometrySupportStatus.Supported));
    }

    [Test]
    public void ExampleIfc_LAngleProfiles_FromFullFile_AreSimple()
    {
        TestFiles.RequireExists(TestFiles.Example);
        using var file = TestFiles.LoadStep(TestFiles.Example);
        var ctx = new MeshingContext(file.EntityResolver);

        foreach (var (profileId, label) in new (int id, string name)[]
        {
            (3405, "L2-col"), (3576, "L2-col-mirror"), (3278, "L3-beam"),
        })
        {
            var profile = ctx.GetEntity(profileId);
            var curveId = profile.GetId(IfcArbitraryClosedProfileDef.Instance.OuterCurve.Index);
            var points = CurveEvaluator.Evaluate2D(ctx, ctx.GetEntity(curveId), dropClosure: true);
            var cleaned = PolygonWithHoles.CleanRing(points);
            Assert.That(PolygonWithHoles.HasSelfIntersection(cleaned), Is.False,
                () => $"{label} #{profileId} curve #{curveId}: {cleaned.Count} pts, scale={ctx.LengthScale}");
        }
    }

    [Test]
    public void ExampleIfc_SteelSectionExtrusions_Build()
    {
        TestFiles.RequireExists(TestFiles.Example);
        using var file = TestFiles.LoadStep(TestFiles.Example);
        var ctx = new MeshingContext(file.EntityResolver);

        foreach (var (solidId, profileId) in new (int solid, int profile)[]
        {
            (3409, 3405), (3580, 3576), (3282, 3278), (3168, 3164), (2679, 2677),
        })
        {
            var curveId = file.EntityResolver.GetEntity(profileId).GetId(2);
            var points = CurveEvaluator.Evaluate2D(ctx, ctx.GetEntity(curveId), dropClosure: true);
            var cleaned = PolygonWithHoles.CleanRing(points);
            var selfIntersect = PolygonWithHoles.HasSelfIntersection(cleaned);
            TestContext.WriteLine($"#{solidId} profile #{profileId} curve #{curveId}: {cleaned.Count} pts, selfIntersect={selfIntersect}");
        }

        var built = 0;
        foreach (var id in new[] { 3409, 3580, 3282, 3168, 2679 })
        {
            var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(id));
            if (mesh.HasValue && mesh.Value.FaceIndices.Count > 0)
                built++;
        }
        Assert.That(built, Is.EqualTo(5), "All steel section extrusions from example.ifc should mesh");
    }

    [Test]
    public void ExampleIfc_ShsProfileWithVoids_Builds()
    {
        TestFiles.RequireExists(TestFiles.Example);
        using var file = TestFiles.LoadStep(TestFiles.Example);
        var ctx = new MeshingContext(file.EntityResolver);

        foreach (var (solidId, profileId) in new (int solid, int profile)[]
        {
            (7649, 7642), (7935, 7928),
        })
        {
            var profile = ProfileBuilder.Build(ctx, ctx.GetEntity(profileId));
            TestContext.WriteLine(
                $"#{solidId} profile #{profileId}: outer={profile.Outer.Count} hole={profile.Holes[0].Count} " +
                $"outerSelf={PolygonWithHoles.HasSelfIntersection(profile.Outer)} " +
                $"holeSelf={PolygonWithHoles.HasSelfIntersection(profile.Holes[0])}");
            Assert.That(profile.Triangulate(), Is.Not.Empty);
            var mesh = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(solidId));
            Assert.That(mesh.HasValue, Is.True, $"#{solidId} should mesh");
            Assert.That(mesh!.Value.FaceIndices.Count, Is.GreaterThan(0));
        }
    }
}
