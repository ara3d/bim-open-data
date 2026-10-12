using System.Globalization;
using Ara3D.BimOpenSchema.DataModel;
using Ara3D.Ifc.Conventions;

namespace Ara3D.Ids;

/// <summary>A model's project units in SI, as the IFC converter records them on the project
/// entity; each is null when the model records none. IFC declares the area and volume units
/// apart from the length unit (a metric Revit export has millimetres and square metres), so an
/// area is never scaled by the length unit squared.</summary>
public sealed record IdsUnitScales(double? LengthToMetre, double? AreaToSquareMetre, double? VolumeToCubicMetre)
{
    /// <summary>SI per model unit for a power of length (1 a length, 2 an area, 3 a volume), or
    /// null when the model records no such unit.</summary>
    public double? ToSi(int power)
        => power switch
        {
            1 => LengthToMetre,
            2 => AreaToSquareMetre,
            3 => VolumeToCubicMetre,
            _ => null,
        };

    /// <summary>True when the length unit is the metre, so an undeclared area or volume unit and
    /// a number of unknown measure are taken as SI.</summary>
    public bool InMetres
        => LengthToMetre is { } metres && Math.Abs(metres - 1) <= 1e-9;
}

/// <summary>Brings a model's numbers into the SI units IDS 1.0 writes its values in (metres,
/// square metres, cubic metres) before a constraint compares them. A number is converted when
/// its unit can be told: a unit stated on the parameter (the descriptor's Units column), else a
/// length attribute in <see cref="LengthAttributes"/>, else the property facet's <c>dataType</c>,
/// scaled by the model's recorded length, area or volume unit (<see cref="IdsUnitScales"/>). A
/// number whose unit cannot be told is compared as stored, with a warning naming it, unless the
/// model is in metres; so is a length, area or volume on a model that records no unit of that
/// kind and is not in metres.</summary>
public static class IdsUnits
{
    /// <summary>IFC attributes whose type is a length measure (IfcLengthMeasure,
    /// IfcPositiveLengthMeasure, IfcNonNegativeLengthMeasure), by attribute name as BOS stores it
    /// after <c>Ifc:</c>. The name alone decides, whatever the class, so list a name only when no
    /// IFC class uses it for anything but a length (PanelWidth, a ratio, is left out). To extend:
    /// add the attribute name here; an attribute facet over a name not listed gets the
    /// not-converted warning on a model not in metres.</summary>
    public static readonly IReadOnlySet<string> LengthAttributes = new HashSet<string>(
    [
        // Doors and windows; storeys, sites, buildings and spaces.
        "OverallWidth", "OverallHeight", "Elevation", "RefElevation", "ElevationOfRefHeight",
        "ElevationOfTerrain", "ElevationWithFlooring",
        // Material layers and their usage.
        "LayerThickness", "OffsetFromReferenceLine",
        // Stair flights (IFC2x3), reinforcement.
        "RiserHeight", "TreadLength", "NominalDiameter", "BarLength",
        // Profiles and swept solids.
        "XDim", "YDim", "Radius", "Depth", "Width", "Thickness", "WallThickness", "FlangeThickness",
        "WebThickness", "OverallDepth", "FilletRadius", "EdgeRadius", "FlangeEdgeRadius",
        "InternalFilletRadius", "Girth",
        // Door and window lining and panel properties.
        "LiningDepth", "LiningThickness", "ThresholdDepth", "ThresholdThickness", "TransomThickness",
        "TransomOffset", "LiningOffset", "ThresholdOffset", "CasingThickness", "CasingDepth",
        "LiningToPanelOffsetX", "LiningToPanelOffsetY", "PanelDepth", "MullionThickness",
    ], StringComparer.Ordinal);

    /// <summary>The power of length an IDS <c>dataType</c> carries: 1 for a length, 2 for an area,
    /// 3 for a volume, 0 for a plain number; null for any other measure (a mass, an angle), whose
    /// unit the model's recorded units do not tell.</summary>
    public static int? LengthPower(string? dataType)
        => dataType?.Trim().ToUpperInvariant() switch
        {
            "IFCLENGTHMEASURE" or "IFCPOSITIVELENGTHMEASURE" or "IFCNONNEGATIVELENGTHMEASURE" => 1,
            "IFCAREAMEASURE" => 2,
            "IFCVOLUMEMEASURE" => 3,
            "IFCREAL" or "IFCINTEGER" or "IFCNUMBER" or "IFCPOSITIVEINTEGER" or "IFCCOUNTMEASURE" or "IFCNUMERICMEASURE"
                or "IFCRATIOMEASURE" or "IFCPOSITIVERATIOMEASURE" or "IFCNORMALISEDRATIOMEASURE" => 0,
            _ => null,
        };

    /// <summary>1 for an attribute in <see cref="LengthAttributes"/>, else null.</summary>
    public static int? AttributeLengthPower(string attributeName)
        => LengthAttributes.Contains(attributeName) ? 1 : null;

    /// <summary>The value in SI when <paramref name="units"/> names a length, area or volume unit
    /// (m, mm, cm, ft, in, m2, ft2, m3, ft3 and their spellings); null otherwise.</summary>
    public static double? FromStatedUnits(double value, string units)
        => units.Length == 0 ? null : TextNormalization.CanonicalNumber(value, TextNormalization.UnitKey(units)).Value;

    /// <summary>The candidate a constraint compares: a number in SI when its unit can be told
    /// (<paramref name="power"/> is the facet's reading of it, null when it has none), otherwise
    /// the value as stored, with a warning naming <paramref name="subject"/> when the model is not
    /// known to be in metres. Text and logicals pass through.</summary>
    internal static object Candidate(IdsValue value, int? power, IdsUnitScales units, string subject, IdsWarnings warnings)
    {
        if (value.Candidate is not double number)
            return value.Candidate;
        if (FromStatedUnits(number, value.Units) is { } stated)
            return stated;
        if (power is 0)
            return number;
        if (power is { } p && units.ToSi(p) is { } scale)
            return number * scale;
        if (units.InMetres)
            return number;
        warnings.Add(NotConverted(subject, power, units));
        return number;
    }

    private static string NotConverted(string subject, int? power, IdsUnitScales units)
        => $"{subject} is compared as stored, not converted to SI: " + (power switch
        {
            2 => $"the model records no area unit ({IfcParameterNames.AreaUnitToSquareMetre}) and is not in metres; IFC declares the area unit apart from the length unit",
            3 => $"the model records no volume unit ({IfcParameterNames.VolumeUnitToCubicMetre}) and is not in metres; IFC declares the volume unit apart from the length unit",
            _ when units.LengthToMetre is null => $"the model records no length unit ({IfcParameterNames.LengthUnitToMetre})",
            _ => $"its unit is not known (1 model length unit = {units.LengthToMetre.Value.ToString(CultureInfo.InvariantCulture)} m); a property facet's dataType (IFCLENGTHMEASURE, say) or a Units column would tell it",
        });
}
