using System.Security.Cryptography;
using System.Text;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>Name-based GUIDs (RFC 4122 version 5, SHA-1) in the lowercase form BCF requires,
/// so the same input always gives the same topic and viewpoint GUIDs.</summary>
public static class BcfGuid
{
    /// <summary>This library's namespace for version 5 GUIDs; any fixed value would do.</summary>
    private static readonly byte[] Namespace = Convert.FromHexString("6b0b9f6e1f2a4c5e9c1d3a7e5f402bcf");

    private static string FromName(string name)
    {
        var hash = SHA1.HashData([.. Namespace, .. Encoding.UTF8.GetBytes(name)]);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    /// <summary>The GUID of the topic with this title under this seed.</summary>
    public static string Topic(string seed, string title)
        => FromName($"topic\u001f{seed}\u001f{title}");

    /// <summary>The GUID of a topic's one viewpoint.</summary>
    public static string Viewpoint(string topicGuid)
        => FromName($"viewpoint\u001f{topicGuid}");
}
