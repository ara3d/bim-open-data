using Ara3D.BimOpenSchema;
using Ara3D.DataTable;
using Xbim.InformationSpecifications;

namespace Ara3D.Ids;

/// <summary>The tables an IDS evaluation produces, with the issue-table column names of
/// <see cref="IssueColumns"/> (TKT-182), so a verdict table from an IDS reads like one from
/// <c>check.rule</c> and feeds the BCF writer unchanged.</summary>
public static class IdsTables
{
    /// <summary>One row per verdict: GlobalId, EntityIndex, CheckId, CheckTitle, Citation,
    /// Verdict, Reason.</summary>
    public static IDataTable ToTable(this IdsReport report, string name = "verdicts")
    {
        var rows = report.Verdicts;
        var builder = new DataTableBuilder(name);
        builder.AddColumn(rows.Select(v => (object?)v.GlobalId).ToArray(), IssueColumns.GlobalId, typeof(string));
        builder.AddColumn(rows.Select(v => (object?)v.EntityIndex).ToArray(), IssueColumns.EntityIndex, typeof(long));
        builder.AddColumn(rows.Select(v => (object?)v.CheckId).ToArray(), IssueColumns.CheckId, typeof(string));
        builder.AddColumn(rows.Select(v => (object?)v.CheckTitle).ToArray(), IssueColumns.CheckTitle, typeof(string));
        builder.AddColumn(rows.Select(v => (object?)v.Citation).ToArray(), IssueColumns.Citation, typeof(string));
        builder.AddColumn(rows.Select(v => (object?)v.Verdict.ToString()).ToArray(), IssueColumns.Verdict, typeof(string));
        builder.AddColumn(rows.Select(v => (object?)v.Reason).ToArray(), IssueColumns.Reason, typeof(string));
        return builder.Build();
    }

    /// <summary>One row per specification, before any model is read: CheckId, CheckTitle,
    /// IfcVersion, Cardinality, Applicability, Requirements, and Supported (false when a facet
    /// would give InfoNotAvailable).</summary>
    public static IDataTable Specifications(Xids ids, string name = "specifications")
    {
        var specs = ids.AllSpecifications().ToList();
        var builder = new DataTableBuilder(name);
        builder.AddColumn(specs.Select(s => (object?)IdsEvaluator.CheckId(s)).ToArray(), IssueColumns.CheckId, typeof(string));
        builder.AddColumn(specs.Select(s => (object?)IdsEvaluator.CheckTitle(s)).ToArray(), IssueColumns.CheckTitle, typeof(string));
        builder.AddColumn(specs.Select(s => (object?)string.Join(", ", s.IfcVersion ?? [])).ToArray(), "IfcVersion", typeof(string));
        builder.AddColumn(specs.Select(s => (object?)(s.Cardinality?.Description ?? "")).ToArray(), "Cardinality", typeof(string));
        builder.AddColumn(specs.Select(s => (object?)Facets(s.Applicability)).ToArray(), "Applicability", typeof(string));
        builder.AddColumn(specs.Select(s => (object?)IdsEvaluator.Citation(s)).ToArray(), "Requirements", typeof(string));
        builder.AddColumn(specs.Select(s => (object?)IsSupported(s)).ToArray(), "Supported", typeof(bool));
        return builder.Build();
    }

    private static string Facets(FacetGroup? group)
        => string.Join("; ", IdsEvaluator.Facets(group).Select(f => f.Short()));

    private static bool IsSupported(Specification specification)
        => IdsEvaluator.Facets(specification.Applicability).Concat(IdsEvaluator.Facets(specification.Requirement))
            .All(IdsFacets.IsSupported);
}
