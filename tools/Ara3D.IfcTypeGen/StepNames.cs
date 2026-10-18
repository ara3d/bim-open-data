namespace Ara3D.IfcTypeGen;

/// <summary>
/// C# names for the lower_snake_case identifiers of a STEP long form: b_spline_surface_with_knots becomes
/// BSplineSurfaceWithKnots, trim_1 becomes Trim1. The generator checks that no two declarations meet in one name.
/// </summary>
public static class StepNames
{
    /// <summary>Members of StepEntityType and object that a generated attribute field must not hide.</summary>
    public static readonly IReadOnlySet<string> ReservedMembers = new HashSet<string>
    {
        "Instance", "EntityName", "Attributes", "ToString", "Equals", "GetHashCode", "GetType", "MemberwiseClone", "Finalize",
    };

    public static string ToPascal(this string name)
        => string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));

    public static string Capitalize(string part)
        => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();

    public static string ToInterface(this string name)
        => "I" + name.ToPascal();

    /// <summary>
    /// The field name of each slot of an entity's record, in record order. An attribute name that two supertypes
    /// both declare (cartesian_transformation_operator_3d has two names) is qualified by its declaring entity on
    /// every slot that shares it; a name that would equal the class or a reserved member gets an Attribute suffix.
    /// </summary>
    public static IReadOnlyList<string> FieldNames(this IReadOnlyList<AttributeSlot> slots, string className)
    {
        var plain = slots.Select(s => s.Attribute.Name.ToPascal()).ToList();
        var shared = plain.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var names = slots
            .Select((s, i) => shared.Contains(plain[i]) ? s.Entity.ToPascal() + plain[i] : plain[i])
            .Select(n => n == className || ReservedMembers.Contains(n) ? n + "Attribute" : n)
            .ToList();
        if (names.Distinct().Count() != names.Count)
            throw new InvalidOperationException($"{className}: field names collide: {string.Join(", ", names)}");
        return names;
    }

    /// <summary>The enumeration item a Part 21 file writes as .B_SPLINE., matched without underscores or case.</summary>
    public static string ToMatchKey(this string item)
        => item.Replace("_", "").ToUpperInvariant();
}
