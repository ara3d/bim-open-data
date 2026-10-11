using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Open sessions keyed by file path, bounded, evicting the least recently used: the
/// part of <see cref="IfcSessionCache"/> that does not depend on the kind of session. The cache owns
/// its sessions and disposes one when it is closed, evicted, or replaced. A session is opened
/// under the cache's lock, so two calls for one file never open it twice.</summary>
internal sealed class SessionCache<TSession> : IDisposable
    where TSession : class, IDisposable
{
    private readonly Dictionary<FilePath, TSession> _sessions = [];
    private readonly List<FilePath> _order = [];
    private readonly object _lock = new();
    private readonly int _capacity;

    public SessionCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>The open session for <paramref name="path"/>, or a new one from
    /// <paramref name="open"/>. A held session that <paramref name="isCurrent"/> rejects is
    /// disposed and opened again.</summary>
    public TSession Get(FilePath path, Func<FilePath, TSession> open, Func<TSession, bool>? isCurrent = null)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(path, out var existing))
            {
                if (isCurrent == null || isCurrent(existing))
                {
                    Touch(path);
                    return existing;
                }
                Remove(path);
            }

            return Add(path, open(path));
        }
    }

    /// <summary>Holds <paramref name="session"/> under <paramref name="path"/>, disposing any
    /// session held there before.</summary>
    public TSession Put(FilePath path, TSession session)
    {
        lock (_lock)
        {
            Remove(path);
            return Add(path, session);
        }
    }

    public bool IsOpen(FilePath path)
    {
        lock (_lock)
            return _sessions.ContainsKey(path);
    }

    public bool Close(FilePath path)
    {
        lock (_lock)
            return Remove(path);
    }

    public int CloseAll()
    {
        lock (_lock)
        {
            var count = _sessions.Count;
            foreach (var session in _sessions.Values)
                session.Dispose();
            _sessions.Clear();
            _order.Clear();
            return count;
        }
    }

    /// <summary>The open sessions, least recently used first.</summary>
    public IReadOnlyList<TSession> OpenSessions()
    {
        lock (_lock)
            return _order.Select(path => _sessions[path]).ToList();
    }

    private TSession Add(FilePath path, TSession session)
    {
        _sessions[path] = session;
        Touch(path);
        while (_order.Count > _capacity)
            Remove(_order[0]);
        return session;
    }

    private bool Remove(FilePath path)
    {
        if (!_sessions.Remove(path, out var session))
            return false;
        _order.Remove(path);
        session.Dispose();
        return true;
    }

    private void Touch(FilePath path)
    {
        _order.Remove(path);
        _order.Add(path);
    }

    public void Dispose()
        => CloseAll();
}
