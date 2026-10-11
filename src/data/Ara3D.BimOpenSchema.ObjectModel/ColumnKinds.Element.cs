using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// The identity and placement columns of bim.elements and the EntityText view.
public static partial class ColumnKinds
{
    public static readonly ColumnKind EntityIndex = new(IssueColumns.EntityIndex, ColumnScalarType.Integer,
        "Row number of the entity in the BOS Entities table; -1 when a row is about no entity.", ColumnRole.Key);

    public static readonly ColumnKind LocalId = new("LocalId", ColumnScalarType.Integer,
        "The id the source file gave the entity (the STEP id of an IFC file, the element id of Revit).", ColumnRole.Key);

    public static readonly ColumnKind StepId = new("StepId", ColumnScalarType.Integer,
        "The EntityText view's name for LocalId.", ColumnRole.Key);

    public static readonly ColumnKind GlobalId = new(IssueColumns.GlobalId, ColumnScalarType.Text,
        "The entity's globally unique id (an IfcGloballyUniqueId); empty when the source gave none.", ColumnRole.Key);

    public static readonly ColumnKind Name = new("Name", ColumnScalarType.Text,
        "The entity's name, or a parameter's name in a parameter table.", ColumnRole.Label);

    public static readonly ColumnKind Category = new("Category", ColumnScalarType.Text,
        "The entity's category: the IFC class (IFCWALL) or the Revit category.", ColumnRole.Category);

    public static readonly ColumnKind CategoryType = new("CategoryType", ColumnScalarType.Text,
        "The kind of the category, as the source file classifies it.", ColumnRole.Category);

    public static readonly ColumnKind Type = new(IssueColumns.Type, ColumnScalarType.Text,
        "The name of the entity's type (the family type of an element), or the type of a BCF issue.", ColumnRole.Category);

    public static readonly ColumnKind ClassName = new("ClassName", ColumnScalarType.Text,
        "The class of the entity in the source model.", ColumnRole.Category);

    public static readonly ColumnKind Level = new("Level", ColumnScalarType.Text,
        "Name of the level or storey the entity sits on.", ColumnRole.Category);

    public static readonly ColumnKind Elevation = new("Elevation", ColumnScalarType.Number,
        "Height of the level or element; absent when the model gives none.", ColumnRole.Measure);

    public static readonly ColumnKind Room = new("Room", ColumnScalarType.Text,
        "Name of the room the entity is in, or a room's label in a hop table.", ColumnRole.Reference);

    public static readonly ColumnKind Document = new("Document", ColumnScalarType.Text,
        "Title of the document (the linked or host model) the entity comes from.", ColumnRole.Category);

    public static readonly ColumnKind Workset = new("Workset", ColumnScalarType.Integer,
        "Id of the workset the entity belongs to; absent when it has none.", ColumnRole.Category);

    public static readonly ColumnKind Group = new("Group", ColumnScalarType.Text,
        "Name of the group the entity belongs to.", ColumnRole.Category);

    private static IReadOnlyList<ColumnKind> ElementKinds =>
    [
        EntityIndex, LocalId, StepId, GlobalId, Name, Category, CategoryType, Type, ClassName,
        Level, Elevation, Room, Document, Workset, Group,
    ];
}
