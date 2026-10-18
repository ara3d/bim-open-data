using Ara3D.Step.Entities;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>The size of a named unit in SI terms: metres per length unit, radians per plane-angle unit. An si_unit
/// gives its prefix's factor; a conversion_based_unit is followed through its conversion_factor (a
/// measure_with_unit) to the unit it is defined in. The length half repeats Ara3D.Step.Mesher's StepUnitsReader
/// (TKT-312 build log: the mesher's walk goes when it meshes through the decoder).</summary>
public static class StepUnitFactors
{
    const int MaxConversionDepth = 8;

    public static double? MetresPerUnit(this StepEntity unit)
        => unit.Factor(ST.SiUnitName.Metre, 0);

    public static double? RadiansPerUnit(this StepEntity unit)
        => unit.Factor(ST.SiUnitName.Radian, 0);

    /// <summary>The factor of a measure_with_unit's value: its value times its unit's factor in SI.</summary>
    public static double? MeasureIn(this StepEntity measure, ST.SiUnitName si)
        => measure.Measure(ST.MeasureWithUnit.Instance.ValueComponent)
            * measure.Ref(ST.MeasureWithUnit.Instance.UnitComponent)?.Factor(si, 0);

    static double? Factor(this StepEntity unit, ST.SiUnitName si, int depth)
    {
        if (depth > MaxConversionDepth)
            return null;
        if (unit.Is<ST.ISiUnit>())
            return unit.Enum(ST.SiUnit.Instance.Name) == si ? PrefixFactor(unit.Enum(ST.SiUnit.Instance.Prefix)) : null;
        if (!unit.Is<ST.IConversionBasedUnit>() || unit.Ref(ST.ConversionBasedUnit.Instance.ConversionFactor) is not { } factor)
            return null;
        return factor.Measure(ST.MeasureWithUnit.Instance.ValueComponent)
            * factor.Ref(ST.MeasureWithUnit.Instance.UnitComponent)?.Factor(si, depth + 1);
    }

    static double PrefixFactor(ST.SiPrefix? prefix)
        => prefix switch
        {
            ST.SiPrefix.Exa => 1e18,
            ST.SiPrefix.Peta => 1e15,
            ST.SiPrefix.Tera => 1e12,
            ST.SiPrefix.Giga => 1e9,
            ST.SiPrefix.Mega => 1e6,
            ST.SiPrefix.Kilo => 1e3,
            ST.SiPrefix.Hecto => 1e2,
            ST.SiPrefix.Deca => 1e1,
            ST.SiPrefix.Deci => 1e-1,
            ST.SiPrefix.Centi => 1e-2,
            ST.SiPrefix.Milli => 1e-3,
            ST.SiPrefix.Micro => 1e-6,
            ST.SiPrefix.Nano => 1e-9,
            ST.SiPrefix.Pico => 1e-12,
            ST.SiPrefix.Femto => 1e-15,
            ST.SiPrefix.Atto => 1e-18,
            _ => 1,
        };
}
