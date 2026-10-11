namespace Ara3D.BimOpenSchema;

/// <summary>The scalar type a column of this kind holds. Deliberately not called ColumnType: the
/// dataflow engine has its own, and this assembly does not depend on it.</summary>
public enum ColumnScalarType
{
    Boolean,
    Integer,
    Number,
    Text,
    Date,
    Timestamp,
}

/// <summary>What a column is for in a table: how a reader should use it.</summary>
public enum ColumnRole
{
    /// <summary>Identifies a row or an entity (EntityIndex, GlobalId).</summary>
    Key,
    /// <summary>Names a row for a person (Name, Title).</summary>
    Label,
    /// <summary>Groups rows into a small set of values (Category, Level).</summary>
    Category,
    /// <summary>A quantity to sum, average or chart (Volume, Count).</summary>
    Measure,
    /// <summary>Points at a row of another table (StoreyIndex, FromRoom).</summary>
    Reference,
}

/// <summary>One column the toolkit's tables use, described once. <paramref name="Unit"/> is null
/// when the unit is not known: BOS stores values in the units of the source file, so a kind
/// states a unit only where the code that produces it fixes one.</summary>
public sealed record ColumnKind(
    string Name,
    ColumnScalarType Type,
    string Meaning,
    ColumnRole Role,
    string? Unit = null);
