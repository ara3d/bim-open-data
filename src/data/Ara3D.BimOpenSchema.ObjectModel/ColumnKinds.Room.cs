using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// Rooms, levels, classification and the door graph: bim.rooms, bim.levels, bim.discipline,
// bim.classifyRooms, bim.navGraph, bim.hops, bim.nearest.
public static partial class ColumnKinds
{
    public static readonly ColumnKind Number = new("Number", ColumnScalarType.Text,
        "The room's number as a label (text, because numbers like 'A-101' occur).", ColumnRole.Label);

    public static readonly ColumnKind UnboundedHeight = new("UnboundedHeight", ColumnScalarType.Number,
        "The room's unbounded height parameter; absent when the room has none.", ColumnRole.Measure);

    public static readonly ColumnKind ElementCount = new("ElementCount", ColumnScalarType.Integer,
        "How many elements a level or room holds.", ColumnRole.Measure);

    public static readonly ColumnKind RoomCount = new("RoomCount", ColumnScalarType.Integer,
        "How many rooms a level holds.", ColumnRole.Measure);

    public static readonly ColumnKind Discipline = new("Discipline", ColumnScalarType.Text,
        "The discipline an element is assigned to (architectural, structural, mechanical, ...).", ColumnRole.Category);

    public static readonly ColumnKind RoomClass = new("RoomClass", ColumnScalarType.Text,
        "The class bim.classifyRooms gave the room from its name.", ColumnRole.Category);

    public static readonly ColumnKind Door = new("Door", ColumnScalarType.Integer,
        "EntityIndex of a door.", ColumnRole.Reference);

    public static readonly ColumnKind DoorName = new("DoorName", ColumnScalarType.Text,
        "The door's name.", ColumnRole.Label);

    public static readonly ColumnKind FromRoom = new("FromRoom", ColumnScalarType.Text,
        "Label of the room on the door's from side; 'Outside' when there is none.", ColumnRole.Reference);

    public static readonly ColumnKind ToRoom = new("ToRoom", ColumnScalarType.Text,
        "Label of the room on the door's to side; 'Outside' when there is none.", ColumnRole.Reference);

    public static readonly ColumnKind Hops = new("Hops", ColumnScalarType.Integer,
        "Doors to pass from the start room; absent when the room cannot be reached.", ColumnRole.Measure);

    public static readonly ColumnKind Distance = new("Distance", ColumnScalarType.Number,
        "A distance in the model's length unit: to the nearest candidate row in bim.nearest, from the plan centre in bim.orientation.", ColumnRole.Measure);

    private static IReadOnlyList<ColumnKind> RoomKinds =>
    [
        Number, UnboundedHeight, ElementCount, RoomCount, Discipline, RoomClass,
        Door, DoorName, FromRoom, ToRoom, Hops, Distance,
    ];
}
