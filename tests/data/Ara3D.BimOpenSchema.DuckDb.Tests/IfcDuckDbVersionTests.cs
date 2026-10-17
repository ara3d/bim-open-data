using System.Security.Cryptography;
using System.IO.Compression;
using Ara3D.Ifc.DuckDb;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.DuckDb.Tests;

/// <summary>Ties <see cref="IfcDuckDbBuild.Version"/> to what the converter writes. Prepared
/// samples (the Duplex .bos and .duckdb files, git-ignored) are rebuilt only when their stamp names
/// a different version, so a converter change without a bump leaves stale files on every machine
/// that built them earlier and the golden tests there fail with a hash nobody can explain.</summary>
[TestFixture]
[Category("RequiresData")]
public sealed class IfcDuckDbVersionTests
{
    /// <summary>The fingerprint of the BOS file for samples/nrc/duplex-enriched.ifc under each
    /// version. Add a line when the version changes. When the sample file changes and the converter does not, replace the line (done for the NRC_ rename of the property sets).</summary>
    private static readonly Dictionary<string, string> Fingerprints = new()
    {
        ["ifc-duckdb/2"] = "72e2941267f6df54102a3b780bebbd39cd18a937d1743d4b849d3fb835060175",
    };

    [Test]
    public void ConverterOutput_MatchesThePinnedFingerprint_ForItsVersion()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ara3d-ifc-duckdb-version", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var bos = Path.Combine(folder, "duplex-enriched.bos");
            IfcDuckDbBuild.SaveBos(new FilePath(IfcDuckDbBuildTests.SamplePath("duplex-enriched.ifc")), new FilePath(bos));

            Assert.That(Fingerprints.TryGetValue(IfcDuckDbBuild.Version, out var pinned), Is.True,
                $"No fingerprint pinned for {IfcDuckDbBuild.Version}; the converter's output is {Fingerprint(bos)}.");
            Assert.That(Fingerprint(bos), Is.EqualTo(pinned),
                "The converter's output changed. Bump IfcDuckDbBuild.Version and pin the new fingerprint, "
                + "so prepared samples built by the old converter are rebuilt.");
        }
        finally
        {
            BosDuckDbTests.TryDelete(folder);
        }
    }

    /// <summary>SHA-256 over each archive entry's name and bytes in name order. The zip's own
    /// timestamps are left out because they differ between builds of identical content.</summary>
    internal static string Fingerprint(string bosPath)
    {
        using var zip = ZipFile.OpenRead(bosPath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(entry.FullName + "\n"));
            using var stream = entry.Open();
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            hash.AppendData(buffer.ToArray());
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
