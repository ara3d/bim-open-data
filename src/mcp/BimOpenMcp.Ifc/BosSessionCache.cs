using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Keeps recently used BOS models open across tool calls, keyed by .bos path. Bounded,
/// evicting the least recently used, since each session holds a whole model. A session whose
/// file has changed on disk is opened again. An IFC session's conversion and a .frag read by
/// frag_to_bos land here too, so a bosPath either one returns is already open.</summary>
public sealed class BosSessionCache : IDisposable
{
    public const int DefaultCapacity = 3;

    private readonly SessionCache<BosSession> _sessions;

    public BosSessionCache(int capacity = DefaultCapacity)
        => _sessions = new SessionCache<BosSession>(capacity);

    /// <summary>Returns the open session for a .bos file, reading it if it is not open or has
    /// changed since it was read.</summary>
    public BosSession Get(string bosPath)
    {
        if (string.IsNullOrWhiteSpace(bosPath))
            throw new ArgumentException("A .bos file path is required.", nameof(bosPath));

        var full = new FilePath(bosPath);
        if (!File.Exists(full.FullPath))
            throw new FileNotFoundException($"BOS file not found: {full.FullPath}");

        return _sessions.Get(full, BosSession.Open, session => session.IsCurrent);
    }

    /// <summary>Holds a session built by a caller that has the model in memory already,
    /// replacing any session for the same file.</summary>
    public BosSession Put(BosSession session)
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
