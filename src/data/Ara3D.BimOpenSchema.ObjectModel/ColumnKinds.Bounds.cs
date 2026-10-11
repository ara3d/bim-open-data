using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// The bim.bounds columns: axis-aligned bounding boxes and what derives from them. All are in
// the model's own length unit, which BOS does not record, so none states one.
public static partial class ColumnKinds
{
    private static ColumnKind Length(string name, string meaning)
        => new(name, ColumnScalarType.Number, meaning, ColumnRole.Measure);

    public static readonly ColumnKind MinX = Length("MinX", "Smallest X of the element's bounding box.");
    public static readonly ColumnKind MinY = Length("MinY", "Smallest Y of the element's bounding box.");
    public static readonly ColumnKind MinZ = Length("MinZ", "Smallest Z of the element's bounding box.");
    public static readonly ColumnKind MaxX = Length("MaxX", "Largest X of the element's bounding box.");
    public static readonly ColumnKind MaxY = Length("MaxY", "Largest Y of the element's bounding box.");
    public static readonly ColumnKind MaxZ = Length("MaxZ", "Largest Z of the element's bounding box.");
    public static readonly ColumnKind SizeX = Length("SizeX", "Extent of the bounding box along X (MaxX - MinX).");
    public static readonly ColumnKind SizeY = Length("SizeY", "Extent of the bounding box along Y (MaxY - MinY).");
    public static readonly ColumnKind SizeZ = Length("SizeZ", "Extent of the bounding box along Z (MaxZ - MinZ).");
    public static readonly ColumnKind CenterX = Length("CenterX", "X of the bounding box's center.");
    public static readonly ColumnKind CenterY = Length("CenterY", "Y of the bounding box's center.");
    public static readonly ColumnKind CenterZ = Length("CenterZ", "Z of the bounding box's center.");

    public static readonly ColumnKind FootprintArea = new("FootprintArea", ColumnScalarType.Number,
        "Area of the bounding box seen from above (SizeX * SizeY), not of the element's true footprint.", ColumnRole.Measure);

    public static readonly ColumnKind Volume = new("Volume", ColumnScalarType.Number,
        "A volume: of the bounding box in bim.bounds (SizeX * SizeY * SizeZ), of the room in bim.rooms.", ColumnRole.Measure);

    public static readonly ColumnKind Diagonal = Length("Diagonal", "Length of the bounding box's space diagonal.");

    private static IReadOnlyList<ColumnKind> BoundsKinds =>
    [
        MinX, MinY, MinZ, MaxX, MaxY, MaxZ, SizeX, SizeY, SizeZ, CenterX, CenterY, CenterZ,
        FootprintArea, Volume, Diagonal,
    ];
}
