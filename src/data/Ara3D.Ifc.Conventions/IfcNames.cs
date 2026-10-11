namespace Ara3D.Ifc.Conventions;

/// <summary>An entity's BOS name and the room number its naming moved out of the name.</summary>
/// <param name="Name">The entity's name; null when it has none.</param>
/// <param name="RoomNumber">For an IFCSPACE, its non-empty Name, which authoring tools fill with
/// the room number; the reader stores it as <see cref="IfcParameterNames.RoomNumber"/>. Otherwise null.</param>
public readonly record struct IfcEntityName(string? Name, string? RoomNumber);

/// <summary>How a reader names an entity from its IFC attributes, whatever the source: a STEP
/// file (attributes by schema position) or That Open's Fragments (attributes by name).</summary>
public static class IfcNames
{
    /// <summary>The attributes that name an instance of their class, in the order they are tried:
    /// IfcRoot.Name and the Name of the resource classes that have one (materials, layers,
    /// presentation layers, organisations, units), IfcMaterialLayerSet.LayerSetName,
    /// IfcProfileDef.ProfileName, and IfcApplication.ApplicationFullName. No IFC class has two
    /// of them, so the first a class has is its name attribute.</summary>
    public static readonly IReadOnlyList<string> NameAttributes = [IfcParameterNames.NameAttribute, "LayerSetName", "ProfileName", "ApplicationFullName"];

    /// <summary>IfcSpace.LongName, which a space is named by when it has one.</summary>
    public const string LongNameAttribute = "LongName";

    public const string SpaceClass = "IFCSPACE";

    /// <summary>The entity's name: IfcSpace.LongName when it is non-empty (exporters put the room
    /// number in Name, which then becomes <see cref="IfcEntityName.RoomNumber"/>), else the first of
    /// <see cref="NameAttributes"/> that has a value.</summary>
    /// <param name="ifcClass">The entity's IFC class, upper case (<c>IFCSPACE</c>).</param>
    /// <param name="stringAttribute">The decoded text of the named attribute, or null when the
    /// class has no such attribute or the file gives it no string: no id, reference, number, or
    /// list stands in for a name. A stored '' is "", a value.</param>
    public static IfcEntityName NameOf(string? ifcClass, Func<string, string?> stringAttribute)
    {
        var name = NameAttributes.Select(stringAttribute).FirstOrDefault(s => s != null);
        if (ifcClass != SpaceClass)
            return new(name, null);
        var roomNumber = string.IsNullOrEmpty(name) ? null : name;
        return stringAttribute(LongNameAttribute) is { Length: > 0 } longName
            ? new(longName, roomNumber)
            : new(name, roomNumber);
    }
}
