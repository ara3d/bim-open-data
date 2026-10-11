using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// Parameters, their coverage, and the columns only the BOS DuckDB views add (ParameterText,
// RelationText, StoreyOfEntity, StoreyOfElement).
public static partial class ColumnKinds
{
    public static readonly ColumnKind ParameterGroup = new("ParameterGroup", ColumnScalarType.Text,
        "The group a parameter is filed under in the source tool's property dialog.", ColumnRole.Category);

    public static readonly ColumnKind ValueType = new("ValueType", ColumnScalarType.Text,
        "The kind of a parameter's value: String, Number, Entity, Point and so on.", ColumnRole.Category);

    public static readonly ColumnKind Units = new("Units", ColumnScalarType.Text,
        "The unit name the source file gave a parameter; absent when it gave none.", ColumnRole.Category);

    public static readonly ColumnKind Value = new("Value", ColumnScalarType.Text,
        "A parameter's value as text, whatever its ValueType.", ColumnRole.Label);

    public static readonly ColumnKind Count = new("Count", ColumnScalarType.Integer,
        "How many rows or entities fall in the group.", ColumnRole.Measure);

    public static readonly ColumnKind Distinct = new("Distinct", ColumnScalarType.Integer,
        "How many different values a parameter takes.", ColumnRole.Measure);

    public static readonly ColumnKind FillRate = new("FillRate", ColumnScalarType.Number,
        "Share of entities that carry the parameter, from 0 to 1.", ColumnRole.Measure);

    public static readonly ColumnKind EntityIndexA = new("EntityIndexA", ColumnScalarType.Integer,
        "EntityIndex of the first entity of a relation.", ColumnRole.Reference);

    public static readonly ColumnKind EntityIndexB = new("EntityIndexB", ColumnScalarType.Integer,
        "EntityIndex of the second entity of a relation; for containment, the container.", ColumnRole.Reference);

    public static readonly ColumnKind NameA = new("NameA", ColumnScalarType.Text,
        "Name of the first entity of a relation.", ColumnRole.Label);

    public static readonly ColumnKind NameB = new("NameB", ColumnScalarType.Text,
        "Name of the second entity of a relation.", ColumnRole.Label);

    public static readonly ColumnKind RelationType = new("RelationType", ColumnScalarType.Text,
        "The kind of a relation: PartOf, MemberOf, ContainedIn and so on.", ColumnRole.Category);

    public static readonly ColumnKind StoreyIndex = new("StoreyIndex", ColumnScalarType.Integer,
        "EntityIndex of the building storey above an entity.", ColumnRole.Reference);

    public static readonly ColumnKind StoreyName = new("StoreyName", ColumnScalarType.Text,
        "Name of the building storey above an entity.", ColumnRole.Label);

    public static readonly ColumnKind Depth = new("Depth", ColumnScalarType.Integer,
        "Relations between an entity and its storey; 0 for the storey itself.", ColumnRole.Measure);

    private static IReadOnlyList<ColumnKind> ParameterKinds =>
    [
        ParameterGroup, ValueType, Units, Value, Count, Distinct, FillRate,
        EntityIndexA, EntityIndexB, NameA, NameB, RelationType, StoreyIndex, StoreyName, Depth,
    ];
}
