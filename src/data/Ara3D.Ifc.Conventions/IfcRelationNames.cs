using Ara3D.BimOpenSchema;

namespace Ara3D.Ifc.Conventions;

/// <summary>An IFC relation class that becomes a BOS relation, with the inverse attribute that
/// reaches it from each end. The BOS relation points from the related object to the relating one:
/// a wall is ContainedIn its storey.</summary>
/// <param name="IfcClass">The relation's IFC class.</param>
/// <param name="RelationType">The BOS relation it becomes.</param>
/// <param name="RelatedInverse">The inverse attribute on the related object (the BOS source).</param>
/// <param name="RelatingInverse">The inverse attribute on the relating object (the BOS target).</param>
public sealed record IfcRelationConvention(string IfcClass, RelationType RelationType, string RelatedInverse, string RelatingInverse);

/// <summary>IFC relation and inverse attribute names, for a reader that sees relations as named
/// lists on each object (That Open's Fragments) rather than as relation entities.</summary>
public static class IfcRelationNames
{
    /// <summary>The relations read from inverse attributes. Ara3D.IfcLoader's IfcRelations reads
    /// the same classes from STEP and gives the same BOS relation types. HasAssociations reaches
    /// every IfcRelAssociates, not only the material one; a reader of inverse names cannot tell
    /// them apart and takes each as HasMaterial.</summary>
    public static readonly IReadOnlyList<IfcRelationConvention> Relations =
    [
        new("IFCRELCONTAINEDINSPATIALSTRUCTURE", RelationType.ContainedIn, "ContainedInStructure", "ContainsElements"),
        new("IFCRELAGGREGATES", RelationType.MemberOf, "Decomposes", "IsDecomposedBy"),
        new("IFCRELNESTS", RelationType.ChildOf, "Nests", "IsNestedBy"),
        new("IFCRELASSOCIATESMATERIAL", RelationType.HasMaterial, "HasAssociations", "AssociatedTo"),
    ];

    /// <summary>The BOS relation each related-side inverse name becomes.</summary>
    public static readonly IReadOnlyDictionary<string, RelationType> ByRelatedInverse =
        Relations.ToDictionary(r => r.RelatedInverse, r => r.RelationType, StringComparer.Ordinal);

    /// <summary>Relating-side inverse names a reader skips: those of <see cref="Relations"/>, which
    /// repeat the related side from the other end, and DefinesOccurrence (a property set's
    /// objects, read from IsDefinedBy) and ReferencesElements (a spatial element's referenced
    /// elements, read from ReferencedInStructures).</summary>
    public static readonly IReadOnlySet<string> SkippedRelatingInverses =
        new HashSet<string>(Relations.Select(r => r.RelatingInverse).Append(DefinesOccurrence).Append(ReferencesElements), StringComparer.Ordinal);

    /// <summary>On a type object: the objects it types (IfcRelDefinesByType, relating side).</summary>
    public const string ObjectTypeOf = "ObjectTypeOf";

    /// <summary>On an object: its property sets, quantity sets, and type (IfcRelDefinesByProperties
    /// and IfcRelDefinesByType, related side).</summary>
    public const string IsDefinedBy = "IsDefinedBy";

    /// <summary>On a type object: its own property sets (an attribute, not an inverse).</summary>
    public const string HasPropertySets = "HasPropertySets";

    /// <summary>On a property set definition: the objects it defines (IfcRelDefinesByProperties,
    /// relating side).</summary>
    public const string DefinesOccurrence = "DefinesOccurrence";

    /// <summary>On a spatial element: the elements it references (IfcRelReferencedInSpatialStructure,
    /// relating side).</summary>
    public const string ReferencesElements = "ReferencesElements";

    /// <summary>On an IfcPropertySet and an IfcElementQuantity: the attribute listing its members.</summary>
    public static readonly IReadOnlySet<string> SetMembers =
        new HashSet<string>(["HasProperties", "Quantities"], StringComparer.Ordinal);
}
