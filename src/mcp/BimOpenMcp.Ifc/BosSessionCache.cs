using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Keeps recently used BOS models open across tool calls, keyed by .bos path. Bounded,
/// evicting the least recently used, since each session holds a whole model. A session whose
/// file has changed on disk is opened again. An IFC session's conversion and a .frag read by
/// frag_to_bos land here too, so a bosPath either one returns is already open. A tool call holds
/// its session through a <see cref="SessionLease{TSession}"/>, so another call cannot dispose it
/// mid-answer.</summary>
public sealed class BosSessionCache : IDisposable
{
    public const int DefaultCapacity = 3;

    private readonly SessionCache<BosSession> _sessions;

    /// <summary>A cache whose sessions build their databases in <paramref name="files"/>, by
    /// default <see cref="DerivedFiles.Default"/>.</summary>
    public BosSessionCache(int capacity = DefaultCapacity, DerivedFiles? files = null)
    {
        _sessions = new SessionCache<BosSession>(capacity);
        Files = files ?? DerivedFiles.Default;
    }

    /// <summary>Where the sessions keep the files they build: DuckDB databases and, for the IFC
    /// sessions that share this cache, BOS conversions.</summary>
    public DerivedFiles Files { get; }

    /// <summary>Returns the open session for a .bos file, reading it if it is not open or has
    /// changed since it was read. Without a lease: for a caller that runs one call at a time.</summary>
    public BosSession Get(string bosPath)
        => _sessions.Get(Existing(bosPath), Open, session => session.IsCurrent);

    /// <summary>As <see cref="Get"/>, with a lease that keeps the session open until it is
    /// disposed.</summary>
    public SessionLease<BosSession> Lease(string bosPath)
        => _sessions.Lease(Existing(bosPath), Open, session => session.IsCurrent);

    private BosSession Open(FilePath bosPath)
        => BosSession.Open(bosPath, Files);

    private static FilePath Existing(string bosPath)
    {
        if (string.IsNullOrWhiteSpace(bosPath))
            throw new ArgumentException("A .bos file path is required.", nameof(bosPath));

        var full = new FilePath(bosPath);
        return File.Exists(full.FullPath) ? full : throw new FileNotFoundException($"BOS file not found: {full.FullPath}");
    }

    /// <summary>Holds a session built by a caller that has the model in memory already,
    /// replacing any session for the same file, and returns a lease on it.</summary>
    public SessionLease<BosSession> Put(BosSession session)
        => _sessions.Put(session.BosPath, session);

    public bool IsOpen(string bosPath)
        => _sessions.IsOpen(new FilePath(bosPath));

    public bool Close(string bosPath)
        => _sessions.Close(new FilePath(bosPath));

    public int CloseAll()
        => _sessions.CloseAll();

    public IReadOnlyList<BosSession> OpenSessions()
        => _sessions.OpenSessions();

    public void Dispose()
        => CloseAll();
}
