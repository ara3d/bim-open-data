using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ara3D.BimOpenSchema;

/// <summary>The registry of column kinds. Each kind is a static member in the file of its area
/// (<c>ColumnKinds.Element.cs</c> and so on); <see cref="All"/> lists them in area order.</summary>
public static partial class ColumnKinds
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<ColumnKind> All { get; }

    // A static constructor runs after every field initializer, whichever file declares it.
    static ColumnKinds()
        => All = [.. ElementKinds, .. BoundsKinds, .. RoomKinds, .. ParameterKinds, .. IssueKinds,
            .. OrientationKinds, .. ScheduleKinds, .. CheckKinds];

    /// <summary>The kind with this name, ignoring case, or null.</summary>
    public static ColumnKind? Find(string name)
        => All.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The registry as a JSON array in the order of <see cref="All"/>, for the editor
    /// and the guides to read without a build.</summary>
    public static string ToJson()
        => JsonSerializer.Serialize(All, JsonOptions);
}
