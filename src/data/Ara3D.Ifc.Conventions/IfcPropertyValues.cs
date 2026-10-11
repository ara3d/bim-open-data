namespace Ara3D.Ifc.Conventions;

/// <summary>How a property or quantity's value is found and typed. A property becomes a BOS
/// parameter named by the property's Name and grouped by its set's Name.</summary>
public static class IfcPropertyValues
{
    /// <summary>The attributes that hold the value of a property (IfcPropertySingleValue and its
    /// siblings) or a quantity (IfcQuantityLength and its siblings); the first one present wins.</summary>
    public static readonly IReadOnlyList<string> ValueAttributes =
    [
        "NominalValue", "LengthValue", "AreaValue", "VolumeValue", "CountValue", "WeightValue",
        "TimeValue", "EnumerationValues", "ListValues", "LowerBoundValue", "UpperBoundValue",
    ];

    /// <summary>IFC defined types whose values are integers. A reader that knows a value's
    /// defined type writes a number of one of these types as an Int parameter, any other number
    /// as a Number parameter.</summary>
    public static readonly IReadOnlySet<string> IntegerTypes = new HashSet<string>(
    [
        "IFCINTEGER", "IFCPOSITIVEINTEGER", "IFCDIMENSIONCOUNT", "IFCYEARNUMBER", "IFCMONTHINYEARNUMBER",
        "IFCDAYINMONTHNUMBER", "IFCDAYINWEEKNUMBER", "IFCHOURINDAY", "IFCMINUTEINHOUR",
    ], StringComparer.Ordinal);

    public static bool IsIntegerType(string? ifcType)
        => ifcType != null && IntegerTypes.Contains(ifcType);
}
