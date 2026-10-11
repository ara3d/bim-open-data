using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>One BIM Open Schema model held open across tool calls: its tables, the
/// <see cref="BosScene"/> the GLB, USD and BCF writers share, and a DuckDB database with the text
/// views the SQL tools query. The bos_* tools and the IFC tools that read a conversion all go
/// through one, so a model's archive is read once per session, not once per call.
/// <para>
/// The tables and the database are each built on first use: a session that only answers SQL
/// never reads the tables into memory, and one that only exports never builds the database.
/// The database is built from the archive into <see cref="DerivedFiles"/>, in a folder named by the
/// archive's path, size and write time, so a restarted server reuses it; disposing the session
/// leaves it there for the next one, and a sweep at server start removes it once it is stale.
/// A disposed session builds nothing: asking it for the database throws.
/// </para>
/// <para>
/// Every table is read. The writers between them need all of them, and <see cref="BosScene"/>
/// cannot tell a table that was not read from one that is empty, so a session that read only
/// some would answer "no parameters" where the truth is "not loaded".
/// </para>
/// </summary>
public sealed class BosSession : IDisposable
{
    private readonly (long Length, DateTime WrittenUtc) _stamp;
    private readonly Lazy<IBimData> _data;
    private readonly Lazy<BosScene> _scene;
    private readonly Lazy<DerivedFile> _database;
    private readonly object _disposeLock = new();
    private bool _disposed;

    private BosSession(FilePath bosPath, Lazy<IBimData> data, DerivedFiles files)
    {
        BosPath = bosPath;
        _data = data;
        _scene = new Lazy<BosScene>(() => new BosScene(Data));
        _stamp = Stamp(bosPath);
        _database = new Lazy<DerivedFile>(() => BuildDatabase(files));
        OpenedUtc = DateTime.UtcNow;
    }

    /// <summary>Writes <paramref name="data"/> to <paramref name="bosPath"/>, creating its folder,
    /// and returns a session over the data, so the tables are not read back. The recipe for a tool
    /// that reads another format into BOS (frag_to_bos today): read, then
    /// <see cref="BosSessionCache.Put"/> the session this returns, then
    /// <see cref="CopyDatabaseBeside"/> if the caller wants the database next to the .bos.</summary>
    public static BosSession FromData(IBimData data, FilePath bosPath, DerivedFiles? files = null)
    {
        var directory = System.IO.Path.GetDirectoryName(bosPath.FullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        data.WriteToParquetZip(bosPath);
        return new BosSession(bosPath, new Lazy<IBimData>(data), files ?? DerivedFiles.Default);
    }

    /// <summary>A session over the archive at <paramref name="bosPath"/>, whose tables are read,
    /// all of them, on first use of <see cref="Data"/> or <see cref="Scene"/>.</summary>
    public static BosSession Open(FilePath bosPath, DerivedFiles? files = null)
        => new(bosPath, new Lazy<IBimData>(() => ParquetUtils.ReadBimDataFromParquetZip(bosPath)), files ?? DerivedFiles.Default);

    public FilePath BosPath { get; }

    /// <summary>Every table of the model. Read on first use.</summary>
    public IBimData Data
        => _data.Value;

    /// <summary>The view the writers share, built over <see cref="Data"/> on first use.</summary>
    public BosScene Scene
        => _scene.Value;

    public DateTime OpenedUtc { get; }

    /// <summary>The size of the archive when the session was opened.</summary>
    public long BosBytes
        => _stamp.Length;

    /// <summary>The DuckDB database: the BOS tables without geometry, the text views
    /// (<see cref="IfcDuck.CreateViews"/>) and MetricCatalog. Built on first use, or found where
    /// an earlier session built it. Open it read-only (<see cref="IfcDuck.OpenReadOnly"/>): other
    /// servers may be reading it.</summary>
    public FilePath DatabasePath
        => _database.Value.Path;

    /// <summary>The metric dictionary MetricCatalog was read from, or null when it is empty.
    /// Builds the database if it is not built yet.</summary>
    public FilePath? MetricDictionary
        => _database.Value.ExtraInputs.Count > 0 ? _database.Value.ExtraInputs[0] : default(FilePath?);

    /// <summary>False once the archive on disk has changed size or write time, or is gone: the
    /// session no longer describes the file.</summary>
    public bool IsCurrent
        => File.Exists(BosPath.FullPath) && Stamp(BosPath) == _stamp;

    private static (long, DateTime) Stamp(FilePath path)
    {
        var info = new FileInfo(path.FullPath);
        return (info.Length, info.LastWriteTimeUtc);
    }

    /// <summary>Copies the database (building it if needed) to the .bos path with the extension
    /// .duckdb and returns that path. Copies under a temporary name and moves into place, so a
    /// failed copy never leaves a half-written database where a reader would take it for a whole
    /// one.</summary>
    public FilePath CopyDatabaseBeside()
    {
        var destination = System.IO.Path.ChangeExtension(BosPath.FullPath, ".duckdb");
        var partial = destination + ".partial";
        File.Copy(DatabasePath.FullPath, partial, overwrite: true);
        File.Move(partial, destination, overwrite: true);
        return new FilePath(destination);
    }

    /// <summary>Marks the session closed. Files it built stay in <see cref="DerivedFiles"/>.</summary>
    public void Dispose()
    {
        lock (_disposeLock)
            _disposed = true;
    }

    /// <summary>Builds or finds the database. Holds the dispose lock throughout, so a session
    /// disposed while building finishes first, and one disposed before never starts.</summary>
    private DerivedFile BuildDatabase(DerivedFiles files)
    {
        lock (_disposeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var name = System.IO.Path.GetFileNameWithoutExtension(BosPath.FullPath) + ".duckdb";
            return files.GetOrBuild("duckdb", BosPath, name, folder =>
            {
                var database = new FilePath(System.IO.Path.Combine(folder, name));
                BosPath.BosToDuckDB(database);
                IfcDuck.CreateViews(database);
                return IfcMetricCatalog.Create(database, BosPath) is { } dictionary ? [dictionary] : [];
            });
        }
    }
}
