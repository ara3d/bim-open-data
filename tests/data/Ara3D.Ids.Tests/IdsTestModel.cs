using Ara3D.BimOpenSchema;
using Ara3D.DataTable;

namespace Ara3D.Ids.Tests;

/// <summary>A small model built from the text tables, the shape <c>bos.load</c> outputs, so the
/// evaluator is tested without a file: two doors (widths 0.9 and 0.8, one with
/// a predefined type, one USERDEFINED with an ObjectType), two walls (one with a fire rating and
/// a length quantity, one with an empty fire rating and a logical), a window, and a space whose
/// LongName is the entity name and whose IFC Name is the room number. Enumerations are stored
/// as the converter stores them, with their dots. The model records no length unit; the wall's
/// quantity states its unit, "m", in the Units column.
/// <para>Both doors are of the door type "Door Type A" (named in the Type column, as the
/// <c>EntityText</c> view names it), which carries Pset_DoorCommon.FireRating = EI60 and an
/// OperationType attribute: door D1 overrides the fire rating with its own EI30, door D2 has
/// none of its own.</para>
/// <para>The relations, as the IFC converter writes them: wall W1 HasMaterial a layer set whose two
/// layers each HasMaterial a material, "Concrete" and "Insulation"; door D1 HasMaterial "Wood";
/// both doors are ContainedIn the storey "Level 1", which is MemberOf (IfcRelAggregates) the
/// building. The window and wall W2 are related to nothing. <see cref="Build"/> leaves the
/// relations out, <see cref="BuildWithRelations"/> gives them.</para></summary>
internal static class IdsTestModel
{
    public const string Door1 = "D1", Door2 = "D2", Wall1 = "W1", Wall2 = "W2", Window = "X1", Space = "S1", DoorType = "DT1";
    public const string DoorTypeName = "Door Type A";

    public static IdsModel Build()
        => IdsModel.FromTables(Entities(), Parameters());

    public static IdsModel BuildWithRelations()
        => IdsModel.FromTables(Entities(), Parameters(), Relations());

    public static IDataTable Entities()
    {
        var b = new DataTableBuilder("entities");
        b.AddColumn(new object?[] { 0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L, 11L, 12L, 13L, 14L, 15L }, "EntityIndex", typeof(long));
        b.AddColumn(new object?[] { Door1, Door2, Wall1, Wall2, Window, Space, null, null, null, null, null, null, null, "L1", "B1", DoorType }, "GlobalId", typeof(string));
        b.AddColumn(new object?[]
        {
            "Door 1", "Door 2", "Wall 1", "Wall 2", "Window 1", "Kitchen", "IFCDOOR",
            "Wall Set", null, null, "Concrete", "Insulation", "Wood", "Level 1", "Building", DoorTypeName,
        }, "Name", typeof(string));
        b.AddColumn(new object?[]
        {
            "IFCDOOR", "IFCDOOR", "IFCWALL", "IFCWALL", "IFCWINDOW", "IFCSPACE", null,
            "IFCMATERIALLAYERSET", "IFCMATERIALLAYER", "IFCMATERIALLAYER", "IFCMATERIAL", "IFCMATERIAL", "IFCMATERIAL",
            "IFCBUILDINGSTOREY", "IFCBUILDING", "IFCDOORTYPE",
        }, "Category", typeof(string));
        b.AddColumn(new object?[] { DoorTypeName, DoorTypeName, null, null, null, null, null, null, null, null, null, null, null, null, null, null }, "Type", typeof(string));
        return b.Build();
    }

    public static IDataTable Parameters()
    {
        var rows = new (long Entity, string Name, string Group, string Type, string? Value)[]
        {
            (0, "Ifc:OverallWidth", "IFCDOOR", "Number", "0.9"),
            (0, "Ifc:PredefinedType", "IFCDOOR", "String", ".DOOR."),
            (0, "FireRating", "Pset_DoorCommon", "String", "EI30"),
            (0, "IsExternal", "Pset_DoorCommon", "String", ".T."),
            (1, "Ifc:OverallWidth", "IFCDOOR", "Number", "0.8"),
            (1, "Ifc:PredefinedType", "IFCDOOR", "String", ".USERDEFINED."),
            (1, "Ifc:ObjectType", "IFCDOOR", "String", "Hatch"),
            (2, "FireRating", "Pset_WallCommon", "String", "REI60"),
            (2, "Length", "Qto_WallBaseQuantities", "Number", "5"),
            (3, "FireRating", "Pset_WallCommon", "String", null),
            (3, "LoadBearing", "Pset_WallCommon", "String", ".F."),
            (5, "Ifc:Room:Number", "IFCSPACE", "String", "101"),
            (15, "FireRating", "Pset_DoorCommon", "String", "EI60"),
            (15, "Ifc:OperationType", "IFCDOORTYPE", "String", ".SINGLE_SWING_LEFT."),
        };
        var b = new DataTableBuilder("parameters");
        b.AddColumn(rows.Select(r => (object?)r.Entity).ToArray(), "EntityIndex", typeof(long));
        b.AddColumn(rows.Select(r => (object?)r.Name).ToArray(), "Name", typeof(string));
        b.AddColumn(rows.Select(r => (object?)r.Group).ToArray(), "ParameterGroup", typeof(string));
        b.AddColumn(rows.Select(r => (object?)(r.Name == "Length" ? "m" : null)).ToArray(), "Units", typeof(string));
        b.AddColumn(rows.Select(r => (object?)r.Type).ToArray(), "ValueType", typeof(string));
        b.AddColumn(rows.Select(r => (object?)r.Value).ToArray(), "Value", typeof(string));
        return b.Build();
    }

    /// <summary>The relations, with the three <c>RelationText</c> columns the model reads.</summary>
    public static IDataTable Relations()
    {
        var rows = new (long A, long B, string Type)[]
        {
            (2, 7, "HasMaterial"),
            (7, 8, "HasLayer"),
            (7, 9, "HasLayer"),
            (8, 10, "HasMaterial"),
            (9, 11, "HasMaterial"),
            (0, 12, "HasMaterial"),
            (0, 13, "ContainedIn"),
            (1, 13, "ContainedIn"),
            (13, 14, "MemberOf"),
        };
        var b = new DataTableBuilder("relations");
        b.AddColumn(rows.Select(r => (object?)r.A).ToArray(), "EntityIndexA", typeof(long));
        b.AddColumn(rows.Select(r => (object?)r.B).ToArray(), "EntityIndexB", typeof(long));
        b.AddColumn(rows.Select(r => (object?)r.Type).ToArray(), "RelationType", typeof(string));
        return b.Build();
    }
}

/// <summary>IDS 1.0 XML built from fragments, so each test states only the facets it is about.</summary>
internal static class IdsXml
{
    public static string Document(params string[] specifications)
        => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ids xmlns="http://standards.buildingsmart.org/IDS" xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <info><title>test</title></info>
              <specifications>
            {string.Join("\n", specifications)}
              </specifications>
            </ids>
            """;

    /// <summary>One specification; <paramref name="occurs"/> is the applicability cardinality,
    /// as IDS 1.0 writes it (minOccurs and maxOccurs).</summary>
    public static string Specification(string name, string applicability, string requirements = "",
        string occurs = """minOccurs="1" maxOccurs="unbounded" """, string description = "")
        => $"""
            <specification name="{name}" ifcVersion="IFC4"{(description.Length > 0 ? $" description=\"{description}\"" : "")}>
              <applicability {occurs}>{applicability}</applicability>
              <requirements>{requirements}</requirements>
            </specification>
            """;

    public static string Entity(string name, string? predefinedType = null)
        => $"<entity><name>{Simple(name)}</name>{(predefinedType is null ? "" : $"<predefinedType>{Simple(predefinedType)}</predefinedType>")}</entity>";

    public static string EntityEnum(params string[] names)
        => $"<entity><name><xs:restriction base=\"xs:string\">{string.Join("", names.Select(n => $"<xs:enumeration value=\"{n}\"/>"))}</xs:restriction></name></entity>";

    public static string Property(string set, string name, string? value = null, string? dataType = null, string? cardinality = null)
        => $"<property{Attr("dataType", dataType)}{Attr("cardinality", cardinality)}><propertySet>{Simple(set)}</propertySet><baseName>{Simple(name)}</baseName>{(value is null ? "" : $"<value>{value}</value>")}</property>";

    public static string Attribute(string name, string? value = null, string? cardinality = null)
        => $"<attribute{Attr("cardinality", cardinality)}><name>{Simple(name)}</name>{(value is null ? "" : $"<value>{value}</value>")}</attribute>";

    public static string Material(string value, string? cardinality = null)
        => MaterialOf(Simple(value), cardinality);

    /// <summary>A material facet whose value is IDS XML: a simple value or a restriction.</summary>
    public static string MaterialOf(string valueXml, string? cardinality = null)
        => $"<material{Attr("cardinality", cardinality)}><value>{valueXml}</value></material>";

    /// <summary>A part-of facet; <paramref name="relation"/> is the IDS relation attribute, left
    /// out when null.</summary>
    public static string PartOf(string entity, string? relation, string? cardinality = null)
        => $"<partOf{Attr("relation", relation)}{Attr("cardinality", cardinality)}><entity><name>{Simple(entity)}</name></entity></partOf>";

    public static string Simple(string value)
        => $"<simpleValue>{value}</simpleValue>";

    public static string Enumeration(params string[] values)
        => $"<xs:restriction base=\"xs:string\">{string.Join("", values.Select(v => $"<xs:enumeration value=\"{v}\"/>"))}</xs:restriction>";

    public static string Pattern(string pattern)
        => $"<xs:restriction base=\"xs:string\"><xs:pattern value=\"{pattern}\"/></xs:restriction>";

    public static string AtLeast(double value)
        => $"<xs:restriction base=\"xs:double\"><xs:minInclusive value=\"{value}\"/></xs:restriction>";

    public static string AtMost(double value)
        => $"<xs:restriction base=\"xs:double\"><xs:maxInclusive value=\"{value}\"/></xs:restriction>";

    private static string Attr(string name, string? value)
        => value is null ? "" : $" {name}=\"{value}\"";
}
