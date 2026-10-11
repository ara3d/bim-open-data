using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Keeps recently used models open across tool calls, because loading an IFC file is a
/// whole-file parse and an agent asks many small questions of one model. Bounded, evicting the
/// least recently used, since each open model holds its whole file in memory. It owns the
/// <see cref="BosSessionCache"/> that holds the sessions' BOS conversions and every other BOS model
/// the tools open, so one server has one place a .bos path is looked up.</summary>
public sealed class IfcSessionCache : IDisposable
{
    public const int DefaultCapacity = 3;

    private readonly SessionCache<IfcSession> _sessions;

    public IfcSessionCache(int capacity = DefaultCapacity, int bosCapacity = BosSessionCache.DefaultCapacity)
    {
        _sessions = new SessionCache<IfcSession>(capacity);
        BosSessions = new BosSessionCache(bosCapacity);
    }

    public BosSessionCache BosSessions { get; }

    /// <summary>Returns the open session for a file, loading it if needed. Tools call this rather
    /// than requiring an explicit open, so any tool works as the first call against a model.
    /// A .bos or .frag path fails with a message naming the tools that read it.</summary>
    public IfcSession Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An IFC file path is required.", nameof(path));

        var full = new FilePath(path);
        RejectNonIfc(full);
        if (!File.Exists(full.FullPath))
            throw new FileNotFoundException($"IFC file not found: {full.FullPath}");

        return _sessions.Get(full, file => new IfcSession(file, BosSessions));
    }

    /// <summary>The BOS session the query tools read for <paramref name="path"/>: the model a .bos
    /// file holds, or an IFC file's conversion, converting it on first use.</summary>
    public BosSession GetBos(string path)
        => IsBos(path) ? BosSessions.Get(path) : Get(path).Bos;

    public static bool IsBos(string path)
        => Path.GetExtension(path).Equals(".bos", StringComparison.OrdinalIgnoreCase);

    private static void RejectNonIfc(FilePath path)
    {
        var extension = Path.GetExtension(path.FullPath).ToLowerInvariant();
        if (extension == ".bos")
            throw new ArgumentException(
                $"{path.FullPath} is a BIM Open Schema file, and this tool reads IFC files only. On a .bos use "
                + "ifc_table, ifc_sql, ifc_sql_export, ifc_export_glb, or the bos_* tools.");
        if (extension == ".frag")
            throw new ArgumentException(
                $"{path.FullPath} is a That Open Fragments file, and this tool reads IFC files only. "
                + "Convert it with frag_to_bos, then pass the bosPath it returns to ifc_sql or the bos_* tools.");
    }

    public bool IsOpen(string path)
        => _sessions.IsOpen(new FilePath(path));

    public bool Close(string path)
        => _sessions.Close(new FilePath(path));

    public int CloseAll()
        => _sessions.CloseAll();

    public IReadOnlyList<IfcSession> OpenSessions()
        => _sessions.OpenSessions();

    /// <summary>Closes the IFC sessions, then every BOS session.</summary>
    public void Dispose()
    {
        CloseAll();
        BosSessions.Dispose();
    }
}
