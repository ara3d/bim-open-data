namespace Ara3D.IfcTypeGen;

/// <summary>
/// The C# type a generated StepAttribute&lt;T&gt; carries for an EXPRESS type: a simple type becomes double, long,
/// bool, or string; an entity or a SELECT becomes its interface; an ENUMERATION its enum; a defined type
/// (length_measure = REAL) resolves to what it names; an aggregate of any kind becomes IReadOnlyList.
/// </summary>
public sealed class StepTypeMap
{
    public static readonly IReadOnlyDictionary<string, string> SimpleTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["REAL"] = "double",
        ["NUMBER"] = "double",
        ["INTEGER"] = "long",
        ["BOOLEAN"] = "bool",
        ["LOGICAL"] = "bool",
        ["STRING"] = "string",
        ["BINARY"] = "string",
    };

    private readonly IReadOnlyDictionary<string, ExpressTypeExpr> _aliases;
    private readonly IReadOnlySet<string> _interfaces;
    private readonly IReadOnlySet<string> _enums;

    public StepTypeMap(ExpressSchema schema)
    {
        _aliases = schema.Decls.OfType<ExpressTypeAlias>().ToDictionary(a => a.Name, a => a.TypeExpr, StringComparer.OrdinalIgnoreCase);
        _interfaces = schema.Entities.Select(e => e.Name)
            .Concat(schema.Decls.OfType<ExpressSelect>().Select(s => s.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _enums = schema.Decls.OfType<ExpressEnumeration>().Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string ToCSharp(ExpressTypeExpr type)
    {
        if (type.GenericParameters.Count > 0)
            return $"IReadOnlyList<{ToCSharp(type.GenericParameters.Single())}>";
        if (SimpleTypes.TryGetValue(type.Name, out var simple))
            return simple;
        if (_interfaces.Contains(type.Name))
            return type.Name.ToInterface();
        if (_enums.Contains(type.Name))
            return type.Name.ToPascal();
        if (_aliases.TryGetValue(type.Name, out var aliased))
            return ToCSharp(aliased);
        throw new NotSupportedException($"No C# type for EXPRESS type {type.Name}");
    }
}
