using Ara3D.Geometry;
using Ara3D.Step.Entities;

namespace Ara3D.Step.Mesher;

/// <summary>The length unit and uncertainty of one geometric representation context: a file length times
/// <see cref="MetresPerUnit"/> is metres; <see cref="Uncertainty"/> is in file units, or null when the
/// context declares none.</summary>
public readonly record struct StepUnits(double MetresPerUnit, double? Uncertainty);

public enum StepSupport
{
    Supported,
    Approximate,
    Unsupported,
}

/// <summary>A face the mesher left out, and why.</summary>
public readonly record struct StepDroppedFace(int FaceId, string Reason);

/// <summary>Counts of meshed entity names with their worst support status, messages, and dropped faces.
/// Meshing records here and never throws on unsupported or malformed geometry.</summary>
public sealed class StepMeshingDiagnostics
{
    readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    readonly Dictionary<string, StepSupport> _status = new(StringComparer.Ordinal);
    readonly List<string> _messages = [];
    readonly List<StepDroppedFace> _dropped = [];

    public IReadOnlyDictionary<string, int> EntityCounts => _counts;
    public IReadOnlyDictionary<string, StepSupport> EntityStatus => _status;
    public IReadOnlyList<string> Messages => _messages;
    public IReadOnlyList<StepDroppedFace> DroppedFaces => _dropped;

    public void Record(string entityName, StepSupport status, string? message = null)
    {
        _counts[entityName] = _counts.GetValueOrDefault(entityName) + 1;
        if (!_status.TryGetValue(entityName, out var existing) || existing < status)
            _status[entityName] = status;
        if (!string.IsNullOrEmpty(message))
            _messages.Add($"{entityName}: {message}");
    }

    public void RecordDroppedFace(int faceId, string reason)
        => _dropped.Add(new(faceId, reason));
}

/// <summary>One shape item meshed in its own coordinate system, lengths in metres. <see cref="FacesPending"/> counts the
/// dropped faces whose library code is declared without a body yet; an item all of whose faces are pending counts as
/// geometry pending.</summary>
public sealed record StepShapeMesh(TriangleMesh3D Mesh, int FacesMeshed, int FacesDropped, int FacesPending)
{
    public bool IsPending
        => FacesPending > 0 && FacesPending == FacesMeshed + FacesDropped;
}
