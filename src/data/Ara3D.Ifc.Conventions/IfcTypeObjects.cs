namespace Ara3D.Ifc.Conventions;

/// <summary>Where a type object's own property sets go, and how a reader finds the values an
/// occurrence inherits from its type.
/// <para>An IfcTypeObject lists property sets in its <see cref="IfcRelationNames.HasPropertySets"/>
/// attribute. Each set's values become parameters on the type entity, named by the property and
/// grouped by the set, exactly as an occurrence's own sets are written; they are not copied onto
/// the occurrences. An occurrence points at its type through <c>Entity.Type</c>, so a reader that
/// wants what IDS 1.0 calls the occurrence's properties takes the occurrence's own parameters and
/// adds its type's, the occurrence's value winning for the same (set, property) pair.</para></summary>
public static class IfcTypeObjects
{
    /// <summary>The STEP attribute index of HasPropertySets on IfcTypeObject and every subtype, in
    /// IFC2x3, IFC4, and IFC4x3: after GlobalId, OwnerHistory, Name, Description, and
    /// ApplicableOccurrence.</summary>
    public const int HasPropertySetsAttribute = 5;

    /// <summary>True when a class with these attribute names, in STEP order, is an IfcTypeObject:
    /// its attribute <see cref="HasPropertySetsAttribute"/> is HasPropertySets.</summary>
    public static bool IsTypeObject(IReadOnlyList<string> attributeNames)
        => attributeNames.Count > HasPropertySetsAttribute
           && attributeNames[HasPropertySetsAttribute] == IfcRelationNames.HasPropertySets;
}
