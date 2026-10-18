using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>The rows of one kernel table that one occurrence reaches, keyed by instance id. Reading an instance
/// either yields a builder, run once every table's indices are known, or fails, and the failure is remembered so
/// each instance is read once. A row whose reading meets a row being read (a reference cycle) fails.</summary>
public sealed class PendingRows<TRow>
{
    readonly Dictionary<int, Func<TRow>?> _rows = new();
    readonly HashSet<int> _open = [];
    readonly Dictionary<int, int> _index = new();
    List<int>? _ok;

    /// <summary>Reads instance <paramref name="id"/> once; false when it (or a row it needs) cannot be decoded, in
    /// which case <paramref name="failed"/> has been told why.</summary>
    public bool Reach(int id, Func<Func<TRow>> read, Action<int, Exception> failed)
    {
        if (_rows.TryGetValue(id, out var known))
            return known is not null;
        if (!_open.Add(id))
            return false;
        Func<TRow>? built = null;
        try
        {
            built = read();
        }
        catch (Exception ex) when (DecodeErrors.IsExpected(ex))
        {
            failed(id, ex);
        }
        finally
        {
            _open.Remove(id);
        }
        _rows[id] = built;
        return built is not null;
    }

    /// <summary>The kernel index of a row read successfully; valid after <see cref="Assign"/>.</summary>
    public int IndexOf(int id)
        => _index[id];

    /// <summary>Instance ids reached, decoded or not.</summary>
    public IEnumerable<int> Reached => _rows.Keys;

    public IEnumerable<int> Failed => _rows.Where(r => r.Value is null).Select(r => r.Key);

    /// <summary>Gives the decoded rows their kernel indices, ascending by instance id after the table's
    /// <paramref name="firstIndex"/> rows.</summary>
    public void Assign(int firstIndex)
    {
        _ok = _rows.Where(r => r.Value is not null).Select(r => r.Key).Order().ToList();
        for (var k = 0; k < _ok.Count; k++)
            _index[_ok[k]] = firstIndex + k;
    }

    /// <summary>Builds the decoded rows in index order and appends them, with their sources, to the table.</summary>
    public void Commit(KernelTable<TRow> table, int occurrence)
    {
        foreach (var id in _ok ?? throw new InvalidOperationException("Assign before Commit"))
        {
            table.Rows.Add(_rows[id]!());
            table.Sources.Add(new StepSource(new StepOccurrenceIndex(occurrence), new StepInstanceId(id)));
        }
    }
}

/// <summary>One kernel table of the whole model and the source of each row.</summary>
public sealed class KernelTable<TRow>
{
    public List<TRow> Rows { get; } = [];
    public List<StepSource> Sources { get; } = [];
}

/// <summary>The failures a row's reader turns into findings: the typed layer's wrong-kind and dangling-reference
/// errors, a required attribute left unset, and the decoder's own <see cref="DecodeException"/>.</summary>
public static class DecodeErrors
{
    public static bool IsExpected(Exception ex)
        => ex is DecodeException or InvalidDataException or KeyNotFoundException or InvalidOperationException
            or IndexOutOfRangeException or ArgumentOutOfRangeException or FormatException;

    /// <summary>The finding for a failure: the decoder's code, a dangling reference, an attribute of the wrong
    /// kind, or a skipped instance.</summary>
    public static (StepDiagnosticCode Code, int Id) FindingOf(Exception ex, int id)
        => ex switch
        {
            DecodeException d => (d.Code, d.InstanceId),
            KeyNotFoundException => (StepDiagnosticCode.DanglingReference(), id),
            InvalidDataException => (StepDiagnosticCode.AttributeKindMismatch(), id),
            _ => (StepDiagnosticCode.InstanceSkipped(), id),
        };
}
