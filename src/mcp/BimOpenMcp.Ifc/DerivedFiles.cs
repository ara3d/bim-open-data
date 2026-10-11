using System.Security.Cryptography;
using System.Text;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Ifc.Conventions;
using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>A file made from another one and every input it was made from: what
/// <see cref="DerivedFiles.GetOrBuild"/> returns.</summary>
public sealed record DerivedFile(FilePath Path, IReadOnlyList<FilePath> ExtraInputs);

/// <summary>The files the server makes from a model (an IFC file's BOS conversion, a .bos file's
/// DuckDB database), kept under one temp root in folders named by a content key: the kind of file,
/// the source's path, size and write time, and the build of the code that makes it. A server that
/// restarts finds the folder and reuses it instead of converting again, and two servers on one
/// machine share it.
/// <para>
/// A folder is built under a temporary name and moved into place whole, with a marker naming every
/// file it was made from and that file's size and write time, so a half-built folder is never
/// taken for a whole one. A folder whose inputs have changed or gone, or that no server has used for
/// <see cref="DefaultUnusedFor"/>, is stale: <see cref="Sweep"/> deletes it, and the server runs
/// a sweep at start. Nothing is deleted when a session closes, since another server may be reading
/// the same folder.
/// </para>
/// <para>
/// An input a build looked for and did not find is not recorded, so a metric dictionary CSV that
/// appears after the database was built is not seen until the .bos file changes.
/// </para>
/// </summary>
public sealed class DerivedFiles
{
    private const string Marker = "inputs.txt";
    private const string PartialSuffix = ".partial";

    /// <summary>How long a folder no server has used is kept.</summary>
    public static readonly TimeSpan DefaultUnusedFor = TimeSpan.FromDays(7);

    /// <summary>How old a half-built folder must be before a sweep takes it for an abandoned one
    /// rather than another server's build in progress.</summary>
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(1);

    /// <summary>The store under the system temp folder that the server uses.</summary>
    public static DerivedFiles Default { get; } = new(Path.Combine(Path.GetTempPath(), "bimopenmcp"));

    /// <summary>Identifies the code that makes the files: a new build of a converter or of the
    /// DuckDB views gets new folders, so a fixed bug is never served from an old folder.</summary>
    private static readonly string BuildId = string.Join(
        ",",
        new[]
        {
            typeof(DerivedFiles), typeof(IfcToBosConverter), typeof(IfcClasses), typeof(BimGeometryBuilder),
            typeof(ParquetUtils), typeof(BosDuckDbViews),
        }.Select(type => type.Assembly.ManifestModule.ModuleVersionId.ToString("N")));

    public DerivedFiles(string root)
        => Root = Path.GetFullPath(root);

    public string Root { get; }

    /// <summary>The file named <paramref name="fileName"/> made from <paramref name="source"/>,
    /// built by <paramref name="build"/> when no current copy exists. <paramref name="build"/>
    /// writes <paramref name="fileName"/> into the folder it is given and returns any other files
    /// it read, so a change to one of them makes the copy stale too. <paramref name="rebuild"/>
    /// builds even when a current copy exists, and replaces it.</summary>
    public DerivedFile GetOrBuild(
        string kind,
        FilePath source,
        string fileName,
        Func<string, IReadOnlyList<FilePath>> build,
        bool rebuild = false)
    {
        var folder = Path.Combine(Root, kind, Key(kind, source));
        if (!rebuild && TryReuse(folder, fileName) is { } reused)
            return reused;
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);

        var partial = folder + "." + Guid.NewGuid().ToString("N")[..8] + PartialSuffix;
        Directory.CreateDirectory(partial);
        try
        {
            var extra = build(partial);
            File.WriteAllLines(Path.Combine(partial, Marker), new[] { source }.Concat(extra).Select(Describe));
            Directory.Move(partial, folder);
        }
        catch (IOException) when (TryReuse(folder, fileName) != null)
        {
            // Another server finished the same folder first; use its copy.
        }
        finally
        {
            if (Directory.Exists(partial))
                Directory.Delete(partial, recursive: true);
        }

        return TryReuse(folder, fileName)
               ?? throw new IOException($"Built {fileName} from {source.FullPath}, but {folder} is not current.");
    }

    /// <summary>Deletes every stale or abandoned folder, skipping one another process holds open,
    /// and returns how many it deleted.</summary>
    public int Sweep(TimeSpan unusedFor)
    {
        if (!Directory.Exists(Root))
            return 0;

        var deleted = 0;
        var now = DateTime.UtcNow;
        foreach (var folder in Directory.EnumerateDirectories(Root).SelectMany(Directory.EnumerateDirectories))
        {
            var age = now - Directory.GetLastWriteTimeUtc(folder);
            var stale = folder.EndsWith(PartialSuffix, StringComparison.Ordinal) || !File.Exists(Path.Combine(folder, Marker))
                ? age > AbandonedAfter
                : age > unusedFor || !InputsAreCurrent(folder);
            if (!stale)
                continue;
            try
            {
                Directory.Delete(folder, recursive: true);
                deleted++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // In use by another server; the next sweep tries again.
            }
        }

        return deleted;
    }

    /// <summary>The derived file in <paramref name="folder"/> when the folder is whole and every
    /// input it records is unchanged; marks the folder used.</summary>
    private static DerivedFile? TryReuse(string folder, string fileName)
    {
        var file = Path.Combine(folder, fileName);
        if (!File.Exists(file) || !InputsAreCurrent(folder))
            return null;
        try
        {
            Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only the sweep reads the time, and the next use sets it again.
        }

        var extra = File.ReadAllLines(Path.Combine(folder, Marker)).Skip(1).Select(line => new FilePath(line.Split('|', 3)[2]));
        return new DerivedFile(new FilePath(file), extra.ToList());
    }

    private static bool InputsAreCurrent(string folder)
    {
        var marker = Path.Combine(folder, Marker);
        return File.Exists(marker) && File.ReadAllLines(marker) is { Length: > 0 } lines
            && lines.All(line => line == Describe(new FilePath(line.Split('|', 3)[2])));
    }

    /// <summary>A marker line: size, write time and path, or a size of -1 for a file that is gone.</summary>
    private static string Describe(FilePath file)
    {
        var info = new FileInfo(file.FullPath);
        return info.Exists
            ? $"{info.Length}|{info.LastWriteTimeUtc.Ticks}|{info.FullName}"
            : $"-1|0|{info.FullName}";
    }

    private static string Key(string kind, FilePath source)
    {
        var text = $"{kind}\n{BuildId}\n{Describe(source).ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
    }
}
