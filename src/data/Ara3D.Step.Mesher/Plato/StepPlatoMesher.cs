using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>
/// Meshes a whole STEP file through the Plato STEP library compiled to C# (Ara3D.StepBrep): the decoder's DecodeRaw,
/// the library's PrepareModel, then under layer 8's HostMeshingOptions (the defaults with quad grids preferred, owner
/// decision of 2026-10-21, TKT-405): the once-per-model tables (<see cref="StepPlatoTables"/>), every edge discretized
/// once (layer 4's DiscretizeAllRanged) and every face given layer 8's FaceOutcome (layer 5's MeshFace when its edges
/// are discretized), both on every core; then layer 8's refinement loop on the blocking requests only
/// (<see cref="Refined"/>); and the face meshes numbered and assembled (layer 6). Layer 8's MeshModel is generated but
/// not called: its loop answers advisory requests too and re-meshes every face at each round (the unit cylinder ran
/// over 10 minutes against about 4.5 s here, TKT-313 log 2026-10-19). Library code that throws on one edge or face is
/// caught here and becomes an empty outcome and a <see cref="StepPlatoFailure"/>, so a surface the library cannot
/// mesh yet leaves every other face meshed. Decoding is per file because DecodeRaw and PrepareModel are: the tolerances
/// come from the whole model's size, and mirrored occurrences are folded in place.
/// The version 1 contract check (chordal bound, closed shells, orientation, degenerate elements) is not run here, in any
/// configuration: it is the corpus harness's ContractCheck, MeshCheck and BoundaryCheck, run on the finished mesh when the
/// harness or a test asks, and layer 7's validation is never called from this path (TKT-489).
/// </summary>
public static class StepPlatoMesher
{
    /// <summary>The <see cref="StepPlatoFailure.Kind"/> of a throw inside the refinement loop; its index is the round.</summary>
    public const string RefineFailure = "refine";

    /// <param name="maxRefinementRounds">The refinement loop's round limit; null for the options' own, 0 for the first
    /// pass alone.</param>
    public static StepPlatoMesh MeshFile(this StepModel model, double chordalMillimetres, int? maxRefinementRounds = null)
    {
        var raw = model.DecodeRaw();
        var prepared = raw.PrepareModel();
        var m = prepared.Model;
        var o = Options(chordalMillimetres);
        if (maxRefinementRounds is { } rounds)
            o = o.WithRounds(rounds);
        var tables = StepPlatoTables.Of(m);
        var failures = new List<StepPlatoFailure>();
        var d = new EdgeDiscretizations(Rows(m.Edges.Count, i => tables.Discretized(m, new BrepEdgeIndex(i), o.Face.Discretization),
            (i, _) => new BrepEdgeIndex(i).Undiscretized(), StepPlatoFailure.Edge, failures));
        var outcomes = Rows(m.Faces.Count, i => tables.FaceOutcome(m, new BrepFaceIndex(i), d, o.Face),
            (i, failure) => Unmeshed(new BrepFaceIndex(i), failure), StepPlatoFailure.Face, failures);
        var final = tables.Refined(m, o, d, outcomes, failures);
        var faceMeshes = final.Outcomes.Select(x => x.Mesh).ToArray();
        var mesh = m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(final.Discretizations, faceMeshes));
        return new(raw, prepared, final.Outcomes, mesh, failures);
    }

    /// <summary>Layer 8's refinement loop over a first pass the host meshed itself, answering blocking requests only.
    /// Each round is layer 8's Step from its parts (StepRefined, StepRemeshed, the per-face FaceOutcomeInRange,
    /// StepNext; Step's body is StepByParts, Law_V1Proof_StepByPartsIsStep) with the remeshed faces meshed on every
    /// core, each throw caught as in the first pass (owner decision of TKT-403), then BlockingOnly: advisory requests
    /// stay unanswered, which is what made MeshModel re-mesh every face each round. A throw in Refine ends the loop at
    /// the last whole round and is recorded as a refine failure. PlatoRefinementTests checks the result equals layer
    /// 8's FinalStateAnsweringBlocking, Step in sequence.</summary>
    public static PipelineState Refined(this StepPlatoTables tables, BrepModel m, MeshingOptions o, EdgeDiscretizations d,
        IReadOnlyList<FaceMeshOutcome> outcomes, List<StepPlatoFailure> failures)
    {
        var chosen = m.AllFaces();
        var s = d.BlockingStateOf(outcomes, chosen);
        while (!(bool)s.IsTerminal(o))
        {
            EdgeDiscretizations refined;
            try
            {
                refined = m.StepRefined(o, s);
            }
            catch (Exception ex)
            {
                failures.Add(new StepPlatoFailure(RefineFailure, (int)s.Round, ex.GetType().Name, ex.Message));
                break;
            }
            var remeshed = m.StepRemeshed(chosen, s, refined);
            var previous = s.Outcomes;
            var next = Rows(m.Faces.Count, i => (bool)remeshed[i]
                    ? m.FaceOutcomeInRange(new BrepFaceIndex(i), refined, o, chosen, tables.ArenaOk) : previous[i],
                (i, failure) => Unmeshed(new BrepFaceIndex(i), failure), StepPlatoFailure.Face, failures);
            s = m.StepNext(s, refined, remeshed, next).BlockingOnly();
        }
        return s;
    }

    /// <summary>Layer 8's HostMeshingOptions for an absolute chordal bound in model units.</summary>
    public static MeshingOptions Options(double chordal)
        => ((Number)chordal).HostMeshingOptions();

    static FaceMeshOutcome Unmeshed(BrepFaceIndex f, StepPlatoFailure failure)
        => f.UnmeshedOutcome("HOST-LIBRARY-THREW", $"The library threw {failure.Exception} meshing this face: {failure.Message}");

    /// <summary>One row per index, each computed by <paramref name="row"/> on every core (owner decision 2 of
    /// docs/plans/step-mesher-brainstorm-2026-10-21.md; the library is pure and the tables are read-only); a row that
    /// throws is recorded in <paramref name="failures"/>, in row order, and replaced by <paramref name="fallback"/>.
    /// </summary>
    static T[] Rows<T>(int count, Func<int, T> row, Func<int, StepPlatoFailure, T> fallback, string kind, List<StepPlatoFailure> failures)
    {
        var rows = new T[count];
        var failed = new StepPlatoFailure?[count];
        Parallel.For(0, count, i =>
        {
            try
            {
                rows[i] = row(i);
            }
            catch (Exception ex)
            {
                var failure = new StepPlatoFailure(kind, i, ex.GetType().Name, ex.Message);
                failed[i] = failure;
                rows[i] = fallback(i, failure);
            }
        });
        for (var i = 0; i < count; i++)
            if (failed[i] is { } failure)
                failures.Add(failure);
        return rows;
    }
}
