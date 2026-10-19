using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>A STEP file meshed by the Plato library: the decoder's raw model (its occurrences and the source of every
/// kernel row), the prepared model, one face outcome per face of the prepared model, the assembled mesh in the
/// model's millimetres with every occurrence placed, and the host's findings where the library's code threw.</summary>
public sealed record StepPlatoMesh(
    RawDecodedModel Raw,
    DecodedModel Prepared,
    IReadOnlyList<FaceMeshOutcome> Outcomes,
    ModelMesh Mesh,
    IReadOnlyList<StepPlatoFailure> Failures)
{
    public BrepModel Model => Prepared.Model;

    /// <summary>Each B-rep item's body at its first occurrence, by the item's STEP instance id: the mesh a placed item
    /// shares with its other placements.</summary>
    public IReadOnlyDictionary<int, BodyIndex> BodyOfInstance { get; } = FirstBodies(Raw.Sources.Bodies);

    readonly HashSet<int> _pendingFaces = PendingFaces(Prepared.Model, Failures);

    /// <summary>True when face <paramref name="face"/> has no mesh because library code it reached, on the face or
    /// on one of its edges, is declared without a body (NotImplementedException).</summary>
    public bool IsPending(BrepFaceIndex face)
        => _pendingFaces.Contains(face.Value);

    /// <summary>The STEP instance id of face <paramref name="face"/>'s surface.</summary>
    public int SurfaceInstance(BrepFaceIndex face)
        => Raw.Sources.Surfaces[Model.Faces[face.Value].Surface.Value].Instance.Value;

    /// <summary>The faces of body <paramref name="body"/>, every shell's, in shell order.</summary>
    public IReadOnlyList<BrepFaceIndex> FacesOf(BodyIndex body)
        => Model.BodyShells(body).SelectMany(s => Model.ShellFaces(s)).ToArray();

    /// <summary>True when face <paramref name="face"/> has at least one element.</summary>
    public bool IsMeshed(BrepFaceIndex face)
        => Outcomes[face.Value].Mesh.Triangles.Count + Outcomes[face.Value].Mesh.Quads.Count > 0;

    /// <summary>The STEP instance id a row of the decoded body table came from.</summary>
    public int BodyInstance(BodyIndex body)
        => Raw.Sources.Bodies[body.Value].Instance.Value;

    /// <summary>The occurrence that placed body <paramref name="body"/>.</summary>
    public StepOccurrence OccurrenceOf(BodyIndex body)
        => Raw.Occurrences[Raw.Sources.Bodies[body.Value].Occurrence.Value];

    /// <summary>The STEP instance id of face <paramref name="face"/>.</summary>
    public int FaceInstance(BrepFaceIndex face)
        => Raw.Sources.Faces[face.Value].Instance.Value;

    static Dictionary<int, BodyIndex> FirstBodies(IReadOnlyList<StepSource> bodies)
    {
        var first = new Dictionary<int, BodyIndex>();
        for (var b = 0; b < bodies.Count; b++)
            first.TryAdd(bodies[b].Instance.Value, new BodyIndex(b));
        return first;
    }

    static HashSet<int> PendingFaces(BrepModel m, IReadOnlyList<StepPlatoFailure> failures)
    {
        var faces = failures.Where(f => f.IsPending && f.Kind == StepPlatoFailure.Face).Select(f => f.Index).ToHashSet();
        var edges = failures.Where(f => f.IsPending && f.Kind == StepPlatoFailure.Edge).Select(f => f.Index).ToHashSet();
        if (edges.Count > 0)
            for (var f = 0; f < m.Faces.Count; f++)
                if (Enumerable.Any(m.FaceEdges(new BrepFaceIndex(f)), e => edges.Contains(e.Value)))
                    faces.Add(f);
        return faces;
    }
}

/// <summary>Library code that threw on one edge or face (<see cref="Kind"/> "edge" or "face", <see cref="Index"/> its
/// kernel row), with the exception's type and message. <see cref="IsPending"/> marks a NotImplementedException: a
/// function the library declares and has not written yet.</summary>
public readonly record struct StepPlatoFailure(string Kind, int Index, string Exception, string Message)
{
    public const string Edge = "edge";
    public const string Face = "face";

    public bool IsPending => Exception == nameof(NotImplementedException);
}
