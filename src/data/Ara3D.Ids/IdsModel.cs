using System.Globalization;
using Ara3D.BimOpenSchema;
using Ara3D.DataTable;
using Ara3D.Ifc.Conventions;

namespace Ara3D.Ids;

/// <summary>One parameter of an entity, as the BOS text views show it: the set (or, for an IFC
/// attribute, the entity's class), the name, the stored type, the value as text, and the
/// descriptor's units ("" when it states none; the IFC converter states none). An empty text is
/// absent: IDS treats "not provided" and "" alike, and BOS stores an IFC <c>$</c> property value
/// as an empty string.</summary>
public sealed record IdsValue(string Group, string Name, ParameterType Type, string Text, string Units = "")
{
    public bool IsPresent
        => Text.Length > 0;

    /// <summary>True for an IFC logical stored as text: <c>.T.</c>, <c>.F.</c>, or <c>.U.</c>.</summary>
    public bool IsLogical
        => Text is ".T." or ".F." or ".U.";

    /// <summary>True for an IFC enumeration stored as its STEP token, <c>.DOOR.</c> or
    /// <c>.NOTDEFINED.</c>: the converter keeps the dots.</summary>
    public bool IsEnumeration
        => Text.Length > 2 && Text[0] == '.' && Text[^1] == '.' && !IsLogical;

    /// <summary>The text an IDS compares against: an enumeration without its dots, else the text.</summary>
    public string Token
        => IsEnumeration ? Text[1..^1] : Text;

    /// <summary>The value typed for an IDS constraint: a number for Number and Int, a boolean for
    /// the IFC <c>.T.</c> and <c>.F.</c> logicals BOS stores as text, otherwise the token.</summary>
    public object Candidate
        => Type switch
        {
            ParameterType.Number or ParameterType.Int =>
                double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : Text,
            _ => Text switch
            {
                ".T." => true,
                ".F." => false,
                _ => Token,
            },
        };

    /// <summary>What kind of value this is, for the IDS <c>dataType</c> check.</summary>
    public IdsValueKind Kind
        => Type switch
        {
            ParameterType.Number or ParameterType.Int => IdsValueKind.Number,
            ParameterType.String when IsLogical => IdsValueKind.Boolean,
            _ => IdsValueKind.Text,
        };
}

/// <summary>The kinds BOS can tell apart. IFC's measure types (a length against an area) are
/// not recorded in BOS, so an IDS <c>dataType</c> is checked at this level.</summary>
public enum IdsValueKind
{
    Number,
    Boolean,
    Text,
    Unknown,
}

/// <summary>An entity with its parameters. BOS stores IFC property-set values under the set's
/// name and IFC attributes as parameters named <c>Ifc:{attribute}</c> grouped under the entity's
/// class; GlobalId and Name live on the entity itself and are exposed as attributes too.
/// <para>An occurrence's type object (a door type, an IFC2x3 door style) keeps its own property
/// sets as parameters on the type entity (see <c>IfcTypeObjects</c> in Ara3D.Ifc.Conventions), so
/// <see cref="Properties"/> adds the type's values to the occurrence's own, as IDS 1.0 and
/// ifctester do. Attributes are not inherited: PredefinedType is the occurrence's own.</para></summary>
public sealed record IdsEntity(
    long EntityIndex,
    string GlobalId,
    string Name,
    string Category,
    IReadOnlyList<IdsValue> Values)
{
    /// <summary>The BOS entity index of the type object (<c>Entity.Type</c>), or -1 when the entity
    /// has none or the source gives only the type's name.</summary>
    public long TypeIndex { get; init; } = -1;

    /// <summary>The type object's name, as the <c>EntityText</c> view's <c>Type</c> column gives
    /// it; "" when the entity has none.</summary>
    public string TypeName { get; init; } = "";

    /// <summary>Resolves <see cref="Type"/>; null for an entity built outside
    /// <see cref="IdsModel.FromBimData"/> and <see cref="IdsModel.FromTables"/>, which has no type.</summary>
    internal IdsTypeObjects? TypeObjects { get; init; }

    public bool HasType
        => TypeIndex >= 0 || TypeName.Length > 0;

    /// <summary>The type object, or null when the entity has none or it cannot be told (see
    /// <see cref="TypeUnresolved"/>).</summary>
    public IdsEntity? Type
        => TypeObjects?.Of(this);

    /// <summary>Why the entity names a type that cannot be found, or null.</summary>
    internal string? TypeUnresolved
        => TypeObjects?.Unresolved(this);

    public bool IsAttribute(IdsValue value)
        => value.Group == Category && value.Name.StartsWith(IfcParameterNames.Prefix, StringComparison.Ordinal);

    /// <summary>The property values the entity itself carries, without its type's.</summary>
    public IEnumerable<IdsValue> OwnProperties
        => Values.Where(v => !IsAttribute(v));

    /// <summary>The entity's own property values, then its type's for each set and property name
    /// the entity does not carry. The entity's row wins even when its value is empty, as an IFC
    /// <c>$</c> on the occurrence overrides the type in ifctester.</summary>
    public IEnumerable<IdsValue> Properties
        => Type is { } type ? Inherit(OwnProperties.ToList(), type.OwnProperties) : OwnProperties;

    private static IEnumerable<IdsValue> Inherit(IReadOnlyList<IdsValue> own, IEnumerable<IdsValue> inherited)
    {
        var stated = own.Select(v => (v.Group, v.Name)).ToHashSet();
        return own.Concat(inherited.Where(v => !stated.Contains((v.Group, v.Name))));
    }

    /// <summary>Every attribute BOS carries for the entity, keyed by the IFC attribute name.</summary>
    public IEnumerable<(string Name, IdsValue Value)> Attributes
        => Values.Where(IsAttribute)
            .Select(v => (v.Name[IfcParameterNames.Prefix.Length..], v))
            .Prepend((IfcParameterNames.NameAttribute, NameAttribute))
            .Prepend((IfcParameterNames.GlobalIdAttribute, new IdsValue(Category, IfcParameterNames.GlobalIdAttribute, ParameterType.String, GlobalId)));

    /// <summary>The IFC Name attribute. For a space the converter stores the LongName as the
    /// entity's name and the IFC Name under <c>Ifc:Room:Number</c>, so that row is the Name.</summary>
    private IdsValue NameAttribute
        => Category == SpaceCategory
            ? Values.FirstOrDefault(v => v.Group == Category && v.Name == IfcParameterNames.RoomNumber)
                ?? new IdsValue(Category, IfcParameterNames.NameAttribute, ParameterType.String, "")
            : new IdsValue(Category, IfcParameterNames.NameAttribute, ParameterType.String, Name);

    private const string SpaceCategory = "IFCSPACE";

    public IdsValue? Attribute(string name)
        => Attributes.FirstOrDefault(a => a.Name == name).Value;
}

/// <summary>What an IDS is evaluated over: every entity with a class, each with all of its
/// parameters, and the relations between them when the source has them. Built from a BOS
/// document, or from the <c>EntityText</c>, <c>ParameterText</c> and (optionally)
/// <c>RelationText</c> tables (as <c>bos.load</c> outputs them) so a check can run over tables
/// already in a graph.</summary>
public sealed class IdsModel
{
    private readonly Lazy<IdsRelations>? _relations;
    private readonly Lazy<Dictionary<long, IdsEntity>> _byIndex;

    /// <param name="relations">Builds the relations on first use, so a check with no material or
    /// part-of facet never sorts them; null when the source has none, and those two facets give
    /// InfoNotAvailable.</param>
    public IdsModel(IReadOnlyList<IdsEntity> entities, Func<IdsRelations>? relations = null)
    {
        Entities = entities;
        _relations = relations is null ? null : new(relations);
        _byIndex = new(() =>
        {
            var byIndex = new Dictionary<long, IdsEntity>(entities.Count);
            foreach (var entity in entities)
                byIndex.TryAdd(entity.EntityIndex, entity);
            return byIndex;
        });
        Units = new(
            RecordedNumber(entities, IfcParameterNames.LengthUnitToMetre),
            RecordedNumber(entities, IfcParameterNames.AreaUnitToSquareMetre),
            RecordedNumber(entities, IfcParameterNames.VolumeUnitToCubicMetre));
    }

    /// <summary>The first parameter of that name in the model, as a number; null when none.</summary>
    private static double? RecordedNumber(IReadOnlyList<IdsEntity> entities, string name)
        => entities.SelectMany(e => e.Values)
            .Where(v => v.Name == name)
            .Select(v => double.TryParse(v.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
            .FirstOrDefault();

    public IReadOnlyList<IdsEntity> Entities { get; }

    /// <summary>The relations, or null when the model was built without them.</summary>
    public IdsRelations? Relations
        => _relations?.Value;

    /// <summary>The entity at a BOS entity index, or null when it has no class (or no row).</summary>
    public IdsEntity? Entity(long entityIndex)
        => _byIndex.Value.GetValueOrDefault(entityIndex);

    /// <summary>The project's length, area and volume units in SI, from its
    /// <c>Ifc:LengthUnitToMetre</c>, <c>Ifc:AreaUnitToSquareMetre</c> and
    /// <c>Ifc:VolumeUnitToCubicMetre</c> rows. IDS values are in SI, so the evaluator scales a
    /// length, area or volume by these before comparing it (see <see cref="IdsUnits"/>).</summary>
    public IdsUnitScales Units { get; }

    /// <summary>Every entity whose category is a row (the class entity), with its parameters in
    /// table order and its <c>Entity.Type</c>, and the document's relations. Values are rendered as the DuckDB text views render them: an entity value by
    /// its name, a point as text, an absent string or number as "".</summary>
    public static IdsModel FromBimData(IBimData data)
    {
        var values = new Dictionary<int, List<IdsValue>>();
        foreach (var p in data.Parameters)
        {
            if ((uint)p.Descriptor >= (uint)data.Descriptors.Length)
                continue;
            var d = data.Descriptors[(int)p.Descriptor];
            var value = new IdsValue(Text(data, d.Group), Text(data, d.Name), d.Type, ValueText(data, d.Type, p.Value), Text(data, d.Units));
            if (!values.TryGetValue((int)p.Entity, out var list))
                values[(int)p.Entity] = list = [];
            list.Add(value);
        }

        IdsModel? model = null;
        var types = new IdsTypeObjects(() => model!);
        var entities = new List<IdsEntity>();
        for (var i = 0; i < data.Entities.Length; i++)
        {
            var e = data.Entities[i];
            if ((uint)e.Category >= (uint)data.Entities.Length)
                continue;
            var hasType = (uint)e.Type < (uint)data.Entities.Length && (int)e.Type != i;
            entities.Add(new(i, Text(data, e.GlobalId), Text(data, e.Name), Text(data, data.Entities[(int)e.Category].Name),
                values.TryGetValue(i, out var list) ? list : [])
            {
                TypeIndex = hasType ? (int)e.Type : -1,
                TypeName = hasType ? Text(data, data.Entities[(int)e.Type].Name) : "",
                TypeObjects = types,
            });
        }

        var relations = data.Relations;
        return model = new(entities, () => IdsRelations.FromBimData(relations));
    }

    /// <summary>From the text tables: <c>EntityIndex</c>, <c>GlobalId</c>, <c>Name</c>,
    /// <c>Category</c>, and optionally <c>Type</c> (the type object's name, as the
    /// <c>EntityText</c> view gives it) and <c>TypeIndex</c> (the type's EntityIndex, which no view
    /// has yet; when present it is used instead of the name, see <see cref="IdsTypeObjects"/>); and <c>EntityIndex</c>, <c>Name</c>, <c>ParameterGroup</c>,
    /// <c>ValueType</c>, <c>Value</c>, and optionally <c>Units</c> (the <c>ParameterText</c> view
    /// has it); and, optionally, the relations in the <c>RelationText</c> shape (see
    /// <see cref="IdsRelations.FromTable"/>). Column names match case-insensitively. Rows without a
    /// category are left out; a parameter whose entity is not in the entities table is dropped.</summary>
    public static IdsModel FromTables(IDataTable entities, IDataTable parameters, IDataTable? relations = null)
    {
        var values = new Dictionary<long, List<IdsValue>>();
        var pIndex = IdsTableColumns.Required(parameters, "EntityIndex");
        var pName = IdsTableColumns.Required(parameters, "Name");
        var pGroup = IdsTableColumns.Required(parameters, "ParameterGroup");
        var pType = IdsTableColumns.Required(parameters, "ValueType");
        var pValue = IdsTableColumns.Required(parameters, "Value");
        var pUnits = IdsTableColumns.Optional(parameters, "Units");
        for (var row = 0; row < parameters.Rows.Count; row++)
        {
            var index = IdsTableColumns.Index(parameters, pIndex, row);
            var type = Enum.TryParse<ParameterType>(IdsTableColumns.Text(parameters, pType, row), out var t) ? t : ParameterType.String;
            if (!values.TryGetValue(index, out var list))
                values[index] = list = [];
            list.Add(new(IdsTableColumns.Text(parameters, pGroup, row), IdsTableColumns.Text(parameters, pName, row), type, IdsTableColumns.Text(parameters, pValue, row),
                pUnits < 0 ? "" : IdsTableColumns.Text(parameters, pUnits, row)));
        }

        var eIndex = IdsTableColumns.Required(entities, "EntityIndex");
        var eGlobalId = IdsTableColumns.Required(entities, "GlobalId");
        var eName = IdsTableColumns.Required(entities, "Name");
        var eCategory = IdsTableColumns.Required(entities, "Category");
        var eType = IdsTableColumns.Optional(entities, "Type");
        var eTypeIndex = IdsTableColumns.Optional(entities, "TypeIndex");
        IdsModel? model = null;
        var types = new IdsTypeObjects(() => model!);
        var result = new List<IdsEntity>();
        for (var row = 0; row < entities.Rows.Count; row++)
        {
            var category = IdsTableColumns.Text(entities, eCategory, row);
            if (category.Length == 0)
                continue;
            var index = IdsTableColumns.Index(entities, eIndex, row);
            result.Add(new(index, IdsTableColumns.Text(entities, eGlobalId, row), IdsTableColumns.Text(entities, eName, row), category,
                values.TryGetValue(index, out var list) ? list : [])
            {
                TypeIndex = eTypeIndex < 0 ? -1 : IdsTableColumns.Index(entities, eTypeIndex, row),
                TypeName = eType < 0 ? "" : IdsTableColumns.Text(entities, eType, row),
                TypeObjects = types,
            });
        }

        return model = new(result, relations is null ? null : () => IdsRelations.FromTable(relations));
    }

    private static string Text(IBimData data, StringIndex index)
        => (uint)index < (uint)data.Strings.Length ? data.Strings[(int)index] : "";

    private static string ValueText(IBimData data, ParameterType type, int value)
        => type switch
        {
            ParameterType.String => Text(data, (StringIndex)value),
            ParameterType.Number => (uint)value < (uint)data.Numbers.Length
                ? data.Numbers[value].ToString(CultureInfo.InvariantCulture)
                : "",
            ParameterType.Entity => (uint)value < (uint)data.Entities.Length
                ? Text(data, data.Entities[value].Name)
                : "",
            ParameterType.Point => (uint)value < (uint)data.Points.Length
                ? data.Points[value].ToString()
                : "",
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
}
