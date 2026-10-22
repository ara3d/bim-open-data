using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>
/// Meshes a whole STEP file through the Plato STEP library compiled to C# (Ara3D.StepBrep): the decoder's DecodeRaw,
/// the library's PrepareModel, then what layer 8's MeshModel does before its refinement loop: the once-per-model
/// tables (<see cref="StepPlatoTables"/>), every edge discretized once (layer 4's DiscretizeAllRanged), every face
/// given layer 8's FaceOutcome (layer 5's MeshFace when its edges are discretized), and the face meshes numbered and
/// assembled (layer 6). Layer 8's MeshModel is generated but not called: its refinement loop re-meshes every face at
/// each round (the unit cylinder ran over 10 minutes against about 4.5 s here, TKT-313 log 2026-10-19); when the loop
/// is affordable this becomes one call to MeshModel. Library code that throws on one edge or face is caught here and
/// becomes an empty outcome and a <see cref="StepPlatoFailure"/>, so a surface the library cannot mesh yet leaves every
/// other face meshed. Decoding is per file because DecodeRaw and PrepareModel are: the tolerances come from the whole
/// model's size, and mirrored occurrences are folded in place.
/// </summary>
public static class StepPlatoMesher
{
    /// <summary>Layer 8's DefaultMeshingOptions surface angle: the normal turns at most 15 degrees along an element
    /// edge.</summary>
    public const double SurfaceAngleDegrees = 15;

    /// <summary>Layer 5's structured quad grid meshes the faces it fits (four-edge and two-loop cylinder faces,
    /// rectangles) as quads, which <see cref="StepPlatoBodies.ToItemMetres"/> splits for the contract; the grid skips
    /// the 15 degree side-normal criterion, which refinement could never meet on a chord over 30 degrees of arc, and
    /// keeps the chordal bound. Owner decision of 2026-10-21 (TKT-405): about 48% of face time on the baseline
    /// files.</summary>
    public const bool PreferQuads = true;

    public static StepPlatoMesh MeshFile(this StepModel model, double chordalMillimetres)
    {
        var raw = model.DecodeRaw();
        var prepared = raw.PrepareModel();
        var m = prepared.Model;
        var criteria = Criteria(chordalMillimetres);
        var tables = StepPlatoTables.Of(m);
        var failures = new List<StepPlatoFailure>();
        var d = new EdgeDiscretizations(Rows(m.Edges.Count, i => tables.Discretized(m, new BrepEdgeIndex(i), criteria.Discretization),
            (i, _) => new BrepEdgeIndex(i).Undiscretized(), StepPlatoFailure.Edge, failures));
        var outcomes = Rows(m.Faces.Count, i => tables.FaceOutcome(m, new BrepFaceIndex(i), d, criteria),
            (i, failure) => Unmeshed(new BrepFaceIndex(i), failure), StepPlatoFailure.Face, failures);
        var faceMeshes = outcomes.Select(o => o.Mesh).ToArray();
        var mesh = m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(d, faceMeshes));
        return new(raw, prepared, outcomes, mesh, failures);
    }

    /// <summary>Layer 8's DefaultMeshingOptions criteria for an absolute chordal bound in model units.</summary>
    public static FaceMeshingCriteria Criteria(double chordal)
        => new(((Number)chordal).DefaultCriteria(), chordal, ((Number)SurfaceAngleDegrees).Degrees(), PreferQuads);

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
