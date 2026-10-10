namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// How one BOS parameter descriptor appears as a USD property (an attribute, or a
/// relationship for an entity reference): its full name
/// (bim:param:{group}:{name}), its USD value type, and the descriptor's own strings, which
/// the attribute name can only approximate.
/// </summary>
/// <param name="Group">The descriptor's group as stored in BOS; null when missing.</param>
/// <param name="BosName">The descriptor's name as stored in BOS; null when missing.</param>
/// <param name="Units">The descriptor's units; null when missing or empty.</param>
internal sealed record ParameterAttribute(string Name, ParameterType Type, string UsdType, string? Group, string? BosName, string? Units)
{
    /// <summary>One entry per descriptor, by descriptor index. Null for a descriptor whose
    /// parameter type this exporter does not know; its values are left out. Names are unique
    /// across descriptors, so no prim can get two attributes with one name: a collision
    /// (two groups or names that differ only in characters an identifier cannot hold, or one
    /// name with two value types) appends "_{descriptor index}" to the later name.</summary>
    public static ParameterAttribute?[] ForDescriptors(IBimData data)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new ParameterAttribute?[data.Descriptors.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var d = data.Descriptors[i];
            if (UsdTypeOf(d.Type) is not { } usdType)
                continue;
            var group = BosValues.String(data, d.Group);
            var name = BosValues.String(data, d.Name);
            var groupId = UsdNames.ToIdentifier(group ?? "");
            var nameId = UsdNames.ToIdentifier(name ?? "");
            var baseId = nameId;
            for (var attempt = 1; !used.Add(FullName(groupId, nameId)); attempt++)
                nameId = baseId + "_" + i + (attempt == 1 ? "" : "_" + attempt);
            result[i] = new ParameterAttribute(FullName(groupId, nameId), d.Type, usdType,
                group, name, BosValues.NonEmptyString(data, d.Units));
        }
        return result;
    }

    private static string FullName(string groupId, string nameId)
        => UsdNames.ParameterNamespace + groupId + ":" + nameId;

    /// <summary>The USD value type for a BOS parameter type. An entity reference is a
    /// relationship ("rel") targeting the referenced entity's prim.</summary>
    public static string? UsdTypeOf(ParameterType type) => type switch
    {
        ParameterType.Int => "int",
        ParameterType.Number => "float",
        ParameterType.String => "string",
        ParameterType.Entity => "rel",
        ParameterType.Point => "point3f",
        _ => null,
    };

    /// <summary>The /Model/Descriptors scope: each attribute in <paramref name="written"/>
    /// declared once, without a value, with the BOS group and name as displayGroup and
    /// displayName and the BOS type and units in customData. Element prims carry only values,
    /// so these strings are not repeated on every prim. Returns the number declared.</summary>
    public static int WriteDescriptors(UsdaWriter w, IEnumerable<ParameterAttribute> written)
    {
        var count = 0;
        w.Line().Text("def Scope \"").Text(UsdNames.DescriptorsScope).Text('"').End().Open();
        foreach (var a in written)
        {
            w.Line().Text("custom ").Text(a.UsdType).Text(' ').Text(a.Name).OpenMetadata();
            w.Line().Text("customData = {").End().Nest();
            w.Line().Text("string bosType = \"").Text(a.Type.ToString()).Text('"').End();
            if (a.Units is not null)
                w.Line().Text("string units = ").Quoted(a.Units).End();
            w.Close();
            if (a.Group is not null)
                w.Line().Text("displayGroup = ").Quoted(a.Group).End();
            if (a.BosName is not null)
                w.Line().Text("displayName = ").Quoted(a.BosName).End();
            w.CloseMetadata();
            count++;
        }
        w.Close();
        return count;
    }
}
