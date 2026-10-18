using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>Reads a geometric representation context's length unit and length uncertainty (STEP-BREP-MODEL.md
/// section 3.1). Units are complex instances, (LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.)), and a
/// conversion_based_unit is followed through its conversion_factor to an SI unit.</summary>
public static class StepUnitsReader
{
    /// <summary>The scale assumed when a context names no length unit: millimetres, the unit of nearly every
    /// mechanical CAD export. Reading records the assumption.</summary>
    public const double DefaultMetresPerUnit = 0.001;

    const int MaxConversionDepth = 8;

    /// <summary>The context's units, or null when it assigns no length unit. Several uncertainties: the smallest
    /// length one, converted through its own unit into the context's length unit.</summary>
    public static StepUnits? ReadUnits(this StepEntity context)
    {
        var units = context.Refs(GlobalUnitAssignedContext.Instance.Units) ?? [];
        var metresPerUnit = LengthUnitScale(units);
        if (metresPerUnit is not { } scale)
            return null;
        var uncertainties = context.Refs(GlobalUncertaintyAssignedContext.Instance.Uncertainty) ?? [];
        double? smallest = null;
        foreach (var u in uncertainties)
            if (UncertaintyInMetres(u) is { } metres && (smallest is null || metres / scale < smallest))
                smallest = metres / scale;
        return new StepUnits(scale, smallest);
    }

    /// <summary>Metres per unit of a length unit: an si_unit named metre with its prefix, or a
    /// conversion_based_unit whose factor resolves to one. Null for any other unit.</summary>
    public static double? MetresPerUnit(this StepEntity unit)
        => unit.MetresPerUnit(0);

    static double? MetresPerUnit(this StepEntity unit, int depth)
    {
        if (depth > MaxConversionDepth)
            return null;
        if (unit.Is<ISiUnit>())
            return unit.Enum(SiUnit.Instance.Name) == SiUnitName.Metre ? PrefixFactor(unit.Enum(SiUnit.Instance.Prefix)) : null;
        if (!unit.Is<IConversionBasedUnit>() || unit.Ref(ConversionBasedUnit.Instance.ConversionFactor) is not { } factor)
            return null;
        var value = factor.Measure(MeasureWithUnit.Instance.ValueComponent);
        var baseScale = factor.Ref(MeasureWithUnit.Instance.UnitComponent)?.MetresPerUnit(depth + 1);
        return value * baseScale;
    }

    static double? LengthUnitScale(IReadOnlyList<StepEntity> units)
    {
        foreach (var u in units)
            if (u.Is<ILengthUnit>() && u.MetresPerUnit() is { } scale)
                return scale;
        foreach (var u in units)
            if (u.MetresPerUnit() is { } scale)
                return scale;
        return null;
    }

    static double? UncertaintyInMetres(StepEntity uncertainty)
        => uncertainty.Measure(MeasureWithUnit.Instance.ValueComponent)
            * uncertainty.Ref(MeasureWithUnit.Instance.UnitComponent)?.MetresPerUnit();

    static double PrefixFactor(SiPrefix? prefix)
        => prefix switch
        {
            null => 1,
            SiPrefix.Exa => 1e18,
            SiPrefix.Peta => 1e15,
            SiPrefix.Tera => 1e12,
            SiPrefix.Giga => 1e9,
            SiPrefix.Mega => 1e6,
            SiPrefix.Kilo => 1e3,
            SiPrefix.Hecto => 1e2,
            SiPrefix.Deca => 1e1,
            SiPrefix.Deci => 1e-1,
            SiPrefix.Centi => 1e-2,
            SiPrefix.Milli => 1e-3,
            SiPrefix.Micro => 1e-6,
            SiPrefix.Nano => 1e-9,
            SiPrefix.Pico => 1e-12,
            SiPrefix.Femto => 1e-15,
            SiPrefix.Atto => 1e-18,
            _ => 1,
        };
}
