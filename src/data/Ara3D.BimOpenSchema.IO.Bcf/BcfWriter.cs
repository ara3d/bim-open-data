using System.IO.Compression;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>What a write produced. <see cref="ElementsWithoutGeometry"/> counts selected
/// elements that no box was found for (every element, when no geometry was given); they are
/// still listed, in their topic's viewpoint or, when it has none, its description.</summary>
public sealed record BcfSummary(int Topics, int Viewpoints, int Elements, int ElementsWithoutGeometry);

/// <summary>Writes a BCF 3.0 container: <c>bcf.version</c>, <c>extensions.xml</c>, and per topic
/// a folder named by its GUID holding <c>markup.bcf</c> and, when any of its elements has
/// geometry in <c>bounds</c>, a <c>viewpoint.bcfv</c> that selects them under a perspective
/// camera framing their box. Without geometry no viewpoint is written: BCF 3.0 requires a
/// camera in every viewpoint, and this writer does not invent one.</summary>
public static class BcfWriter
{
    public const string VersionFile = "bcf.version";
    public const string ExtensionsFile = "extensions.xml";

    /// <summary>Writes the container to a stream. Every topic is planned and checked before the
    /// first byte is written, so an invalid input leaves the stream untouched.</summary>
    /// <exception cref="ArgumentException">An issue has a blank title, or two issues share one.</exception>
    public static BcfSummary Write(Stream output, IReadOnlyList<BcfIssue> issues, BcfOptions? options = null, ElementBounds? bounds = null)
    {
        options ??= new BcfOptions();
        return Write(output, Plan(issues, options, bounds), options, bounds);
    }

    /// <summary>Writes to a file and creates its folder. The input is checked before any file is
    /// opened, and the container is written beside the target and moved over it only when
    /// complete, so a failure leaves an existing file as it was and no partial file behind.</summary>
    /// <exception cref="ArgumentException">An issue has a blank title, or two issues share one.</exception>
    public static BcfSummary WriteFile(string path, IReadOnlyList<BcfIssue> issues, BcfOptions? options = null, ElementBounds? bounds = null)
    {
        options ??= new BcfOptions();
        var topics = Plan(issues, options, bounds);
        return ReplaceFile(path, stream => Write(stream, topics, options, bounds));
    }

    /// <summary>Runs <paramref name="write"/> on a new temporary file in the target's folder, then
    /// moves it over <paramref name="path"/>. If <paramref name="write"/> throws, the temporary
    /// file is deleted and the target is not touched.</summary>
    internal static T ReplaceFile<T>(string path, Func<Stream, T> write)
    {
        var target = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            T result;
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                result = write(stream);
            File.Move(temporary, target, overwrite: true);
            return result;
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private static List<BcfTopic> Plan(IReadOnlyList<BcfIssue> issues, BcfOptions options, ElementBounds? bounds)
    {
        var topics = issues.Select(i => BcfTopic.Plan(i, options, bounds)).ToList();
        RequireDistinctTitles(topics);
        return topics;
    }

    private static BcfSummary Write(Stream output, IReadOnlyList<BcfTopic> topics, BcfOptions options, ElementBounds? bounds)
    {
        var created = new DateTimeOffset(options.CreationDate.UtcDateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, VersionFile, BcfXml.Version(), created);
            Add(zip, ExtensionsFile, BcfXml.Extensions(topics), created);
            foreach (var topic in topics)
            {
                zip.CreateEntry(topic.Guid + "/").LastWriteTime = created;
                Add(zip, $"{topic.Guid}/{BcfTopic.MarkupFile}", BcfXml.Markup(topic, options.Author, created), created);
                if (topic.HasViewpoint)
                    Add(zip, $"{topic.Guid}/{BcfTopic.ViewpointFile}", BcfXml.Visinfo(topic), created);
            }
        }

        return new(
            topics.Count,
            topics.Count(t => t.HasViewpoint),
            topics.Sum(t => t.GlobalIds.Count),
            topics.Sum(t => t.GlobalIds.Count(id => bounds is null || !bounds.TryGet(id, out _))));
    }

    private static void RequireDistinctTitles(IReadOnlyList<BcfTopic> topics)
    {
        for (var i = 0; i < topics.Count; i++)
            if (topics[i].Title.Length == 0)
                throw new ArgumentException($"Issue {i} has a blank title; BCF requires one");
        var duplicate = topics.GroupBy(t => t.Title).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Two issues share the title '{duplicate.Key}'; the title is the topic's identity, so merge them or tell them apart");
    }

    private static void Add(ZipArchive zip, string name, byte[] content, DateTimeOffset time)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = time;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
