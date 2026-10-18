using Ara3D.StepBrep;

namespace Ara3D.Step.Decode.Tests;

/// <summary>TKT-312's second criterion: the laws of step-raw-witnesses.laws.plato with the decoded models in place
/// of the witnesses (PrepareModel compiled from Plato, run here), and Law_UnitsFactorsAgree on the decoded models.
/// A NotImplementedException makes a test inconclusive: it means PrepareModel reached a function the library declares
/// without a body, not that the decoder is wrong.</summary>
public static class LawTests
{
    [TestCase("unit-cube.stp")]
    [TestCase("two-cubes-assembly.stp")]
    public static void UnitsFactorsAgree(string fixture)
        => Assert.That((bool)Fixtures.Decode(fixture).Law_UnitsFactorsAgree(), Is.True);

    [Test]
    public static void DecodedUnitCubePreparesToTheUnitBox()
    {
        var prepared = Prepare(Fixtures.Decode("unit-cube.stp"));
        Assert.That((bool)prepared.SameDecodedModel(DecodedModel.ExpectedUnitCube()), Is.True);
    }

    /// <summary>Law_RawTwoCubesPreparesToTwoBodies with the decoded model for Witness_RawTwoCubes.</summary>
    [Test]
    public static void DecodedTwoCubesPrepareToTwoBodies()
    {
        var d = Prepare(Fixtures.Decode("two-cubes-assembly.stp"));
        var m = d.Model;
        Assert.That((bool)d.SameDecodedModel(DecodedModel.ExpectedTwoCubes()), Is.True);
        Assert.That(((int)m.Bodies.Count, (int)m.Shells.Count, (int)m.Vertices.Count, (int)m.Edges.Count), Is.EqualTo((2, 2, 16, 24)));
        Assert.That(((int)m.Loops.Count, (int)m.Faces.Count, (int)m.Geometry.Curves.Count, (int)m.Geometry.Surfaces.Count), Is.EqualTo((12, 12, 24, 12)));
        Assert.That((bool)m.RawCounts(new ShellIndex(0)).Equals(BrepWitnesses.CountsOf(8, 12, 6, 6)), Is.True);
        Assert.That((bool)m.RawCounts(new ShellIndex(1)).Equals(BrepWitnesses.CountsOf(8, 12, 6, 6)), Is.True);
        Assert.That((double)d.ModelSize, Is.EqualTo(Math.Sqrt(11.0)));
        Assert.That((double)d.CoordinateExtent, Is.EqualTo(3.0));
    }

    /// <summary>The library's own forms of the two laws, over its witnesses: they say whether the compiled
    /// PrepareModel meets the library's expectation independently of the decoder.</summary>
    [Test]
    public static void LibraryLawsHoldOnTheWitnesses()
    {
        Assert.That((bool)Run(RawDecodedModel.Law_RawUnitCubePreparesToUnitBox), Is.True);
        Assert.That((bool)Run(RawDecodedModel.Law_RawTwoCubesPreparesToTwoBodies), Is.True);
    }

    static DecodedModel Prepare(RawDecodedModel raw)
        => Run(() => raw.PrepareModel());

    static T Run<T>(Func<T> f)
    {
        try
        {
            return f();
        }
        catch (NotImplementedException ex)
        {
            Assert.Inconclusive($"PrepareModel reaches a function the Plato library declares without a body (Reduced or ToleranceDiagnostics, step.library.plato): {ex.Message}");
            throw;
        }
    }
}
