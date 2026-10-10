namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Reads BOS table cells that may be absent. An index of -1 or past the end of its table
/// means the value is missing, and these return null for it rather than a default, so a
/// caller can leave the value out (PROJECT.md principle 3, honest absence).
/// </summary>
internal static class BosValues
{
    public static string? String(IBimData data, StringIndex index)
        => (uint)index < (uint)data.Strings.Length ? data.Strings[(int)index] : null;

    /// <summary>The string, or null when it is missing or empty. For identity fields
    /// (GlobalId, names) the IFC converter stores an unset value as "".</summary>
    public static string? NonEmptyString(IBimData data, StringIndex index)
        => String(data, index) is { Length: > 0 } s ? s : null;

    public static Entity? Entity(IBimData data, EntityIndex index)
        => (uint)index < (uint)data.Entities.Length ? data.Entities[(int)index] : null;

    public static string? EntityName(IBimData data, EntityIndex index)
        => Entity(data, index) is { } e ? NonEmptyString(data, e.Name) : null;

    public static string? DocumentTitle(IBimData data, DocumentIndex index)
        => (uint)index < (uint)data.Documents.Length ? NonEmptyString(data, data.Documents[(int)index].Title) : null;

    public static float? Number(IBimData data, NumberIndex index)
        => (uint)index < (uint)data.Numbers.Length ? data.Numbers[(int)index] : null;

    public static Point? Point(IBimData data, PointIndex index)
        => (uint)index < (uint)data.Points.Length ? data.Points[(int)index] : null;
}
