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
/// The database is built from the archive, in a private temp folder deleted on dispose.
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
    private readonly Lazy<BosDatabase> _database;

    private BosSession(FilePath bosPath, Lazy<IBimData> data)
    {
        BosPath = bosPath;
        _data = data;
        _scene = new Lazy<BosScene>(() => new BosScene(Data));
        _stamp = Stamp(bosPath);
        _database = new Lazy<BosDatabase>(() => BosDatabase.Build(bosPath));
        OpenedUtc = DateTime.UtcNow;
    }

    /// <summary>A session over <paramref name="data"/>, which must be what <paramref name="bosPath"/>
    /// holds: a caller that has just written the file passes the data it wrote, so it is not read
    /// back.</summary>
    public BosSession(FilePath bosPath, IBimData data)
        : this(bosPath, new Lazy<IBimData>(data))
    {
    }

    /// <summary>A session over the archive at <paramref name="bosPath"/>, whose tables are read,
    /// all of them, on first use of <see cref="Data"/> or <see cref="Scene"/>.</summary>
    public static BosSession Open(FilePath bosPath)
        => new(bosPath, new Lazy<IBimData>(() => ParquetUtils.ReadBimDataFromParquetZip(bosPath)));

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
    /// (<see cref="IfcDuck.CreateViews"/>) and MetricCatalog. Built on first use.</summary>
    public FilePath DatabasePath
        => _database.Value.Path;

    /// <summary>The metric dictionary MetricCatalog was read from, or null when it is empty.
    /// Builds the database if it is not built yet.</summary>
    public FilePath? MetricDictionary
        => _database.Value.MetricDictionary;

    /// <summary>False once the archive on disk has changed size or write time, or is gone: the
    /// session no longer describes the file.</summary>
    public bool IsCurrent
        => File.Exists(BosPath.FullPath) && Stamp(BosPath) == _stamp;

    private static (long, DateTime) Stamp(FilePath path)
    {
        var info = new FileInfo(path.FullPath);
        return (info.Length, info.LastWriteTimeUtc);
    }

    public void Dispose()
    {
        if (_database.IsValueCreated)
            _database.Value.Dispose();
    }

    private sealed record BosDatabase(string Folder, FilePath Path, FilePath? MetricDictionary) : IDisposable
    {
        public static BosDatabase Build(FilePath bos)
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bimopenmcp-bos", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var database = new FilePath(System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(bos.FullPath) + ".duckdb"));
            bos.BosToDuckDB(database);
            IfcDuck.CreateViews(database);
            return new BosDatabase(folder, database, IfcMetricCatalog.Create(database, bos));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
