namespace Ara3D.BimOpenSchema;

/// <summary>Column names of issue, selection and verdict tables: the rows a check, an IDS
/// evaluation or a query produces and BCF carries. Every reader and writer of these tables
/// takes its names from here (TKT-182).</summary>
public static class IssueColumns
{
    public const string GlobalId = "GlobalId";
    public const string EntityIndex = "EntityIndex";
    public const string Title = "Title";
    public const string Description = "Description";
    public const string Status = "Status";
    public const string Priority = "Priority";
    public const string Type = "Type";

    public const string CheckId = "CheckId";
    public const string Verdict = "Verdict";
    public const string CheckTitle = "CheckTitle";
    public const string Citation = "Citation";
    public const string Reason = "Reason";
}
