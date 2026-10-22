using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>
/// Meshes a whole STEP file through the Plato STEP library compiled to C# (Ara3D.StepBrep): the decoder's DecodeRaw,
/// the library's PrepareModel, then what layer 8's MeshModel does before its refinement loop: the once-per-model
/// tables (<see cref="StepPlatoTables"/>), every edge discretized once (layer 4's DiscretizeAllRanged), every face
/// given layer 8's FaceOutcome (layer 5's MeshFace when its edges are discretized), then layer 8's refinement loop on
/// the blocking requests only (<see cref="Refined"/>), and the face meshes numbered and assembled (layer 6). Layer 8's
/// MeshModel is generated but not called: its loop answers advisory requests too and re-meshes every face at each
/// round (the unit cylinder ran over 10 minutes against about 4.5 s here, TKT-313 log 2026-10-19). Library code that throws on one edge or face is caught here and
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

    /// <summary>The <see cref="StepPlatoFailure.Kind"/> of a throw inside the refinement loop; its index is the round.</summary>
    public const string RefineFailure = "refine";

    /// <param name="maxRefinementRounds">The refinement loop's round limit; null for layer 8's default, 0 for the
    /// first pass alone.</param>
    public static StepPlatoMesh MeshFile(this StepModel model, double chordalMillimetres, int? maxRefinementRounds = null)
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
        var final = tables.Refined(m, criteria, maxRefinementRounds ?? MaxRefinementRounds(chordalMillimetres), d, outcomes, failures);
        var faceMeshes = final.Outcomes.Select(o => o.Mesh).ToArray();
        var mesh = m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(final.Discretizations, faceMeshes));
        return new(raw, prepared, final.Outcomes, mesh, failures);
    }

    /// <summary>Layer 8's refinement loop (its Step, restated here) over the host's first pass, answering blocking
    /// requests only (RefinementReason.IsBlocking, such as FACE-NOT-SIMPLE's "refine this edge"): layer 4's Refine on
    /// the pending requests, then the face outcome again on exactly the faces that cite a changed edge (layer 8's
    /// FacesTouchingByCoedges), so both faces of a refined edge take its new points and the mesh stays watertight; their
    /// blocking requests become pending; up to <paramref name="maxRounds"/>. Advisory requests are left unanswered:
    /// answering them is what made MeshModel re-mesh every face each round (TKT-313 log 2026-10-19). Step itself is
    /// not called for two reasons: Refine needs <see cref="RefineCriteria"/>, which Step would also hand to MeshFace
    /// (on u-blox ANN-MB2 a 0.0004 mm minimum length kept one round's MeshFace running for over 10 minutes), and the
    /// faces are meshed here in parallel, each throw caught as in the first pass. A throw in Refine ends the loop at the
    /// last whole round and is recorded as a refine failure. Step's segment floors are not kept: the host discretizes
    /// with none.</summary>
    public static PipelineState Refined(this StepPlatoTables tables, BrepModel m, FaceMeshingCriteria criteria, int maxRounds,
        EdgeDiscretizations d, IReadOnlyList<FaceMeshOutcome> outcomes, List<StepPlatoFailure> failures)
    {
        var chosen = Enumerable.Repeat((StepBrep.Boolean)true, m.Faces.Count).ToArray();
        var s = new PipelineState(0, d, SegmentFloors.NoFloors(), outcomes, Blocking(outcomes.SelectMany(x => x.Requests)));
        while (s.Pending.Count > 0 && (int)s.Round < maxRounds)
        {
            EdgeDiscretizations refined;
            try
            {
                refined = m.Refine(s.Discretizations, s.Pending, RefineCriteria(criteria.Discretization, s.Pending));
            }
            catch (Exception ex)
            {
                failures.Add(new StepPlatoFailure(RefineFailure, (int)s.Round, ex.GetType().Name, ex.Message));
                break;
            }
            var remeshed = m.FacesTouchingByCoedges(chosen, refined.ChangedEdges(s.Discretizations));
            var previous = s.Outcomes;
            var next = Rows(m.Faces.Count, i => (bool)remeshed[i] ? tables.FaceOutcome(m, new BrepFaceIndex(i), refined, criteria) : previous[i],
                (i, failure) => Unmeshed(new BrepFaceIndex(i), failure), StepPlatoFailure.Face, failures);
            s = new PipelineState(s.Round.Add(1), refined, s.Floors, next, Blocking(next.RequestsOf(remeshed)));
        }
        return s;
    }

    /// <summary>Layer 8's DefaultMeshingOptions refinement round limit.</summary>
    public static int MaxRefinementRounds(double chordal)
        => (int)((Number)chordal).DefaultMeshingOptions().MaxRefinementRounds;

    /// <summary>The criteria Refine answers the pending requests with: the minimum segment length lowered to
    /// DefaultCriteria's ratio (0.1) of the smallest positive chordal bound they ask for. Layer 4's Refine lowers the
    /// chordal bound to the requested one but keeps MinLength, so at the host's 1 mm bound (MinLength 0.1 mm) an arc
    /// under 0.2 mm long cannot be split at all, and a face whose boundary segments collide there stays unmeshed
    /// however the loop runs (u-blox CAM face 124: two one-segment arcs asked for 0.0029 mm). The edge's tolerance stays
    /// the floor (layer 4's EffectiveMinLength). Refine touches only the requested edges.</summary>
    public static DiscretizationCriteria RefineCriteria(DiscretizationCriteria c, IReadOnlyList<EdgeRefinementRequest> pending)
    {
        var chordals = pending.Select(q => (double)q.Chordal).Where(x => x > 0).ToList();
        return chordals.Count == 0 || 0.1 * chordals.Min() >= (double)c.MinLength ? c : c.WithMinLength(0.1 * chordals.Min());
    }

    static IReadOnlyList<EdgeRefinementRequest> Blocking(IEnumerable<EdgeRefinementRequest> requests)
        => requests.Where(q => (bool)q.Reason.IsBlocking()).ToList();

    /// <summary>Layer 8's DefaultMeshingOptions criteria for an absolute chordal bound in model units.</summary>
    public static FaceMeshingCriteria Criteria(double chordal)
        => new(((Number)chordal).DefaultCriteria(), chordal, ((Number)SurfaceAngleDegrees).Degrees(), PreferQuads);

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
