namespace Ara3D.Ifc.Conventions;

/// <summary>Names of the BOS parameters a reader writes for IFC attributes, as opposed to
/// property-set values (those are named by the property and grouped by the set). Each comment
/// gives the parameter type, the entity it sits on, and its group.</summary>
public static class IfcParameterNames
{
    /// <summary>Every IFC attribute parameter starts with this.</summary>
    public const string Prefix = "Ifc:";

    /// <summary>The parameter holding an IFC attribute: <c>Ifc:&lt;attribute&gt;</c>, grouped by the
    /// entity's IFC class.</summary>
    public static string Attribute(string attributeName) => Prefix + attributeName;

    /// <summary>String, on each IFCSPACE, group IFCSPACE: the space's Name (attribute 2), which
    /// authoring tools fill with the room number.</summary>
    public const string RoomNumber = Prefix + "Room:Number";

    /// <summary>String, on each IFCGRIDAXIS, group IFCGRIDAXIS: attribute 0, which is the tag
    /// because IfcGridAxis is not an IfcRoot.</summary>
    public const string AxisTag = Prefix + "AxisTag";

    /// <summary>String, on the IFCPROJECT entity, group IFCPROJECT: the project's length unit name.</summary>
    public const string LengthUnit = Prefix + "LengthUnit";

    /// <summary>Number, same entity and group as <see cref="LengthUnit"/>: metres per unit.</summary>
    public const string LengthUnitToMetre = Prefix + "LengthUnitToMetre";

    /// <summary>Entity, on each IfcMaterialLayer, grouped by the layer's class: its layer set.</summary>
    public const string LayerSet = Prefix + "LayerSet";

    /// <summary>Int, same entity and group as <see cref="LayerSet"/>: its 1-based position in the set.</summary>
    public const string LayerIndex = Prefix + "LayerIndex";

    /// <summary>Entity, on each IfcMaterialConstituent, grouped by its class: its constituent set.</summary>
    public const string ConstituentSet = Prefix + "ConstituentSet";

    /// <summary>Int, same entity and group as <see cref="ConstituentSet"/>: its 1-based position.</summary>
    public const string ConstituentIndex = Prefix + "ConstituentIndex";

    /// <summary>The IFC attribute that becomes the entity's Name rather than a parameter.</summary>
    public const string NameAttribute = "Name";

    /// <summary>The IFC attribute that becomes the entity's GlobalId rather than a parameter.</summary>
    public const string GlobalIdAttribute = "GlobalId";

    /// <summary>IfcRoot attributes that never become parameters: GlobalId and Name are entity
    /// columns, and OwnerHistory says who edited the file, not what the building is.</summary>
    public static readonly IReadOnlySet<string> NotParameters =
        new HashSet<string>([GlobalIdAttribute, "OwnerHistory", NameAttribute], StringComparer.Ordinal);
}
