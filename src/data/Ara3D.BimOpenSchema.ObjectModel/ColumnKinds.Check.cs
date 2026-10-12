using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// The tables around a check: an IDS evaluation's specification list and per-specification
// summary (ids.specifications, ids.check). The verdict columns themselves are in the issue kinds.
public static partial class ColumnKinds
{
    public static readonly ColumnKind IfcVersion = new("IfcVersion", ColumnScalarType.Text,
        "The IFC schema versions a specification applies to, comma-separated.", ColumnRole.Category);

    public static readonly ColumnKind Cardinality = new("Cardinality", ColumnScalarType.Text,
        "Whether a specification's applicable entities are required, optional or prohibited.", ColumnRole.Category);

    public static readonly ColumnKind Applicability = new("Applicability", ColumnScalarType.Text,
        "The facets that select the entities a specification applies to, in short form.", ColumnRole.Label);

    public static readonly ColumnKind Requirements = new("Requirements", ColumnScalarType.Text,
        "The facets an applicable entity must satisfy, in short form.", ColumnRole.Label);

    public static readonly ColumnKind Supported = new("Supported", ColumnScalarType.Boolean,
        "False when a facet of the specification would give InfoNotAvailable because BOS cannot answer it.",
        ColumnRole.Category);

    public static readonly ColumnKind Pass = new("Pass", ColumnScalarType.Integer,
        "How many rows of a check got the verdict Pass.", ColumnRole.Measure);

    public static readonly ColumnKind Fail = new("Fail", ColumnScalarType.Integer,
        "How many rows of a check got the verdict Fail.", ColumnRole.Measure);

    public static readonly ColumnKind InfoNotAvailable = new("InfoNotAvailable", ColumnScalarType.Integer,
        "How many rows of a check got the verdict InfoNotAvailable.", ColumnRole.Measure);

    public static readonly ColumnKind Rows = new("Rows", ColumnScalarType.Integer,
        "How many verdict rows a check produced.", ColumnRole.Measure);

    private static IReadOnlyList<ColumnKind> CheckKinds =>
    [
        IfcVersion, Cardinality, Applicability, Requirements, Supported, Pass, Fail, InfoNotAvailable, Rows,
    ];
}
