namespace Ara3D.IfcTypeGen;

/// <summary>
/// One explicit attribute's slot in a Part 21 record. Position is its index in the record of a simple
/// instance of the laid-out entity. OwnPosition is its index in the partial-entity record of Entity, which is
/// how a complex instance such as (NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.)) writes it. IsDerived marks a slot
/// that an entity of the instance redeclares in its DERIVE section; the file writes '*' there.
/// </summary>
public sealed record AttributeSlot(string Entity, ExpressAttribute Attribute, int Position, int OwnPosition, bool IsDerived);

/// <summary>
/// Computes Part 21 attribute positions from the entity graph (ISO 10303-21 12.2.5): a simple instance lists
/// the explicit attributes of every entity in its supertype graph, supertypes before subtypes, depth first in
/// SUBTYPE OF order, each entity once.
/// </summary>
public static class ExpressAttributeLayout
{
    public static IReadOnlyDictionary<string, ExpressEntity> ToEntityLookup(this ExpressSchema schema)
        => schema.Entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

    public static ExpressEntity GetEntity(this IReadOnlyDictionary<string, ExpressEntity> lookup, string name)
        => lookup.TryGetValue(name, out var e) ? e : throw new KeyNotFoundException($"Entity {name} is not declared in the schema");

    /// <summary>
    /// Every entity an instance of the given entities is, each once, in record order. One name gives the graph of a
    /// simple instance; the partial entities of a complex instance give the union of their graphs.
    /// </summary>
    public static IReadOnlyList<ExpressEntity> SupertypeGraph(this IReadOnlyDictionary<string, ExpressEntity> lookup, params string[] entities)
    {
        var order = new List<ExpressEntity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string name)
        {
            if (!seen.Add(name))
                return;
            var e = lookup.GetEntity(name);
            foreach (var s in e.Supertypes)
                Visit(s);
            order.Add(e);
        }

        foreach (var e in entities)
            Visit(e);
        return order;
    }

    /// <summary>
    /// The attribute slots of an instance of the given entities, in record order. For a simple instance pass its
    /// entity and use Position. For a complex instance pass all its partial entities and use OwnPosition within
    /// the slots of each partial entity; IsDerived then accounts for DERIVE redeclarations in any of them.
    /// </summary>
    public static IReadOnlyList<AttributeSlot> Layout(this IReadOnlyDictionary<string, ExpressEntity> lookup, params string[] entities)
    {
        var graph = lookup.SupertypeGraph(entities);
        var derived = graph
            .SelectMany(e => e.DerivedRedeclarations)
            .Select(lookup.Declaration)
            .OfType<(string Entity, string Attribute)>()
            .ToHashSet();

        var slots = new List<AttributeSlot>();
        foreach (var e in graph)
            for (var i = 0; i < e.Attributes.Count; i++)
                slots.Add(new(e.Name, e.Attributes[i], slots.Count, i, derived.Contains((e.Name, e.Attributes[i].Name))));
        return slots;
    }

    /// <summary>
    /// The entity that declares the explicit attribute a SELF\x.y reference names, searched in x and its
    /// supertypes. Null when no explicit attribute matches, as when y is itself derived in x.
    /// </summary>
    public static (string Entity, string Attribute)? Declaration(this IReadOnlyDictionary<string, ExpressEntity> lookup, ExpressAttributeRef r)
    {
        var matches = lookup.SupertypeGraph(r.Entity)
            .SelectMany(e => e.Attributes.Where(a => a.Name.Equals(r.Attribute, StringComparison.OrdinalIgnoreCase)).Select(a => (e.Name, a.Name)))
            .ToList();
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException($"SELF\\{r.Entity}.{r.Attribute} is ambiguous: declared by {string.Join(", ", matches.Select(m => m.Item1))}"),
        };
    }
}
