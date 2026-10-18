namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>One BCF topic: a title, the GlobalIds of the elements it is about, and optional
/// text fields. The title is the topic's identity within one file: its folder GUID is derived
/// from it (see <see cref="BcfOptions.GuidSeed"/>), so two issues with one title are an error.
/// Blank and repeated GlobalIds are dropped when written. An empty string means the field is
/// absent; <see cref="Type"/> and <see cref="Status"/> then fall back to
/// <see cref="BcfOptions.DefaultTopicType"/> and <see cref="BcfOptions.DefaultTopicStatus"/>.
/// A <see cref="Camera"/> a viewer captured is written as the topic's viewpoint in place of the
/// one framed from the elements' bounds, and gives a topic with no geometry a viewpoint too.
/// <see cref="Comments"/> are written in order.</summary>
public sealed record BcfIssue(
    string Title,
    IReadOnlyList<string> GlobalIds,
    string Description = "",
    string Status = "",
    string Priority = "",
    string Type = "",
    BcfCamera? Camera = null,
    IReadOnlyList<BcfComment>? Comments = null);
