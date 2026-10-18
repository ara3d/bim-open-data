namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>Settings for one BCF file. Every field has a default; tests pin
/// <see cref="CreationDate"/> so two writes of the same issues are byte-identical.</summary>
public sealed record BcfOptions
{
    /// <summary>Written as every topic's CreationDate (whole seconds, UTC) and as every zip
    /// entry's timestamp. BCF requires a date; the default is the moment the options are made.</summary>
    public DateTimeOffset CreationDate { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Every topic's CreationAuthor, which BCF requires. Tools show it as the creator.</summary>
    public string Author { get; init; } = "Ara3D.BimOpenSchema.IO.Bcf";

    /// <summary>Mixed into every topic GUID. Two exports with the same seed and title share a
    /// topic GUID, so re-importing a rerun check updates its topics; a different seed per
    /// check keeps unrelated checks with the same titles apart.</summary>
    public string GuidSeed { get; init; } = "";

    /// <summary>TopicType for an issue with an empty <see cref="BcfIssue.Type"/>.</summary>
    public string DefaultTopicType { get; init; } = "Issue";

    /// <summary>TopicStatus for an issue with an empty <see cref="BcfIssue.Status"/>.</summary>
    public string DefaultTopicStatus { get; init; } = "Open";

    /// <summary>Vertical field of view in degrees of a framed camera, and of a given camera that has none.</summary>
    public double FieldOfView { get; init; } = 60;

    /// <summary>Width over height of a framed camera, and of a given camera that has none. At 1 the camera frames the elements in
    /// both directions; a reader with a wider view only sees more around them.</summary>
    public double AspectRatio { get; init; } = 1;

    /// <summary>The model file the topics refer to, as a path or a file name; "" for none. When set,
    /// every topic's markup header lists it as an external file: its file name, and the path as
    /// given as the reference, so a viewer can find the model the camera and components belong to.</summary>
    public string ModelFile { get; init; } = "";

    /// <summary>The model file's date, written in the header when <see cref="ModelFile"/> is set.</summary>
    public DateTimeOffset? ModelDate { get; init; }
}
