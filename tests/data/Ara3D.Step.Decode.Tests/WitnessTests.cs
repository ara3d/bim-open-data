using Ara3D.StepBrep;

namespace Ara3D.Step.Decode.Tests;

/// <summary>TKT-312's first criterion: the two adapter fixtures decode to the Plato library's raw witnesses
/// (sandbox/step-brep-mesh/03-step/step-raw-witnesses.library.plato), compared by the library's own SameRawModel.</summary>
public static class WitnessTests
{
    [Test]
    public static void UnitCubeIsTheRawUnitCubeWitness()
    {
        var decoded = Fixtures.Decode("unit-cube.stp");
        var witness = RawDecodedModel.Witness_RawUnitCube();
        Assert.That((bool)decoded.SameRawModel(witness), Is.True, () => Fixtures.Differences(decoded, witness));
    }

    [Test]
    public static void TwoCubeAssemblyIsTheRawTwoCubesWitness()
    {
        var decoded = Fixtures.Decode("two-cubes-assembly.stp");
        var witness = RawDecodedModel.Witness_RawTwoCubes();
        Assert.That((bool)decoded.SameRawModel(witness), Is.True, () => Fixtures.Differences(decoded, witness));
    }

    [Test]
    public static void TheTwoWitnessesDiffer()
        => Assert.That((bool)RawDecodedModel.Witness_RawUnitCube().SameRawModel(RawDecodedModel.Witness_RawTwoCubes()), Is.False);

    [Test]
    public static void UnitCubeDiagnosticsAreTheCensusAndTheUnits()
    {
        var codes = Fixtures.Decode("unit-cube.stp").Codes();
        Assert.That(codes, Does.Contain("STEP-IGNORED-TYPE"));
        Assert.That(codes, Does.Contain("STEP-LENGTH-UNIT"));
        Assert.That(codes, Does.Contain("STEP-ANGLE-UNIT"));
        Assert.That(codes, Does.Contain("STEP-UNCERTAINTY"));
        Assert.That(codes, Has.None.EqualTo("STEP-INSTANCE-SKIPPED"));
    }
}
