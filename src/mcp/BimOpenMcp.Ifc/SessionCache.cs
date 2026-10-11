using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>A hold on an open session: the session is not disposed while a lease on it is
/// undisposed, even when its cache closes, evicts, or replaces it. A tool call takes one for the
/// length of the call, so over HTTP a call on another model cannot dispose a session mid-answer.</summary>
public sealed class SessionLease<TSession> : IDisposable
    where TSession : class, IDisposable
{
    private Action? _release;

    internal SessionLease(TSession session, Action release)
    {
        Session = session;
        _release = release;
    }

    public TSession Session { get; }

    public void Dispose()
        => Interlocked.Exchange(ref _release, null)?.Invoke();
}

/// <summary>Open sessions keyed by file path, bounded, evicting the least recently used: the
/// shared part of <see cref="IfcSessionCache"/> and <see cref="BosSessionCache"/>. The cache owns
/// its sessions and disposes one when it is closed, evicted, or replaced, but not while a
/// <see cref="SessionLease{TSession}"/> holds it: a held session is never evicted, so the cache can
/// hold more than its capacity while calls are running, and one closed or replaced while held is
/// disposed when its last lease ends. A session is opened under the cache's lock, so two calls for
/// one file never open it twice.</summary>
internal sealed class SessionCache<TSession> : IDisposable
    where TSession : class, IDisposable
{
    private readonly Dictionary<FilePath, Entry> _sessions = [];
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
    /// <paramref name="open"/>, without a lease: for a caller that runs one call at a time. A held
    /// session that <paramref name="isCurrent"/> rejects is closed and opened again.</summary>
    public TSession Get(FilePath path, Func<FilePath, TSession> open, Func<TSession, bool>? isCurrent = null)
    {
        lock (_lock)
        {
            var entry = Find(path, open, isCurrent);
            Trim();
            return entry.Session;
        }
    }

    /// <summary>As <see cref="Get"/>, with a lease that keeps the session open until it is
    /// disposed.</summary>
    public SessionLease<TSession> Lease(FilePath path, Func<FilePath, TSession> open, Func<TSession, bool>? isCurrent = null)
    {
        lock (_lock)
        {
            var entry = Find(path, open, isCurrent);
            entry.Leases++;
            Trim();
            return new SessionLease<TSession>(entry.Session, () => Release(entry));
        }
    }

    /// <summary>Holds <paramref name="session"/> under <paramref name="path"/>, closing any
    /// session held there before, and returns a lease on it.</summary>
    public SessionLease<TSession> Put(FilePath path, TSession session)
    {
        lock (_lock)
        {
            Remove(path);
            var entry = Add(path, session);
            entry.Leases++;
            Trim();
            return new SessionLease<TSession>(session, () => Release(entry));
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
            foreach (var path in _order.ToList())
                Remove(path);
            return count;
        }
    }

    /// <summary>The open sessions, least recently used first.</summary>
    public IReadOnlyList<TSession> OpenSessions()
    {
        lock (_lock)
            return _order.Select(path => _sessions[path].Session).ToList();
    }

    private Entry Find(FilePath path, Func<FilePath, TSession> open, Func<TSession, bool>? isCurrent)
    {
        if (_sessions.TryGetValue(path, out var existing))
        {
            if (isCurrent == null || isCurrent(existing.Session))
            {
                Touch(path);
                return existing;
            }
            Remove(path);
        }

        return Add(path, open(path));
    }

    private Entry Add(FilePath path, TSession session)
    {
        var entry = new Entry(session);
        _sessions[path] = entry;
        Touch(path);
        return entry;
    }

    /// <summary>Closes the least recently used sessions no lease holds until the cache is within
    /// its capacity, or every session left is held. The most recently used, the one a caller has
    /// just been handed, always stays.</summary>
    private void Trim()
    {
        for (var i = 0; _order.Count > _capacity && i < _order.Count - 1;)
        {
            if (_sessions[_order[i]].Leases > 0)
                i++;
            else
                Remove(_order[i]);
        }
    }

    private bool Remove(FilePath path)
    {
        if (!_sessions.Remove(path, out var entry))
            return false;
        _order.Remove(path);
        entry.Closed = true;
        if (entry.Leases == 0)
            entry.Session.Dispose();
        return true;
    }

    private void Release(Entry entry)
    {
        lock (_lock)
        {
            entry.Leases--;
            if (entry.Leases > 0)
                return;
            if (entry.Closed)
                entry.Session.Dispose();
            else
                Trim();
        }
    }

    private void Touch(FilePath path)
    {
        _order.Remove(path);
        _order.Add(path);
    }

    public void Dispose()
        => CloseAll();

    private sealed class Entry(TSession session)
    {
        public TSession Session { get; } = session;

        public int Leases { get; set; }

        public bool Closed { get; set; }
    }
}
