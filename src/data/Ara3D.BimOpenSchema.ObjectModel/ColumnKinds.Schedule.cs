using System.Collections.Generic;

namespace Ara3D.BimOpenSchema;

// The bim.buildSequence columns: a synthetic construction schedule. ElementCount is registered
// with the room columns. Dates are ISO text (yyyy-MM-dd), as the node emits them.
public static partial class ColumnKinds
{
    public static readonly ColumnKind Task = new("Task", ColumnScalarType.Text,
        "One trade's work on one storey, named 'Storey - Trade'.", ColumnRole.Label);

    public static readonly ColumnKind Storey = new("Storey", ColumnScalarType.Text,
        "Name of the storey a scheduled task works.", ColumnRole.Category);

    public static readonly ColumnKind Trade = new("Trade", ColumnScalarType.Text,
        "The trade that does a task: Structure, Envelope, MEP, Finishes, or the names the node was given.", ColumnRole.Category);

    public static readonly ColumnKind Start = new("Start", ColumnScalarType.Text,
        "First day of a task, as an ISO date (yyyy-MM-dd).", ColumnRole.Measure);

    public static readonly ColumnKind End = new("End", ColumnScalarType.Text,
        "Day after a task's last day (exclusive), as an ISO date (yyyy-MM-dd).", ColumnRole.Measure);

    public static readonly ColumnKind DurationDays = new("DurationDays", ColumnScalarType.Integer,
        "Calendar days a task lasts.", ColumnRole.Measure);

    public static readonly ColumnKind Finish = new("Finish", ColumnScalarType.Text,
        "The End of the task an element is installed in, as an ISO date.", ColumnRole.Measure);

    public static readonly ColumnKind FinishWeek = new("FinishWeek", ColumnScalarType.Integer,
        "The week, counted from 1 at the schedule's start, that holds the task's last working day (End - 1).",
        ColumnRole.Measure);

    public static readonly ColumnKind Synthetic = new("Synthetic", ColumnScalarType.Boolean,
        "True when the value comes from a generated schedule, not from the model: an invented plan, not a recorded one.",
        ColumnRole.Category);

    private static IReadOnlyList<ColumnKind> ScheduleKinds =>
    [
        Task, Storey, Trade, Start, End, DurationDays, Finish, FinishWeek, Synthetic,
    ];
}
