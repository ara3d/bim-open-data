using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>One topic as read from a BCF container: its markup fields and the GlobalIds its
/// viewpoints select. A topic this library wrote without geometry lists its GlobalIds at the end
/// of the description instead (see <see cref="BcfTopic"/>); the reader recovers those too.</summary>
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
    bool HasViewpoint);

/// <summary>Reads a BCF container (3.0, or 2.1's markup) back into topics, and
/// renders them as an issues table with the <see cref="IssueColumns"/> names, so a colleague's
/// comments come back into a graph the same way a check's verdicts went out.</summary>
public static class BcfReader
{
    public static IReadOnlyList<BcfTopicRecord> ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static IReadOnlyList<BcfTopicRecord> Read(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = new Dictionary<string, ZipArchiveEntry>();
        foreach (var entry in zip.Entries)
            entries[entry.FullName.Replace('\\', '/')] = entry;
        return entries.Keys
            .Where(name => name.EndsWith("/" + BcfTopic.MarkupFile, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(name => Topic(entries, name))
            .ToList();
    }

    /// <summary>The <c>VersionId</c> of <c>bcf.version</c>, or "" when the container has none.</summary>
    public static string Version(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry(BcfWriter.VersionFile);
        return entry is null ? "" : (string?)Load(entry).Root?.Attribute("VersionId") ?? "";
    }

    /// <summary>One row per (topic, GlobalId), or one row with an empty GlobalId for a topic
    /// that selects nothing: GlobalId, Title, Description, Status, Priority, Type, then
    /// TopicGuid, Author, CreationDate.</summary>
    public static IDataTable ToTable(this IReadOnlyList<BcfTopicRecord> topics, string name = "issues")
    {
        var rows = topics
            .SelectMany(t => t.GlobalIds.Count == 0 ? [(t, "")] : t.GlobalIds.Select(id => (t, id)))
            .ToList();
        var builder = new DataTableBuilder(name);
        builder.AddColumn(rows.Select(r => (object?)r.Item2).ToArray(), IssueColumns.GlobalId, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Title).ToArray(), IssueColumns.Title, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Description).ToArray(), IssueColumns.Description, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Status).ToArray(), IssueColumns.Status, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Priority).ToArray(), IssueColumns.Priority, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Type).ToArray(), IssueColumns.Type, typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Guid).ToArray(), "TopicGuid", typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.Author).ToArray(), "Author", typeof(string));
        builder.AddColumn(rows.Select(r => (object?)r.t.CreationDate?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).ToArray(),
            "CreationDate", typeof(string));
        return builder.Build();
    }

    private static BcfTopicRecord Topic(Dictionary<string, ZipArchiveEntry> entries, string markupName)
    {
        var folder = markupName[..(markupName.Length - BcfTopic.MarkupFile.Length)];
        var markup = Load(entries[markupName]);
        var topic = markup.Root?.Element(Name(markup, "Topic"))
            ?? throw new InvalidDataException($"{markupName} has no Topic element");
        var viewpointFiles = ViewpointFiles(markup, topic).Select(f => folder + f).ToList();
        var description = Text(topic, markup, "Description");
        var globalIds = viewpointFiles
            .Where(entries.ContainsKey)
            .SelectMany(f => SelectedIds(Load(entries[f])))
            .Distinct()
            .ToList();
        if (globalIds.Count == 0)
            (description, globalIds) = DescribedIds(description);
        return new(
            (string?)topic.Attribute("Guid") ?? folder.TrimEnd('/'),
            Text(topic, markup, "Title"),
            description,
            (string?)topic.Attribute("TopicType") ?? "",
            (string?)topic.Attribute("TopicStatus") ?? "",
            Text(topic, markup, "Priority"),
            Text(topic, markup, "CreationAuthor"),
            DateTimeOffset.TryParse(Text(topic, markup, "CreationDate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null,
            globalIds,
            viewpointFiles.Count > 0);
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

    /// <summary>The components a viewpoint selects: the IfcGuid attribute, else the AuthoringToolId element.</summary>
    private static IEnumerable<string> SelectedIds(XDocument viewpoint)
        => viewpoint.Descendants(Name(viewpoint, "Selection"))
            .SelectMany(s => s.Elements(Name(viewpoint, "Component")))
            .Select(c => (string?)c.Attribute("IfcGuid") ?? (string?)c.Element(Name(viewpoint, "AuthoringToolId")) ?? "")
            .Where(id => id.Length > 0);

    /// <summary>The GlobalIds <see cref="BcfTopic"/> appends to a description when a topic has no
    /// viewpoint, and the description with that list removed, so writing the topic again does
    /// not list them twice. The list runs to the end of its line; text after it stays.</summary>
    private static (string Description, List<string> GlobalIds) DescribedIds(string description)
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
        var ids = line[(colon + marker.Length)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
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
}
