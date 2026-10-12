namespace Ara3D.Ids;

/// <summary>Finds an occurrence's type object among one model's entities, on first use, so a
/// check with no property facet never builds the name index. An entity that knows its type's
/// EntityIndex (BOS <c>Entity.Type</c>, or a <c>TypeIndex</c> column) is found by index. An entity
/// that knows only the type's name (the <c>EntityText</c> view's <c>Type</c> column) is found by
/// name among the type objects (<see cref="IsTypeObjectClass"/>) that have no type of their own:
/// names repeat (the Duplex has two door styles named "0864 x 2032mm"), so several entities of
/// one name count as one type only when they carry the same property values, and otherwise the
/// type is unresolved.</summary>
internal sealed class IdsTypeObjects
{
    private readonly Func<IdsModel> _model;
    private readonly Lazy<Dictionary<string, IdsEntity?>> _byName;

    /// <param name="model">The model the entities end up in; called on first use, after it exists.</param>
    public IdsTypeObjects(Func<IdsModel> model)
    {
        _model = model;
        _byName = new(() => ByName(_model().Entities));
    }

    /// <summary>The entity's type object, or null when it has none or it cannot be told.</summary>
    public IdsEntity? Of(IdsEntity entity)
        => entity.TypeIndex >= 0 ? _model().Entity(entity.TypeIndex)
            : entity.TypeName.Length > 0 ? _byName.Value.GetValueOrDefault(entity.TypeName)
            : null;

    /// <summary>Why the entity names a type that <see cref="Of"/> cannot give, or null when it
    /// names none or the type is found.</summary>
    public string? Unresolved(IdsEntity entity)
        => !entity.HasType || Of(entity) is not null ? null
            : entity.TypeIndex < 0 && _byName.Value.ContainsKey(entity.TypeName)
                ? $"its type '{entity.TypeName}' names several entities with different property values"
                : entity.TypeIndex < 0
                    ? $"its type '{entity.TypeName}' is not in the entities table as a type object"
                    : $"its type '{entity.TypeName}' (entity {entity.TypeIndex}) is not in the entities table";

    /// <summary>For each name some entity gives as its type, the one type entity of that name, or
    /// null when several of that name carry different property values.</summary>
    private static Dictionary<string, IdsEntity?> ByName(IReadOnlyList<IdsEntity> entities)
    {
        var named = entities.Where(e => e.TypeIndex < 0 && e.TypeName.Length > 0).Select(e => e.TypeName).ToHashSet();
        return entities
            .Where(e => !e.HasType && named.Contains(e.Name) && IsTypeObjectClass(e.Category))
            .GroupBy(e => e.Name)
            .ToDictionary(g => g.Key, g => g.Skip(1).All(e => SameProperties(e, g.First())) ? g.First() : null);
    }

    /// <summary>True for an IFC class that is an IfcTypeObject: IfcTypeObject, IfcTypeProduct,
    /// IfcTypeProcess and IfcTypeResource, every subtype named ...Type (IfcDoorType), and the IFC2x3
    /// IfcDoorStyle and IfcWindowStyle. IfcRelDefinesByType ends in TYPE but is a relation, and
    /// presentation styles (IfcSurfaceStyle) end in STYLE but are not type objects. Without this a
    /// space or material that shares a type's name would give an occurrence its properties.
    /// Ara3D.Ifc.Conventions' IfcTypeObjects tells a type object by its attributes, which a
    /// table of entities does not carry; only the class name is known here.</summary>
    internal static bool IsTypeObjectClass(string category)
        => category.StartsWith("IFCTYPE", StringComparison.OrdinalIgnoreCase)
            || category.EndsWith("TYPE", StringComparison.OrdinalIgnoreCase) && !category.StartsWith("IFCREL", StringComparison.OrdinalIgnoreCase)
            || category.Equals("IFCDOORSTYLE", StringComparison.OrdinalIgnoreCase)
            || category.Equals("IFCWINDOWSTYLE", StringComparison.OrdinalIgnoreCase);

    private static bool SameProperties(IdsEntity a, IdsEntity b)
        => Key(a).SequenceEqual(Key(b));

    private static IEnumerable<(string, string, string)> Key(IdsEntity entity)
        => entity.OwnProperties.Select(v => (v.Group, v.Name, v.Text)).Order();
}
