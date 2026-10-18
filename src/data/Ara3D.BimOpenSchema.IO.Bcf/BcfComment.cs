namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>One comment on a topic. An empty <see cref="Author"/> or a null <see cref="Date"/>
/// is written as the file's <see cref="BcfOptions.Author"/> and
/// <see cref="BcfOptions.CreationDate"/>, because BCF requires both on every comment.</summary>
public sealed record BcfComment(string Author, DateTimeOffset? Date, string Text);
