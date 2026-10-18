using Ara3D.IO.StepParser;

namespace Ara3D.Step.Mesher.Tests.Model;

public static class UnitsTests
{
    const string InchContext = """
        #1=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
        #2=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(25.4),#1);
        #3=(CONVERSION_BASED_UNIT('INCH',#2)LENGTH_UNIT()NAMED_UNIT(*));
        #4=UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(0.001),#3,'distance_accuracy_value','');
        #5=(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.));
        #6=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#4))GLOBAL_UNIT_ASSIGNED_CONTEXT((#5,#3))REPRESENTATION_CONTEXT('',''));
        """;

    [Test]
    public static void UnitCubeIsInMillimetres()
    {
        using var document = new StepDocument(StepText.Fixture("unit-cube.stp"));
        var units = document.Entity(132).ReadUnits();
        Assert.That(units, Is.Not.Null);
        Assert.That(units!.Value.MetresPerUnit, Is.EqualTo(0.001).Within(1e-15));
        Assert.That(units.Value.Uncertainty, Is.EqualTo(1e-6).Within(1e-18));
    }

    [Test]
    public static void InchIsReadThroughItsConversionFactor()
    {
        using var document = StepText.Parse(InchContext);
        var units = document.Entity(6).ReadUnits()!.Value;
        Assert.That(units.MetresPerUnit, Is.EqualTo(0.0254).Within(1e-12));
        Assert.That(units.Uncertainty, Is.EqualTo(0.001).Within(1e-12));
    }

    [Test]
    public static void Ap203StoresTheDimensionsOfAConversionBasedUnit()
    {
        using var document = StepText.Parse(InchContext.Replace("#3=(CONVERSION_BASED_UNIT('INCH',#2)LENGTH_UNIT()NAMED_UNIT(*));",
            "#7=DIMENSIONAL_EXPONENTS(1.,0.,0.,0.,0.,0.,0.);\n#3=(CONVERSION_BASED_UNIT('INCH',#2)LENGTH_UNIT()NAMED_UNIT(#7));"));
        Assert.That(document.Entity(6).ReadUnits()!.Value.MetresPerUnit, Is.EqualTo(0.0254).Within(1e-12));
    }

    [Test]
    public static void AContextWithoutALengthUnitReadsNull()
    {
        using var document = StepText.Parse("""
            #5=(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.));
            #6=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT((#5))REPRESENTATION_CONTEXT('',''));
            """);
        Assert.That(document.Entity(6).ReadUnits(), Is.Null);
    }
}
