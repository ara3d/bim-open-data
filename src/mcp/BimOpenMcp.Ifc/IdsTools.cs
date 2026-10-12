using Ara3D.DataTable;
using Ara3D.Ids;
using Ara3D.MCP;

namespace BimOpenMcp.Ifc;

/// <summary>Checks a model against a buildingSMART IDS (Information Delivery Specification) with
/// <see cref="IdsEvaluator"/> (TKT-50). Two tools rather than one with a mode: listing the
/// specifications needs no model, and a mode would make 'path' required for one call and ignored
/// by the other, which the schema cannot say. Tables come back in the shape ifc_sql uses (columns,
/// rows, and the unpaged total), with the column names of the issue table <c>check.rule</c> and
/// the <c>ids.check</c> node produce.</summary>
public static class IdsTools
{
    public static McpServer Register(this McpServer mcp, IfcSessionCache cache)
        => mcp
            .Tool(
                "ifc_ids_check",
                "Checks a model against an IDS file. IDS (Information Delivery Specification, "
                + "buildingSMART) is an XML file of specifications; each one selects elements "
                + "(applicability, e.g. every IfcDoor) and states what they must carry "
                + "(requirements, e.g. attribute OverallWidth at least 0.85). Returns 'summary', "
                + "one row per specification in the IDS with its Pass, Fail and InfoNotAvailable "
                + "counts; 'warnings'; and 'verdicts', a page of rows, one per element and "
                + "specification: GlobalId, EntityIndex, CheckId (the specification's name), "
                + "CheckTitle, Citation (the requirements as the IDS states them), Verdict, Reason. "
                + "Verdicts: Pass means the element meets every requirement; Fail means it breaks "
                + "one, and Reason says which; InfoNotAvailable means the check could not be made "
                + "(a facet this checker does not support, or data the model lacks) and is never a "
                + "pass, so report it as unchecked. A required specification that matches no "
                + "element gets one Fail row with an empty GlobalId. Always read 'warnings' and "
                + "report them: a facet the IDS loader skipped (a misspelling, say), an unsupported "
                + "facet, or a number compared as stored is listed only there, and means a "
                + "requirement was not checked as written. Set 'verdict' to 'Fail' to page through "
                + "the failures only. IDS values are in SI units (metres, square metres, cubic "
                + "metres); the model's lengths, areas and volumes are converted to SI when their "
                + "unit can be told, and a warning names each value compared as stored instead. "
                + "Call ifc_ids_specifications "
                + "first to see what an IDS asks. 'path' may name a .bos file instead of an IFC file.",
                IfcToolArgs.ModelOrBos()
                    .IdsPath()
                    .String("verdict", "Optional: return only verdict rows with this verdict, one of Pass, Fail, InfoNotAvailable. The summary always counts every row.")
                    .Paged()
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Check(cache, args.GetRequiredString("path"), args.GetRequiredString("idsPath"), ParseVerdict(args.GetString("verdict")), args.Skip(), args.Take()),
                    ["ifc_sql"]))
            .Tool(
                "ifc_ids_specifications",
                "Lists the specifications of an IDS file (Information Delivery Specification) without "
                + "reading a model: one row each with CheckId (its name), CheckTitle, IfcVersion, "
                + "Cardinality (required, optional, prohibited), Applicability (which elements it "
                + "selects), Requirements (what they must carry), and Supported (true when every "
                + "facet can be evaluated over a model that carries what the facet needs: material "
                + "and part-of facets need the model's relations, and give InfoNotAvailable in "
                + "ifc_ids_check over a model without them; false means some facet always gives "
                + "InfoNotAvailable, so that part goes unchecked). "
                + "'warnings' lists what the IDS loader skipped; a skipped facet is a requirement "
                + "nobody will check.",
                McpSchema.Object().IdsPath().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Specifications(args.GetRequiredString("idsPath")),
                    ["ifc_ids_check"]));

    private static McpSchemaBuilder IdsPath(this McpSchemaBuilder builder)
        => builder.String("idsPath", "Absolute path to the .ids file (buildingSMART IDS 1.0 XML).", required: true);

    /// <summary>Loads the IDS before the model, so a missing or unreadable IDS fails without the
    /// cost of converting an IFC file.</summary>
    private static object Check(IfcSessionCache cache, string path, string idsPath, Verdict? verdict, int skip, int take)
    {
        var document = IdsFile.Load(idsPath);
        using var lease = cache.LeaseBos(path);
        var report = IdsEvaluator.Evaluate(document, IdsModel.FromBimData(lease.Session.Data));
        var shown = verdict is { } only ? report with { Verdicts = report.Verdicts.Where(v => v.Verdict == only).ToList() } : report;
        return new
        {
            path,
            bosPath = lease.Session.BosPath.FullPath,
            idsPath = Path.GetFullPath(idsPath),
            summary = Whole(IdsTables.Summary(document, report)),
            warnings = report.Warnings,
            verdict = verdict?.ToString(),
            verdicts = Page(shown.ToTable(), skip, take),
        };
    }

    private static object Specifications(string idsPath)
    {
        var document = IdsFile.Load(idsPath);
        return new
        {
            idsPath = Path.GetFullPath(idsPath),
            specifications = Whole(IdsTables.Specifications(document)),
            warnings = document.Warnings,
        };
    }

    private static Verdict? ParseVerdict(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var name = Enum.GetNames<Verdict>().FirstOrDefault(n => n.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"'verdict' must be one of {string.Join(", ", Enum.GetNames<Verdict>())}; '{text}' is not one.");
        return Enum.Parse<Verdict>(name);
    }

    private static IfcQueryResult Whole(IDataTable table)
        => new(table.Rows.Count, 0, table.Rows.Count, Columns(table), table.Rows.Select(r => r.Values).ToList());

    private static IfcQueryResult Page(IDataTable table, int skip, int take)
    {
        var page = IfcShapes.Page(table.Rows, skip, take);
        return new(page.Total, page.Skip, page.Count, Columns(table), page.Items.Select(r => r.Values).ToList());
    }

    private static IReadOnlyList<string> Columns(IDataTable table)
        => table.Columns.Select(c => c.Descriptor.Name).ToList();
}
