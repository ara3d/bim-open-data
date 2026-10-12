using System.Globalization;
using Ara3D.BimOpenSchema;
using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>Which columns of an issues table hold what. Names match case-insensitively, so
/// <c>GlobalId</c> from DuckDB's <c>EntityText</c> view and <c>CheckTitle</c> from a verdict
/// table both resolve. Only <see cref="GlobalId"/> and <see cref="Title"/> must exist; a
/// missing optional column leaves that field empty. <see cref="AuthoringToolId"/> names the column
/// <see cref="BcfReader.ToTable"/> puts an authoring-tool id in (a Revit UniqueId, say), so a
/// table read from a BCF writes back with every component it named.</summary>
public sealed record BcfColumns(
    string GlobalId = IssueColumns.GlobalId,
    string Title = IssueColumns.Title,
    string Description = IssueColumns.Description,
    string Status = IssueColumns.Status,
    string Priority = IssueColumns.Priority,
    string Type = IssueColumns.Type,
    string AuthoringToolId = BcfReader.AuthoringToolIdColumn)
{
    /// <summary>A verdict table (the <c>check.*</c> nodes' columns, or <c>Ara3D.Ids</c>'s
    /// <c>IdsVerdict</c> rows as a table): one topic per check title, the citation as its
    /// description. Filter it to the failing rows first; every row becomes part of a topic.</summary>
    public static readonly BcfColumns Verdicts = new(Title: IssueColumns.CheckTitle, Description: IssueColumns.Citation);
}

/// <summary>Turns a table into issues: one issue per distinct title, in order of first appearance.</summary>
public static class BcfIssueTable
{
    /// <summary>Groups rows by title. Each issue lists its rows' non-empty ids once each, in row
    /// order: a row's GlobalId, or its AuthoringToolId when the GlobalId is empty (a row with
    /// neither still makes the topic, as a model-level issue). Its description is its rows'
    /// distinct non-empty descriptions joined by line breaks. Status, priority, and type
    /// must agree within a title. Null, DBNull, and blank cells count as empty.</summary>
    /// <exception cref="ArgumentException">The GlobalId or title column is missing, a row has no
    /// title, or rows of one title disagree on status, priority, or type.</exception>
    public static IReadOnlyList<BcfIssue> ToBcfIssues(this IDataTable table, BcfColumns? columns = null)
    {
        columns ??= new BcfColumns();
        var globalId = Require(table, columns.GlobalId);
        var authoringToolId = Find(table, columns.AuthoringToolId);
        var title = Require(table, columns.Title);
        var description = Find(table, columns.Description);
        var status = Find(table, columns.Status);
        var priority = Find(table, columns.Priority);
        var type = Find(table, columns.Type);

        var groups = new Dictionary<string, Group>();
        var order = new List<Group>();
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var key = Text(title, row);
            if (key.Length == 0)
                throw new ArgumentException($"Row {row} of table '{table.Name}' has no '{title.Descriptor.Name}'");
            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group(key);
                groups.Add(key, group);
                order.Add(group);
            }
            var id = Text(globalId, row);
            group.Add(id.Length > 0 ? id : Text(authoringToolId, row), Text(description, row));
            group.Agree(nameof(BcfIssue.Status), ref group.Status, Text(status, row));
            group.Agree(nameof(BcfIssue.Priority), ref group.Priority, Text(priority, row));
            group.Agree(nameof(BcfIssue.Type), ref group.Type, Text(type, row));
        }

        return order.Select(g => g.ToIssue()).ToList();
    }

    /// <summary>Whether an id is in IFC's 22-character GlobalId form. The writer emits such an id
    /// as a component's IfcGuid and any other as its AuthoringToolId.</summary>
    public static bool IsGlobalId(string id) => BcfXml.IsIfcGuid(id);

    private static IDataColumn Require(IDataTable table, string name)
        => Find(table, name) ?? throw new ArgumentException(
            $"Table '{table.Name}' has no '{name}' column; its columns are {string.Join(", ", table.Columns.Select(c => c.Descriptor.Name))}");

    private static IDataColumn? Find(IDataTable table, string name)
        => table.Columns.FirstOrDefault(c => c.Descriptor.Name == name)
           ?? table.Columns.FirstOrDefault(c => string.Equals(c.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Text(IDataColumn? column, int row)
        => column?[row] is { } value and not DBNull
            ? Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";

    private sealed class Group(string title)
    {
        private readonly List<string> _globalIds = [];
        private readonly HashSet<string> _seenIds = [];
        private readonly List<string> _descriptions = [];
        private readonly HashSet<string> _seenDescriptions = [];
        public string Status = "";
        public string Priority = "";
        public string Type = "";

        public void Add(string globalId, string description)
        {
            if (globalId.Length > 0 && _seenIds.Add(globalId))
                _globalIds.Add(globalId);
            if (description.Length > 0 && _seenDescriptions.Add(description))
                _descriptions.Add(description);
        }

        public void Agree(string field, ref string current, string value)
        {
            if (value.Length == 0 || value == current)
                return;
            if (current.Length > 0)
                throw new ArgumentException($"Topic '{title}' has two values for {field}: '{current}' and '{value}'");
            current = value;
        }

        public BcfIssue ToIssue()
            => new(title, _globalIds, string.Join("\n", _descriptions), Status, Priority, Type);
    }
}
