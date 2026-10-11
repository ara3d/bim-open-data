using System.Globalization;
using Ara3D.BimOpenSchema;
using Ara3D.DataTable;
using Ara3D.Ifc.Conventions;

namespace Ara3D.Ids;

/// <summary>One parameter of an entity, as the BOS text views show it: the set (or, for an IFC
/// attribute, the entity's class), the name, the stored type, and the value as text. An empty
/// text is absent: IDS treats "not provided" and "" alike, and BOS stores an IFC <c>$</c>
/// property value as an empty string.</summary>
public sealed record IdsValue(string Group, string Name, ParameterType Type, string Text)
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
/// class; GlobalId and Name live on the entity itself and are exposed as attributes too.</summary>
public sealed record IdsEntity(
    long EntityIndex,
    string GlobalId,
    string Name,
    string Category,
    IReadOnlyList<IdsValue> Values)
{
    public bool IsAttribute(IdsValue value)
        => value.Group == Category && value.Name.StartsWith(IfcParameterNames.Prefix, StringComparison.Ordinal);

    public IEnumerable<IdsValue> Properties
        => Values.Where(v => !IsAttribute(v));

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
/// parameters. Built from a BOS document, or from the <c>EntityText</c> and <c>ParameterText</c>
/// tables (as <c>bos.load</c> outputs them) so a check can run over tables already in a graph.</summary>
public sealed class IdsModel
{
    public IdsModel(IReadOnlyList<IdsEntity> entities)
    {
        Entities = entities;
        LengthToMetre = entities.SelectMany(e => e.Values)
            .Where(v => v.Name == IfcParameterNames.LengthUnitToMetre)
            .Select(v => double.TryParse(v.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
            .FirstOrDefault();
    }

    public IReadOnlyList<IdsEntity> Entities { get; }

    /// <summary>Metres per model length unit, from the project's <c>Ifc:LengthUnitToMetre</c>
    /// row; null when the model records none. IDS values are in SI, and the evaluator compares
    /// numbers as stored, so a model not in metres gets a warning.</summary>
    public double? LengthToMetre { get; }

    /// <summary>Every entity whose category is a row (the class entity), with its parameters in
    /// table order. Values are rendered as the DuckDB text views render them: an entity value by
    /// its name, a point as text, an absent string or number as "".</summary>
    public static IdsModel FromBimData(IBimData data)
    {
        var values = new Dictionary<int, List<IdsValue>>();
        foreach (var p in data.Parameters)
        {
            if ((uint)p.Descriptor >= (uint)data.Descriptors.Length)
                continue;
            var d = data.Descriptors[(int)p.Descriptor];
            var value = new IdsValue(Text(data, d.Group), Text(data, d.Name), d.Type, ValueText(data, d.Type, p.Value));
            if (!values.TryGetValue((int)p.Entity, out var list))
                values[(int)p.Entity] = list = [];
            list.Add(value);
        }

        var entities = new List<IdsEntity>();
        for (var i = 0; i < data.Entities.Length; i++)
        {
            var e = data.Entities[i];
            if ((uint)e.Category >= (uint)data.Entities.Length)
                continue;
            entities.Add(new(i, Text(data, e.GlobalId), Text(data, e.Name), Text(data, data.Entities[(int)e.Category].Name),
                values.TryGetValue(i, out var list) ? list : []));
        }

        return new(entities);
    }

    /// <summary>From the two text tables: <c>EntityIndex</c>, <c>GlobalId</c>, <c>Name</c>,
    /// <c>Category</c>; and <c>EntityIndex</c>, <c>Name</c>, <c>ParameterGroup</c>,
    /// <c>ValueType</c>, <c>Value</c>. Column names match case-insensitively. Rows without a
    /// category are left out; a parameter whose entity is not in the entities table is dropped.</summary>
    public static IdsModel FromTables(IDataTable entities, IDataTable parameters)
    {
        var values = new Dictionary<long, List<IdsValue>>();
        var pIndex = Column(parameters, "EntityIndex");
        var pName = Column(parameters, "Name");
        var pGroup = Column(parameters, "ParameterGroup");
        var pType = Column(parameters, "ValueType");
        var pValue = Column(parameters, "Value");
        for (var row = 0; row < parameters.Rows.Count; row++)
        {
            var index = Index(parameters, pIndex, row);
            var type = Enum.TryParse<ParameterType>(Cell(parameters, pType, row), out var t) ? t : ParameterType.String;
            if (!values.TryGetValue(index, out var list))
                values[index] = list = [];
            list.Add(new(Cell(parameters, pGroup, row), Cell(parameters, pName, row), type, Cell(parameters, pValue, row)));
        }

        var eIndex = Column(entities, "EntityIndex");
        var eGlobalId = Column(entities, "GlobalId");
        var eName = Column(entities, "Name");
        var eCategory = Column(entities, "Category");
        var result = new List<IdsEntity>();
        for (var row = 0; row < entities.Rows.Count; row++)
        {
            var category = Cell(entities, eCategory, row);
            if (category.Length == 0)
                continue;
            var index = Index(entities, eIndex, row);
            result.Add(new(index, Cell(entities, eGlobalId, row), Cell(entities, eName, row), category,
                values.TryGetValue(index, out var list) ? list : []));
        }

        return new(result);
    }

    private static int Column(IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (string.Equals(table.Columns[i].Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new ArgumentException(
            $"Table '{table.Name}' has no '{name}' column; its columns are {string.Join(", ", table.Columns.Select(c => c.Descriptor.Name))}");
    }

    private static long Index(IDataTable table, int column, int row)
        => table[column, row] is { } value and not DBNull ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : -1;

    private static string Cell(IDataTable table, int column, int row)
        => table[column, row] is { } value and not DBNull
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

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
