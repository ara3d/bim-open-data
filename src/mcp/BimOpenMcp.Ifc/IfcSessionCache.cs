using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Keeps recently used models open across tool calls, because loading an IFC file is a
/// whole-file parse and an agent asks many small questions of one model. Bounded, evicting the
/// least recently used, since each open model holds its whole file in memory.</summary>
public sealed class IfcSessionCache : IDisposable
{
    public const int DefaultCapacity = 3;

    private readonly SessionCache<IfcSession> _sessions;

    public IfcSessionCache(int capacity = DefaultCapacity)
        => _sessions = new SessionCache<IfcSession>(capacity);

    /// <summary>Returns the open session for a file, loading it if needed. Tools call this rather
    /// than requiring an explicit open, so any tool works as the first call against a model.</summary>
    public IfcSession Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An IFC file path is required.", nameof(path));

        var full = new FilePath(path);
        if (!File.Exists(full.FullPath))
            throw new FileNotFoundException($"IFC file not found: {full.FullPath}");

        return _sessions.Get(full, file => new IfcSession(file));
    }

    public bool IsOpen(string path)
        => _sessions.IsOpen(new FilePath(path));

    public bool Close(string path)
        => _sessions.Close(new FilePath(path));

    public int CloseAll()
        => _sessions.CloseAll();

    public IReadOnlyList<IfcSession> OpenSessions()
        => _sessions.OpenSessions();

    public void Dispose()
        => CloseAll();
}
