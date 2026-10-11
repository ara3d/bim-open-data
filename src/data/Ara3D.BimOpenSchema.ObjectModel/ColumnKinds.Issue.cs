using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// Issue, selection and verdict tables (TKT-182). GlobalId, EntityIndex and Type are registered
// with the element columns; their names are the same IssueColumns constants.
public static partial class ColumnKinds
{
    public static readonly ColumnKind Title = new(IssueColumns.Title, ColumnScalarType.Text,
        "The issue's title; rows with one title form one issue (one BCF topic).", ColumnRole.Label);

    public static readonly ColumnKind Description = new(IssueColumns.Description, ColumnScalarType.Text,
        "What is wrong, in words.", ColumnRole.Label);

    public static readonly ColumnKind Status = new(IssueColumns.Status, ColumnScalarType.Text,
        "The issue's state, for example Open or Closed.", ColumnRole.Category);

    public static readonly ColumnKind Priority = new(IssueColumns.Priority, ColumnScalarType.Text,
        "The issue's priority, for example High.", ColumnRole.Category);

    public static readonly ColumnKind CheckId = new(IssueColumns.CheckId, ColumnScalarType.Text,
        "Identifier of the check or specification that produced the verdict.", ColumnRole.Key);

    public static readonly ColumnKind Verdict = new(IssueColumns.Verdict, ColumnScalarType.Text,
        "The result of a check on an entity: Pass, Fail, or InfoNotAvailable when the model lacks what the check reads.", ColumnRole.Category);

    public static readonly ColumnKind CheckTitle = new(IssueColumns.CheckTitle, ColumnScalarType.Text,
        "Human title of the check, shown to people.", ColumnRole.Label);

    public static readonly ColumnKind Citation = new(IssueColumns.Citation, ColumnScalarType.Text,
        "The requirement the check enforces, as its source states it (a code clause, the IDS facets).", ColumnRole.Label);

    public static readonly ColumnKind Reason = new(IssueColumns.Reason, ColumnScalarType.Text,
        "Why the entity got its verdict.", ColumnRole.Label);

    private static IReadOnlyList<ColumnKind> IssueKinds =>
    [
        Title, Description, Status, Priority, CheckId, Verdict, CheckTitle, Citation, Reason,
    ];
}
