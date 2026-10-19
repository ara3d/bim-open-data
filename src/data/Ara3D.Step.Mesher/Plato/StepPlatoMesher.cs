using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>
/// Meshes a whole STEP file through the Plato STEP library compiled to C# (Ara3D.StepBrep): the decoder's DecodeRaw,
/// the library's PrepareModel, then what layer 8's MeshModel does before its refinement loop: every edge discretized
/// once (layer 4's DiscretizeAll), every face given layer 8's FaceOutcome (layer 5's MeshFace when its edges are
/// discretized), and the face meshes numbered and assembled (layer 6). The committed generation holds layers 00 to 07,
/// so layer 8 is restated here; when it is generated this becomes one call to MeshModel. Library code that throws on
/// one edge or face is caught here and becomes an empty outcome and a <see cref="StepPlatoFailure"/>, so a surface the
/// library cannot mesh yet leaves every other face meshed. Decoding is per file because DecodeRaw and PrepareModel
/// are: the tolerances come from the whole model's size, and mirrored occurrences are folded in place.
/// </summary>
public static class StepPlatoMesher
{
    /// <summary>Layer 8's DefaultMeshingOptions surface angle: the normal turns at most 15 degrees along an element
    /// edge.</summary>
    public const double SurfaceAngleDegrees = 15;

    public static StepPlatoMesh MeshFile(this StepModel model, double chordalMillimetres)
    {
        var raw = model.DecodeRaw();
        var prepared = raw.PrepareModel();
        var m = prepared.Model;
        var criteria = Criteria(chordalMillimetres);
        var failures = new List<StepPlatoFailure>();
        var d = new EdgeDiscretizations(Rows(m.Edges.Count, i => m.Discretized(new BrepEdgeIndex(i), criteria.Discretization),
            (i, _) => new BrepEdgeIndex(i).Undiscretized(), "edge", failures));
        var outcomes = Rows(m.Faces.Count, i => m.FaceOutcome(new BrepFaceIndex(i), d, criteria),
            (i, failure) => Unmeshed(new BrepFaceIndex(i), failure), "face", failures);
        var faceMeshes = outcomes.Select(o => o.Mesh).ToArray();
        var mesh = m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(d, faceMeshes));
        return new(raw, prepared, outcomes, mesh, failures);
    }

    /// <summary>Layer 8's DefaultMeshingOptions criteria for an absolute chordal bound in model units.</summary>
    public static FaceMeshingCriteria Criteria(double chordal)
        => new(((Number)chordal).DefaultCriteria(), chordal, ((Number)SurfaceAngleDegrees).Degrees(), false);

    /// <summary>Layer 4's DiscretizeAll entry for one edge.</summary>
    static EdgeDiscretization Discretized(this BrepModel m, BrepEdgeIndex e, DiscretizationCriteria c)
        => m.CanDiscretize(e, c) ? m.Discretize(e, c, SegmentFloors.NoFloors()) : e.Undiscretized();

    /// <summary>Layer 8's FaceOutcome with every face chosen.</summary>
    static FaceMeshOutcome FaceOutcome(this BrepModel m, BrepFaceIndex f, EdgeDiscretizations d, FaceMeshingCriteria c)
        => m.IsDiscretizedFace(f, d)
            ? m.MeshFace(f, d, c)
            : f.UnmeshedOutcome("PIPELINE-FACE-UNDISCRETIZED",
                "This face is not intact or cites an edge that has no discretization; it is not meshed.");

    static FaceMeshOutcome Unmeshed(BrepFaceIndex f, StepPlatoFailure failure)
        => f.UnmeshedOutcome("HOST-LIBRARY-THREW", $"The library threw {failure.Exception} meshing this face: {failure.Message}");

    /// <summary>One row per index, each computed by <paramref name="row"/>; a row that throws is recorded in
    /// <paramref name="failures"/> and replaced by <paramref name="fallback"/>.</summary>
    static T[] Rows<T>(int count, Func<int, T> row, Func<int, StepPlatoFailure, T> fallback, string kind, List<StepPlatoFailure> failures)
    {
        var rows = new T[count];
        for (var i = 0; i < count; i++)
        {
            try
            {
                rows[i] = row(i);
            }
            catch (Exception ex)
            {
                var failure = new StepPlatoFailure(kind, i, ex.GetType().Name, ex.Message);
                failures.Add(failure);
                rows[i] = fallback(i, failure);
            }
        }
        return rows;
    }
}
