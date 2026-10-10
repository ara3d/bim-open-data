using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Google.FlatBuffers;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>A file this reader cannot read as Fragments 2; the message names what it found.</summary>
public sealed class FragmentsFormatException(string message) : FormatException(message);

/// <summary>Opens the bytes of a <c>.frag</c> file as the FlatBuffers root <see cref="Model"/>.
/// That Open's writers zlib-deflate the buffer unless asked for a raw one, and finish it without
/// a file identifier; a buffer that does carry an identifier must carry "0001".</summary>
public static class FragmentsFile
{
    public const string FileIdentifier = "0001";

    /// <summary>The major version of @thatopen/fragments that writes Fragments 2 files.</summary>
    public const int SupportedMajorVersion = 3;

    public static Model Open(byte[] bytes)
    {
        var raw = IsZlib(bytes) ? Inflate(bytes) : bytes;
        var buffer = new ByteBuffer(raw);
        CheckIdentifier(buffer);
        var model = Root(buffer);
        CheckVersion(model.Metadata);
        return model;
    }

    /// <summary>The root table, checked for the fields index.fbs marks required at the model level.
    /// Google.FlatBuffers' Verifier (25.2.10) is not used: it throws OverflowException in
    /// GetVRelOffset on files That Open's importer writes. ByteBuffer checks every read against
    /// the buffer's bounds, so a damaged file fails with an exception rather than reading
    /// outside it.</summary>
    private static Model Root(ByteBuffer buffer)
    {
        var rootOffset = buffer.GetInt(0);
        if (rootOffset < 8 || rootOffset > buffer.Length - 4)
            throw new FragmentsFormatException(
                $"Not a Fragments 2 model: the root table offset {rootOffset} lies outside the {buffer.Length}-byte buffer");
        try
        {
            var model = Model.GetRootAsModel(buffer);
            var missing = new[]
            {
                (model.Meshes.HasValue, "meshes"),
                (model.Guid != null, "guid"),
                (model.LocalIdsLength > 0 || model.GetLocalIdsBytes() != null, "local_ids"),
            }.Where(f => !f.Item1).Select(f => f.Item2).ToList();
            if (missing.Count > 0)
                throw new FragmentsFormatException(
                    $"Not a Fragments 2 model: required fields missing from the root table: {string.Join(", ", missing)}");
            return model;
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new FragmentsFormatException($"Not a Fragments 2 model: the root table reads outside the buffer ({e.Message})");
        }
    }

    /// <summary>The zlib header test That Open's loader uses (isRawBuffer in its buffer utilities):
    /// compression method 8 and a header that is a multiple of 31.</summary>
    public static bool IsZlib(byte[] bytes)
        => bytes.Length >= 2 && (bytes[0] & 0x0f) == 8 && ((bytes[0] << 8) | bytes[1]) % 31 == 0;

    private static byte[] Inflate(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        try
        {
            zlib.CopyTo(output);
        }
        catch (InvalidDataException e)
        {
            throw new FragmentsFormatException($"The file starts with a zlib header but does not inflate: {e.Message}");
        }
        return output.ToArray();
    }

    /// <summary>A buffer finished without an identifier has the root table's padding or vtable in
    /// bytes 4 to 8; only four printable characters are read as an identifier.</summary>
    private static void CheckIdentifier(ByteBuffer buffer)
    {
        if (buffer.Length < 8)
            throw new FragmentsFormatException($"Not a Fragments 2 model: {buffer.Length} bytes is too short for a FlatBuffers root");
        var id = buffer.ToArray(4, 4);
        if (!id.All(b => b is >= 0x20 and < 0x7f))
            return;
        var identifier = Encoding.ASCII.GetString(id);
        if (identifier != FileIdentifier)
            throw new FragmentsFormatException(
                $"Unsupported file identifier '{identifier}': Fragments 2 files carry '{FileIdentifier}' or none");
    }

    /// <summary>Files from @thatopen/fragments 3.4 and later record the writer's version in the
    /// metadata JSON; an earlier file has no version and is read as Fragments 2.</summary>
    private static void CheckVersion(string? metadata)
    {
        var version = MetadataVersion(metadata);
        if (version == null || !int.TryParse(version.Split('.')[0], out var major) || major == SupportedMajorVersion)
            return;
        throw new FragmentsFormatException(
            $"Unsupported Fragments version '{version}': this reader reads files written by @thatopen/fragments {SupportedMajorVersion}.x (Fragments 2)");
    }

    private static string? MetadataVersion(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("version", out var v)
                && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
