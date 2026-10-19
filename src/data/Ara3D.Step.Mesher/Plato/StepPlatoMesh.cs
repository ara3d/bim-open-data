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
}

/// <summary>Library code that threw on one edge or face (<see cref="Kind"/> "edge" or "face", <see cref="Index"/> its
/// kernel row), with the exception's type and message. <see cref="IsPending"/> marks a NotImplementedException: a
/// function the library declares and has not written yet.</summary>
public readonly record struct StepPlatoFailure(string Kind, int Index, string Exception, string Message)
{
    public bool IsPending => Exception == nameof(NotImplementedException);
}
