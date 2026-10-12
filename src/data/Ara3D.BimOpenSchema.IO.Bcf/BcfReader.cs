using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>One topic as read from a BCF container: its markup fields and the components its
/// viewpoints select, split by kind of id. <see cref="GlobalIds"/> holds IFC GlobalIds (a
/// component's <c>IfcGuid</c>); <see cref="AuthoringToolIds"/> holds the ids of components that
/// have only an <c>AuthoringToolId</c>, such as a Revit UniqueId. A topic this library wrote
/// without geometry lists its ids at the end of the description instead (see
/// <see cref="BcfTopic"/>); the reader recovers those and sorts them by the writer's rule. A
/// topic has a viewpoint when at least one viewpoint its markup lists was read.</summary>
public sealed record BcfTopicRecord(
    string Guid,
    string Title,
    string Description,
    string Type,
    string Status,
    string Priority,
    string Author,
    DateTimeOffset? CreationDate,
    IReadOnlyList<string> GlobalIds,
    IReadOnlyList<string> AuthoringToolIds,
    bool HasViewpoint);

/// <summary>What <see cref="BcfReader"/> read: the topics, and one warning per part it skipped
/// (a markup or viewpoint that is not XML, a markup with no Topic element, a viewpoint a markup
/// lists that the container does not hold), each naming the zip entry.</summary>
public sealed record BcfContents(IReadOnlyList<BcfTopicRecord> Topics, IReadOnlyList<string> Warnings);

/// <summary>Reads a BCF container (3.0, or 2.1's markup) back into topics, and
/// renders them as an issues table with the <see cref="IssueColumns"/> names, so a colleague's
/// comments come back into a graph the same way a check's verdicts went out. A part that cannot
/// be read is skipped with a warning, so one bad viewpoint does not lose the other topics; only a
/// file that is not a zip throws.</summary>
public static class BcfReader
{
    /// <summary>The issues-table column holding an id from a component's <c>AuthoringToolId</c>.</summary>
    public const string AuthoringToolIdColumn = "AuthoringToolId";

    public static BcfContents ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static BcfContents Read(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = new Dictionary<string, ZipArchiveEntry>();
        foreach (var entry in zip.Entries)
            entries[entry.FullName.Replace('\\', '/')] = entry;
        var warnings = new List<string>();
        var topics = entries.Keys
            .Where(name => name.EndsWith("/" + BcfTopic.MarkupFile, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(name => Topic(entries, name, warnings))
            .OfType<BcfTopicRecord>()
            .ToList();
        return new(topics, warnings);
    }

    /// <summary>The <c>VersionId</c> of <c>bcf.version</c>, or "" when the container has none.</summary>
    public static string Version(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry(BcfWriter.VersionFile);
        return entry is null ? "" : (string?)Load(entry).Root?.Attribute("VersionId") ?? "";
    }

    /// <summary>One row per topic and selected element, or one row with both ids empty for a
    /// topic that selects nothing: GlobalId, Title, Description, Status, Priority, Type, then
    /// TopicGuid, Author, CreationDate, AuthoringToolId. A row from a GlobalId has an empty
    /// AuthoringToolId, and a row from an authoring-tool id has an empty GlobalId.</summary>
    public static IDataTable ToTable(this IReadOnlyList<BcfTopicRecord> topics, string name = "issues")
    {
        var rows = topics
            .SelectMany(t => t.GlobalIds.Count + t.AuthoringToolIds.Count == 0
                ? [(t, GlobalId: "", AuthoringToolId: "")]
                : t.GlobalIds.Select(id => (t, GlobalId: id, AuthoringToolId: ""))
                    .Concat(t.AuthoringToolIds.Select(id => (t, GlobalId: "", AuthoringToolId: id))))
            .ToList();
        var builder = new DataTableBuilder(name);
        builder.AddColumn(rows.Select(r => (object?)r.GlobalId).ToArray(), IssueColumns.GlobalId, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Title).ToArray(), IssueColumns.Title, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Description).ToArray(), IssueColumns.Description, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Status).ToArray(), IssueColumns.Status, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Priority).ToArray(), IssueColumns.Priority, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Type).ToArray(), IssueColumns.Type, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Guid).ToArray(), "TopicGuid", typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Author).ToArray(), "Author", typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.CreationDate?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).ToArray(),
            "CreationDate", typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.AuthoringToolId).ToArray(), AuthoringToolIdColumn, typeof(string));
        return builder.Build();
    }

    /// <summary>The topic in one markup, or null with a warning when the markup is not XML or has
    /// no Topic element. A listed viewpoint that is missing or not XML is skipped with a warning.</summary>
    private static BcfTopicRecord? Topic(Dictionary<string, ZipArchiveEntry> entries, string markupName, List<string> warnings)
    {
        var folder = markupName[..(markupName.Length - BcfTopic.MarkupFile.Length)];
        var markup = TryLoad(entries[markupName], markupName, warnings);
        if (markup is null)
            return null;
        var topic = markup.Root?.Element(Name(markup, "Topic"));
        if (topic is null)
        {
            warnings.Add($"{markupName} has no Topic element; skipped");
            return null;
        }
        var viewpoints = ViewpointFiles(markup, topic)
            .Select(f => folder + f)
            .Select(f => entries.TryGetValue(f, out var entry) ? TryLoad(entry, f, warnings) : Missing(f, markupName, warnings))
            .OfType<XDocument>()
            .ToList();
        var components = viewpoints.SelectMany(SelectedIds).Distinct().ToList();
        var description = Text(topic, markup, "Description");
        if (components.Count == 0)
            (description, components) = DescribedIds(description);
        return new(
            (string?)topic.Attribute("Guid") ?? folder.TrimEnd('/'),
            Text(topic, markup, "Title"),
            description,
            (string?)topic.Attribute("TopicType") ?? "",
            (string?)topic.Attribute("TopicStatus") ?? "",
            Text(topic, markup, "Priority"),
            Text(topic, markup, "CreationAuthor"),
            DateTimeOffset.TryParse(Text(topic, markup, "CreationDate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null,
            components.Where(c => c.IsGlobalId).Select(c => c.Id).ToList(),
            components.Where(c => !c.IsGlobalId).Select(c => c.Id).ToList(),
            viewpoints.Count > 0);
    }

    private static XDocument? Missing(string viewpoint, string markupName, List<string> warnings)
    {
        warnings.Add($"{markupName} lists {viewpoint}, which is not in the container; skipped");
        return null;
    }

    /// <summary>The viewpoint file names a markup lists. BCF 3.0 puts them under
    /// <c>Topic/Viewpoints/ViewPoint/Viewpoint</c>; BCF 2.1 puts one <c>Viewpoints</c> element per
    /// viewpoint beside the topic, each with a <c>Viewpoint</c> child. Both are read.</summary>
    private static IEnumerable<string> ViewpointFiles(XDocument markup, XElement topic)
        => (topic.Element(Name(markup, "Viewpoints"))?.Elements(Name(markup, "ViewPoint")) ?? [])
            .Concat(markup.Root?.Elements(Name(markup, "Viewpoints")) ?? [])
            .Select(v => (string?)v.Element(Name(markup, "Viewpoint")))
            .Where(f => !string.IsNullOrEmpty(f))
            .Select(f => f!)
            .Distinct();

    /// <summary>The components a viewpoint selects: the IfcGuid attribute as a GlobalId, else the
    /// AuthoringToolId element. A component with both is read by its IfcGuid alone.</summary>
    private static IEnumerable<(string Id, bool IsGlobalId)> SelectedIds(XDocument viewpoint)
        => viewpoint.Descendants(Name(viewpoint, "Selection"))
            .SelectMany(s => s.Elements(Name(viewpoint, "Component")))
            .Select(c => (string?)c.Attribute("IfcGuid") is { Length: > 0 } guid
                ? (Id: guid, IsGlobalId: true)
                : (Id: ((string?)c.Element(Name(viewpoint, "AuthoringToolId")))?.Trim() ?? "", IsGlobalId: false))
            .Where(c => c.Id.Length > 0);

    /// <summary>The ids <see cref="BcfTopic"/> appends to a description when a topic has no
    /// viewpoint, and the description with that list removed, so writing the topic again does
    /// not list them twice. The list runs to the end of its line; text after it stays. An id in
    /// IFC's 22-character form is a GlobalId and any other is an authoring-tool id, the rule
    /// <see cref="BcfXml.Visinfo"/> writes a viewpoint by, so a topic reads the same with or
    /// without geometry.</summary>
    private static (string Description, List<(string Id, bool IsGlobalId)> Ids) DescribedIds(string description)
    {
        const string marker = "): ";
        var start = description.LastIndexOf("Elements (no viewpoint", StringComparison.Ordinal);
        if (start < 0)
            return (description, []);
        var end = description.IndexOf('\n', start);
        var line = end < 0 ? description[start..] : description[start..end];
        var colon = line.IndexOf(marker, StringComparison.Ordinal);
        if (colon < 0)
            return (description, []);
        var ids = line[(colon + marker.Length)..]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(id => (Id: id, IsGlobalId: BcfXml.IsIfcGuid(id)))
            .ToList();
        var rest = (description[..start] + (end < 0 ? "" : description[end..])).Trim();
        return (rest, ids);
    }

    private static string Text(XElement topic, XDocument document, string element)
        => ((string?)topic.Element(Name(document, element)))?.Trim() ?? "";

    /// <summary>BCF 3.0 markup has no namespace; a 2.1 file may declare one. Either way the element
    /// is looked up in the root's namespace.</summary>
    private static XName Name(XDocument document, string local)
        => (document.Root?.Name.Namespace ?? XNamespace.None) + local;

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XDocument? TryLoad(ZipArchiveEntry entry, string name, List<string> warnings)
    {
        try
        {
            return Load(entry);
        }
        catch (XmlException e)
        {
            warnings.Add($"{name} is not XML ({e.Message}); skipped");
            return null;
        }
    }
}
