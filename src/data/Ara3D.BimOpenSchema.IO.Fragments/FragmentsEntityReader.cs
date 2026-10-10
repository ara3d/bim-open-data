using System.Text.Json;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>Turns the items of a Fragments model into BOS entities, parameters, and relations,
/// with the conventions <c>Ara3D.Ifc.Bos</c> uses for the same IFC content: one category entity
/// per IFC class, attributes as <c>Ifc:&lt;name&gt;</c> parameters grouped by class, property-set
/// values as parameters named by the property and grouped by the set.</summary>
internal sealed class FragmentsEntityReader
{
    /// <summary>Relation names on the related side of an IFC relation, mapped to BOS relations
    /// from the item to its targets, as Ara3D.IfcLoader's IfcRelations maps the same IFC relations.</summary>
    private static readonly Dictionary<string, RelationType> RelatedSide = new()
    {
        ["ContainedInStructure"] = RelationType.ContainedIn,
        ["Decomposes"] = RelationType.MemberOf,
        ["Nests"] = RelationType.ChildOf,
        ["HasAssociations"] = RelationType.HasMaterial,
    };

    /// <summary>The relating side of the same relations: each repeats a related-side entry from
    /// the other end, so it adds nothing.</summary>
    private static readonly HashSet<string> RelatingSide =
    [
        "ContainsElements", "IsDecomposedBy", "IsNestedBy", "AssociatedTo",
        "DefinesOccurrence", "ReferencesElements",
    ];

    /// <summary>On a type: the instances it types (IfcRelDefinesByType, relating side).</summary>
    private const string ObjectTypeOf = "ObjectTypeOf";

    /// <summary>On an object: its property sets, quantity sets, and type.</summary>
    private const string IsDefinedBy = "IsDefinedBy";

    /// <summary>On a type: its own property sets.</summary>
    private const string HasPropertySets = "HasPropertySets";

    /// <summary>On a property set or a quantity set: its members.</summary>
    private static readonly HashSet<string> SetMembers = ["HasProperties", "Quantities"];

    /// <summary>The attribute holding a property's or quantity's value, first match wins.</summary>
    private static readonly string[] ValueAttributes =
    [
        "NominalValue", "LengthValue", "AreaValue", "VolumeValue", "CountValue", "WeightValue",
        "TimeValue", "EnumerationValues", "ListValues", "LowerBoundValue", "UpperBoundValue",
    ];

    /// <summary>IFC defined types whose values are integers; a number with one of these types
    /// becomes an Int parameter, any other number a Number parameter.</summary>
    private static readonly HashSet<string> IntegerTypes =
    [
        "IFCINTEGER", "IFCPOSITIVEINTEGER", "IFCDIMENSIONCOUNT", "IFCYEARNUMBER", "IFCMONTHINYEARNUMBER",
        "IFCDAYINMONTHNUMBER", "IFCDAYINWEEKNUMBER", "IFCHOURINDAY", "IFCMINUTEINHOUR",
    ];

    public const string IfcParameterPrefix = "Ifc:";
    public const string NameAttribute = "Name";

    private readonly FragmentsItems _items;
    private readonly BimDataBuilder _bdb;
    private readonly DocumentIndex _doc;
    private readonly EntityIndex[] _entityOfItem;
    private readonly Dictionary<string, int> _droppedTargets = new();

    public FragmentsEntityReader(FragmentsItems items, BimDataBuilder bdb, DocumentIndex doc)
    {
        _items = items;
        _bdb = bdb;
        _doc = doc;
        _entityOfItem = new EntityIndex[items.Count];
    }

    /// <summary>The BOS entity of each item, by item position.</summary>
    public IReadOnlyList<EntityIndex> EntityOfItem => _entityOfItem;

    public void Read()
    {
        var typeOfItem = TypesOfItems();
        AddEntities(typeOfItem);
        for (var i = 0; i < _items.Count; i++)
        {
            AddAttributeParameters(i);
            AddRelations(i, typeOfItem);
        }
        foreach (var problem in _items.Problems)
            _bdb.AddDiagnostic(DiagnosticType.ExporterWarning, problem, _doc, BimDataBuilder.InvalidEntityIndex);
        foreach (var (name, count) in _droppedTargets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            _bdb.AddDiagnostic(DiagnosticType.ExporterInfo,
                $"{count} targets of relation '{name}' are not items in the file and were dropped", _doc, BimDataBuilder.InvalidEntityIndex);
    }

    private int[] TypesOfItems()
    {
        var typeOf = Enumerable.Repeat(-1, _items.Count).ToArray();
        for (var t = 0; t < _items.Count; t++)
            foreach (var rel in _items.Relations[t].Where(r => r.Name == ObjectTypeOf))
                foreach (var instance in Items(rel))
                    typeOf[instance] = t;
        return typeOf;
    }

    private void AddEntities(int[] typeOfItem)
    {
        var categories = new Dictionary<string, EntityIndex>(StringComparer.Ordinal);
        foreach (var category in _items.Categories.Where(c => c != null).Distinct(StringComparer.Ordinal))
            categories.Add(category!, _bdb.AddEntity(-1, "", _doc, category!, BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex));

        // Types come first in no particular order, so every entity is added before any type link is set.
        for (var i = 0; i < _items.Count; i++)
            _entityOfItem[i] = _bdb.AddEntity();
        for (var i = 0; i < _items.Count; i++)
        {
            var category = _items.Categories[i] is { } c ? categories[c] : BimDataBuilder.InvalidEntityIndex;
            var type = typeOfItem[i] >= 0 ? _entityOfItem[typeOfItem[i]] : BimDataBuilder.InvalidEntityIndex;
            var name = _items.Attributes[i].FirstOrDefault(a => a.Name == NameAttribute && a.Value.ValueKind == JsonValueKind.String)?.Value.GetString();
            _bdb.UpdateEntity(_entityOfItem[i], _items.LocalIds[i], _items.GlobalIds[i] ?? "", _doc, name ?? "", category, type);
        }
    }

    private void AddAttributeParameters(int item)
    {
        var group = _items.Categories[item] ?? "";
        foreach (var a in _items.Attributes[item])
            if (a.Name != NameAttribute)
                AddValue(_entityOfItem[item], IfcParameterPrefix + a.Name, group, a.Value, a.IfcType);
    }

    private void AddRelations(int item, int[] typeOfItem)
    {
        var entity = _entityOfItem[item];
        var group = _items.Categories[item] ?? "";
        var seenProperties = new HashSet<(string Set, string Name, string Value)>();
        foreach (var rel in _items.Relations[item])
        {
            if (rel.Name == ObjectTypeOf || RelatingSide.Contains(rel.Name))
                continue;
            if (SetMembers.Contains(rel.Name))
            {
                // Read where the set is expanded, onto each object it defines; here only counted.
                CountAbsentTargets(rel);
                continue;
            }
            if (RelatedSide.TryGetValue(rel.Name, out var relationType))
            {
                foreach (var target in Items(rel))
                    _bdb.AddRelation(entity, _entityOfItem[target], relationType);
                continue;
            }
            if (rel.Name is IsDefinedBy or HasPropertySets)
            {
                // A type reached through IsDefinedBy is already the entity's Type.
                foreach (var set in Items(rel).Where(t => t != typeOfItem[item]))
                    AddSetProperties(entity, set, seenProperties);
                continue;
            }
            foreach (var target in Items(rel))
                _bdb.AddParameter(entity, _entityOfItem[target], IfcParameterPrefix + rel.Name, "", group);
        }
    }

    /// <summary>Each member of a property or quantity set becomes a parameter on the entity, named
    /// by the member and grouped by the set; a member without a value adds nothing, and a value
    /// repeated by a second set of the same name is added once (as Ara3D.IfcLoader's
    /// GetDistinctProperties does).</summary>
    private void AddSetProperties(EntityIndex entity, int set, HashSet<(string, string, string)> seen)
    {
        var setName = NameOf(set);
        // Members are resolved without counting: a set shared by many objects is expanded once per
        // object, and its unresolved members are already counted where the set's own relations are read.
        var members = _items.Relations[set].Where(r => SetMembers.Contains(r.Name)).SelectMany(r => Items(r, count: false));
        foreach (var member in members)
        {
            var name = NameOf(member);
            var value = ValueAttributes
                .Select(v => _items.Attributes[member].FirstOrDefault(a => a.Name == v))
                .FirstOrDefault(a => a != null);
            if (name == null || value == null || !seen.Add((setName ?? "", name, value.Value.GetRawText())))
                continue;
            AddValue(entity, name, setName ?? "", value.Value, value.IfcType);
        }
    }

    private string? NameOf(int item)
        => _items.Attributes[item].FirstOrDefault(a => a.Name == NameAttribute)?.Value is { ValueKind: JsonValueKind.String } v
            ? v.GetString()
            : null;

    /// <summary>A JSON value as the BOS parameter of its kind: a string as String, a boolean as
    /// Int 0 or 1 (BOS stores booleans as Int), a number as Int when its IFC type is an integer
    /// type and Number otherwise, and a list or object as String holding its JSON text. Null adds
    /// nothing.</summary>
    private void AddValue(EntityIndex entity, string name, string group, JsonElement value, string? ifcType)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                _bdb.AddParameter(entity, value.GetString()!, name, "", group);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                _bdb.AddParameter(entity, value.ValueKind == JsonValueKind.True ? 1 : 0, name, "", group);
                break;
            case JsonValueKind.Number when ifcType != null && IntegerTypes.Contains(ifcType) && value.TryGetInt32(out var n):
                _bdb.AddParameter(entity, n, name, "", group);
                break;
            case JsonValueKind.Number:
                _bdb.AddParameter(entity, value.GetDouble(), name, "", group);
                break;
            case JsonValueKind.Array:
            case JsonValueKind.Object:
                _bdb.AddParameter(entity, value.GetRawText(), name, "", group);
                break;
        }
    }

    private void CountAbsentTargets(FragmentsRelation rel)
    {
        foreach (var _ in Items(rel)) { }
    }

    /// <summary>The item positions a relation names; a target that is not an item in the file
    /// (an excluded class such as IfcOwnerHistory, or id 0) is counted and skipped.</summary>
    private IEnumerable<int> Items(FragmentsRelation rel, bool count = true)
    {
        foreach (var id in rel.Targets)
        {
            if (id >= 0 && id <= uint.MaxValue && _items.IndexOfLocalId.TryGetValue((uint)id, out var item))
                yield return item;
            else if (count)
                _droppedTargets[rel.Name] = _droppedTargets.GetValueOrDefault(rel.Name) + 1;
        }
    }
}
