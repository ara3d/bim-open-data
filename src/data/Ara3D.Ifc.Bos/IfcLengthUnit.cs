using Ara3D.Ifc.Conventions;
using Ara3D.IfcLoader;
using Ara3D.IO.StepParser;

namespace Ara3D.BimOpenSchema.IO;

/// <summary>The project's declared length unit: the LENGTHUNIT of IfcProject.UnitsInContext.
/// The same reader gives the AREAUNIT and VOLUMEUNIT scales, which IFC declares apart from the
/// length unit (<see cref="AreaToSquareMetre"/>, <see cref="VolumeToCubicMetre"/>).</summary>
public readonly record struct IfcLengthUnit(string Name, double ToMetre)
{
    /// <summary>Kept for callers; see <see cref="IfcParameterNames.LengthUnit"/>.</summary>
    public const string NameParameter = IfcParameterNames.LengthUnit;
    /// <summary>Kept for callers; see <see cref="IfcParameterNames.LengthUnitToMetre"/>.</summary>
    public const string ScaleParameter = IfcParameterNames.LengthUnitToMetre;

    private const int ProjectUnitsInContextIndex = 8;
    private const int NamedUnitTypeIndex = 1;
    private const int SIUnitPrefixIndex = 2;
    private const int SIUnitNameIndex = 3;
    private const int ConversionBasedUnitNameIndex = 2;
    private const int ConversionBasedUnitFactorIndex = 3;
    private const int MeasureWithUnitValueIndex = 0;
    private const int MeasureWithUnitUnitIndex = 1;

    private const string LengthUnitType = "LENGTHUNIT";
    private const string AreaUnitType = "AREAUNIT";
    private const string VolumeUnitType = "VOLUMEUNIT";

    private static readonly Dictionary<string, double> SiPrefixScales = new(StringComparer.Ordinal)
    {
        ["EXA"] = 1e18,
        ["PETA"] = 1e15,
        ["TERA"] = 1e12,
        ["GIGA"] = 1e9,
        ["MEGA"] = 1e6,
        ["KILO"] = 1e3,
        ["HECTO"] = 1e2,
        ["DECA"] = 1e1,
        ["DECI"] = 1e-1,
        ["CENTI"] = 1e-2,
        ["MILLI"] = 1e-3,
        ["MICRO"] = 1e-6,
        ["NANO"] = 1e-9,
        ["PICO"] = 1e-12,
        ["FEMTO"] = 1e-15,
        ["ATTO"] = 1e-18,
    };

    /// <summary>Null when the file declares no length unit.</summary>
    public static IfcLengthUnit? Read(IfcFile file)
        => ReadUnit(file, LengthUnitType, 1);

    /// <summary>Square metres per the project's area unit; null when the file declares none.</summary>
    public static double? AreaToSquareMetre(IfcFile file)
        => ReadUnit(file, AreaUnitType, 2)?.ToMetre;

    /// <summary>Cubic metres per the project's volume unit; null when the file declares none.</summary>
    public static double? VolumeToCubicMetre(IfcFile file)
        => ReadUnit(file, VolumeUnitType, 3)?.ToMetre;

    /// <summary>The project's unit of one type, with its scale in SI (metres to the
    /// <paramref name="power"/>); null when the file declares none.</summary>
    private static IfcLengthUnit? ReadUnit(IfcFile file, string unitType, int power)
    {
        var project = file.EntityResolver.GetEntities().FirstOrDefault(e => e.GetEntityName() == "IFCPROJECT");
        if (project == null)
            return null;

        var unitsInContextId = project.GetId(ProjectUnitsInContextIndex);
        var unitAssignment = file.EntityResolver.GetEntityOrDefault(unitsInContextId);
        if (unitAssignment == null)
            return null;

        foreach (var unitId in unitAssignment.GetIdList(0))
        {
            var unitEntity = file.EntityResolver.GetEntityOrDefault(unitId);
            if (unitEntity == null)
                continue;
            var result = TryReadUnit(file, unitEntity, unitType, power);
            if (result != null)
                return result;
        }

        return null;
    }

    private static IfcLengthUnit? TryReadUnit(IfcFile file, IfcEntity unit, string unitType, int power)
    {
        var entityName = unit.GetEntityName();
        if (entityName == "IFCSIUNIT")
        {
            if (StripEnumMarkers(unit.GetStringOrEmpty(NamedUnitTypeIndex)) != unitType)
                return null;
            return ReadSiUnit(unit, power);
        }

        if (entityName == "IFCCONVERSIONBASEDUNIT" || entityName == "IFCCONVERSIONBASEDUNITWITHOFFSET")
        {
            if (StripEnumMarkers(unit.GetStringOrEmpty(NamedUnitTypeIndex)) != unitType)
                return null;

            var name = unit.GetStringOrEmpty(ConversionBasedUnitNameIndex);
            var measure = file.EntityResolver.GetEntityOrDefault(unit.GetId(ConversionBasedUnitFactorIndex));
            if (measure == null)
                return null;

            var factor = ReadNumberAttribute(measure, MeasureWithUnitValueIndex);
            var baseUnit = file.EntityResolver.GetEntityOrDefault(measure.GetId(MeasureWithUnitUnitIndex));
            var baseToSi = baseUnit != null ? TryReadUnit(file, baseUnit, unitType, power)?.ToMetre ?? 1.0 : 1.0;
            return new IfcLengthUnit(name, factor * baseToSi);
        }

        return null;
    }

    /// <summary>The prefix scales the base unit before the power applies: MILLI SQUARE_METRE is a
    /// square millimetre, 1e-6 square metres.</summary>
    private static IfcLengthUnit ReadSiUnit(IfcEntity unit, int power)
    {
        var prefix = StripEnumMarkers(unit.GetStringOrEmpty(SIUnitPrefixIndex));
        var name = StripEnumMarkers(unit.GetStringOrEmpty(SIUnitNameIndex));
        var scale = string.IsNullOrEmpty(prefix) ? 1.0 : Math.Pow(SiPrefixScales.GetValueOrDefault(prefix, 1.0), power);
        var combinedName = string.IsNullOrEmpty(prefix) ? name : prefix + name;
        return new IfcLengthUnit(combinedName, scale);
    }

    private static double ReadNumberAttribute(IfcEntity entity, int index)
    {
        var token = entity.GetAttribute(index);
        if (token.IsEntity)
        {
            var (_, inner) = token.AsSimpleEntity(entity.Document);
            token = inner;
        }

        return token.IsNumber ? token.AsNumber() : 1.0;
    }

    /// <summary>STEP enumeration values are written as e.g. ".LENGTHUNIT.". GetStringOrEmpty strips
    /// quotes but not these surrounding dots, so callers comparing against a bare enum name trim them here.</summary>
    private static string StripEnumMarkers(string value)
        => value.Trim('.');
}
