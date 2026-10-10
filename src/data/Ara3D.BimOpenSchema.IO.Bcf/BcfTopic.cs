namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>One topic as it is written: an issue with its GUIDs, its resolved type and status,
/// and its camera. <see cref="Camera"/> is null when no element of the issue has geometry, and
/// then the topic has no viewpoint, because BCF 3.0 requires a camera in every viewpoint.</summary>
internal sealed record BcfTopic(
    string Guid,
    string ViewpointGuid,
    string Title,
    string Description,
    string Type,
    string Status,
    string Priority,
    IReadOnlyList<string> GlobalIds,
    BcfCamera? Camera)
{
    public const string ViewpointFile = "viewpoint.bcfv";
    public const string MarkupFile = "markup.bcf";

    public bool HasViewpoint => Camera is not null;

    public static BcfTopic Plan(BcfIssue issue, BcfOptions options, ElementBounds? bounds)
    {
        var title = issue.Title.Trim();
        var guid = BcfGuid.Topic(options.GuidSeed, title);
        var globalIds = issue.GlobalIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct().ToList();
        var box = bounds?.Union(globalIds);
        var camera = box is { } b ? BcfCamera.Frame(b, options.FieldOfView, options.AspectRatio) : (BcfCamera?)null;
        return new(
            guid,
            BcfGuid.Viewpoint(guid),
            title,
            Describe(issue.Description, globalIds, camera is not null),
            OrDefault(issue.Type, options.DefaultTopicType),
            OrDefault(issue.Status, options.DefaultTopicStatus),
            issue.Priority.Trim(),
            globalIds,
            camera);
    }

    /// <summary>A topic without a viewpoint has nowhere else in BCF to list its elements, so their
    /// GlobalIds go at the end of the description, where a person can still find them.</summary>
    private static string Describe(string text, IReadOnlyList<string> globalIds, bool hasViewpoint)
    {
        var description = text.Trim();
        if (hasViewpoint || globalIds.Count == 0)
            return description;
        var list = $"Elements (no viewpoint: no geometry to place a camera on): {string.Join(", ", globalIds)}";
        return description.Length == 0 ? list : $"{description}\n\n{list}";
    }

    private static string OrDefault(string value, string fallback)
        => value.Trim() is { Length: > 0 } v ? v : fallback;
}
