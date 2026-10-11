namespace Ara3D.Ifc.Mesher.Approach1;

public enum GeometrySupportStatus
{
    Supported,
    Approximate,
    Unsupported,
}

public sealed class MeshingDiagnostics
{
    readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    readonly Dictionary<string, GeometrySupportStatus> _status = new(StringComparer.Ordinal);
    readonly List<string> _messages = [];

    public IReadOnlyDictionary<string, int> EntityCounts => _counts;
    public IReadOnlyDictionary<string, GeometrySupportStatus> EntityStatus => _status;
    readonly Dictionary<int, DroppedFaces> _dropped = [];

    /// <summary>Plain messages, then one line per product that lost faces (see <see cref="RecordDroppedFace"/>).</summary>
    public IReadOnlyList<string> Messages
        => _dropped.Count == 0
            ? _messages
            : _messages.Concat(_dropped.OrderBy(kv => kv.Key).Select(kv => kv.Value.Describe(kv.Key))).ToList();

    /// <summary>Faces left out of the mesh, by the express id of the product that owns them
    /// (<see cref="DroppedFaces.NoProduct"/> when meshed outside a product).</summary>
    public IReadOnlyDictionary<int, int> DroppedFaceCounts
        => _dropped.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

    public int TotalDroppedFaces => _dropped.Values.Sum(d => d.Count);

    /// <summary>
    /// Records a face the mesher left out. They are aggregated per product so a large model yields
    /// one line per affected product: the count, the count per reason, and the first few faces.
    /// </summary>
    public void RecordDroppedFace(int? productId, int faceId, string reason)
    {
        var key = productId ?? DroppedFaces.NoProduct;
        if (!_dropped.TryGetValue(key, out var entry))
            _dropped[key] = entry = new DroppedFaces();
        entry.Add(faceId, reason);
        if (!_status.TryGetValue("IFCFACE", out var existing) || GeometrySupportStatus.Approximate < existing)
            _status["IFCFACE"] = GeometrySupportStatus.Approximate;
    }

    public void Record(string entityName, GeometrySupportStatus status, string? message = null)
    {
        _counts[entityName] = _counts.GetValueOrDefault(entityName) + 1;
        if (!_status.TryGetValue(entityName, out var existing) || status < existing)
            _status[entityName] = status;
        if (!string.IsNullOrEmpty(message))
            _messages.Add($"{entityName}: {message}");
    }

    public void RecordUnsupported(string entityName, string? reason = null)
        => Record(entityName, GeometrySupportStatus.Unsupported, reason);

    public void RecordApproximate(string entityName, string? reason = null)
        => Record(entityName, GeometrySupportStatus.Approximate, reason);

    public void RecordSupported(string entityName)
        => Record(entityName, GeometrySupportStatus.Supported);
}

/// <summary>The faces one product lost: a count, a count per reason, and a few examples.</summary>
public sealed class DroppedFaces
{
    public const int NoProduct = -1;
    const int MaxSamples = 3;

    readonly Dictionary<string, int> _byReason = new(StringComparer.Ordinal);
    readonly List<string> _samples = [];

    public int Count { get; private set; }

    public void Add(int faceId, string reason)
    {
        Count++;
        // Group reasons without their numbers ("area off by 23.4 %" and "area off by 5 %" are one kind).
        var by = reason.IndexOf(" by ", StringComparison.Ordinal);
        var kind = by >= 0 ? reason[..by] : reason;
        _byReason[kind] = _byReason.GetValueOrDefault(kind) + 1;
        if (_samples.Count < MaxSamples)
            _samples.Add($"#{faceId} ({reason})");
    }

    public string Describe(int productId)
    {
        var owner = productId == NoProduct ? "outside any product" : $"product #{productId}";
        var reasons = string.Join(", ", _byReason.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Value} {kv.Key}"));
        var more = Count > _samples.Count ? ", ..." : "";
        return $"IFCFACE: {owner} dropped {Count} face(s) from its mesh: {reasons}; first {string.Join(", ", _samples)}{more}";
    }
}
