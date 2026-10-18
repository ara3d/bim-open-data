using Ara3D.IO.StepParser;

namespace Ara3D.Step.Mesher.Tests.Model;

public static class StepMesherTests
{
    [Test]
    public static void UnitCubeResultIsInMillimetres()
    {
        var result = StepMesher.Mesh(StepText.Fixture("unit-cube.stp"));
        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.ContextUnits.Values.Single().MetresPerUnit, Is.EqualTo(0.001).Within(1e-15));
        Assert.That(result.Placements.Select(p => p.ItemId), Is.EqualTo(new[] { 126 }));
        Assert.That(result.ParseDiagnostics, Is.Empty);
        Assert.That(result.Prefix, Is.Null);
    }

    [Test]
    public static void InchContextResultReadsTwoPointFiveFourCentimetres()
    {
        using var document = StepText.Parse("""
            #1=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
            #2=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(25.4),#1);
            #3=(CONVERSION_BASED_UNIT('INCH',#2)LENGTH_UNIT()NAMED_UNIT(*));
            #6=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT((#3))REPRESENTATION_CONTEXT('',''));
            """);
        Assert.That(StepMesher.Mesh(document).ContextUnits[6].MetresPerUnit, Is.EqualTo(0.0254).Within(1e-12));
    }

    [Test]
    public static void AssemblyResultPlacesTheCubeTwice()
    {
        var result = StepMesher.Mesh(StepText.Fixture("two-cubes-assembly.stp"));
        Assert.That(result.Placements.Select(p => p.ItemId), Is.EqualTo(new[] { 126, 126 }));
        Assert.That(result.Placements[1].Transform.Translation.X, Is.EqualTo(0.002f).Within(1e-9f));
        Assert.That(result.AssemblyDepth, Is.EqualTo(1));
        Assert.That(result.Cycles, Is.Empty);
    }

    [Test]
    [Ignore("Needs StepShapeMesher.MeshItem, whose body comes from the Plato STEP library (TKT-299); remove when it lands.")]
    public static void AssemblyModelHasTwoInstancesOfOneMesh()
    {
        var result = StepMesher.Mesh(StepText.Fixture("two-cubes-assembly.stp"));
        Assert.That(result.MeshCount, Is.EqualTo(1));
        Assert.That(result.Model!.Instances.Select(i => i.MeshIndex), Is.EqualTo(new[] { 0, 0 }));
        Assert.That((float)result.Model.Instances[1].Matrix4x4.M41, Is.EqualTo(0.002f).Within(1e-9f));
        Assert.That((float)result.Bounds.Max.X, Is.EqualTo(0.003f).Within(1e-7f));
    }

    [Test]
    public static void ItemsWithoutAShapeMesherAreCountedAsPending()
    {
        var result = StepMesher.Mesh(StepText.Fixture("two-cubes-assembly.stp"));
        Assume.That(result.MeshCount, Is.Zero, "the shape mesher has landed");
        Assert.That(result.GeometryPending, Is.EqualTo(1));
        Assert.That(result.Diagnostics.Messages, Has.Some.Contains("geometry pending for 1 shape items"));
    }

    [Test]
    public static void TruncatedFileCarriesTheParserErrorAndItsPrefix()
    {
        var result = StepMesher.Mesh(StepText.Fixture("unit-cube-truncated.stp"));
        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        var error = result.ParseDiagnostics.Single(d => d.Severity == StepSeverity.Error);
        Assert.That(error.Code, Is.EqualTo(StepDiagnosticCode.Syntax));
        Assert.That(error.DefinitionId, Is.EqualTo(125));
        Assert.That(result.Prefix, Is.EqualTo(new StepParsedPrefix(124, 124, error.Offset)));
        Assert.That(result.Placements, Is.Empty);
    }

    [Test]
    public static void MissingFileIsAFailedResult()
    {
        var result = StepMesher.Mesh(StepText.Fixture("no-such-file.stp"));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }
}
