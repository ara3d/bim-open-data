using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// The bim.orientation columns: where an element sits around the building in plan. Distance is
// registered with the room columns.
public static partial class ColumnKinds
{
    public static readonly ColumnKind Bearing = new("Bearing", ColumnScalarType.Number,
        "Degrees clockwise from project north (+Y of the model) to the element's box centre as seen from the plan centre; absent at the centre. BOS records no true-north angle.",
        ColumnRole.Measure);

    public static readonly ColumnKind Sector = new("Sector", ColumnScalarType.Integer,
        "The 45-degree compass slice of the bearing, 0 (N) to 7 (NW), clockwise.", ColumnRole.Category);

    public static readonly ColumnKind Direction = new("Direction", ColumnScalarType.Text,
        "The compass point of the bearing's slice: N, NE, E, SE, S, SW, W or NW.", ColumnRole.Category);

    public static readonly ColumnKind Facing = new("Facing", ColumnScalarType.Text,
        "The side (N, E, S or W) the box's larger vertical face looks out to.", ColumnRole.Category);

    public static readonly ColumnKind FacingArea = new("FacingArea", ColumnScalarType.Number,
        "Area of the box's larger vertical face, max(SizeX, SizeY) * SizeZ; it overstates a diagonal element's face.",
        ColumnRole.Measure);

    private static IReadOnlyList<ColumnKind> OrientationKinds =>
    [
        Bearing, Sector, Direction, Facing, FacingArea,
    ];
}
