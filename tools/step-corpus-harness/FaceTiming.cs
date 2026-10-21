using System.Diagnostics;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One face's outcome and timing. <see cref="Path"/> names the branch MeshFace took: undiscretized, poly-loop,
/// not-simple, grid, or cdt (triangulated and refined); "threw" when the library threw.</summary>
public sealed record FaceRow(
    int Index, string Surface, int Loops, int Edges, bool AllLines, int Segments, int Triangles, int Quads, string Path,
    FaceStages Stages, double Ms, string Exception, string Message, FaceMeshOutcome Outcome);

public static class FaceTiming
{
    /// <summary>StepPlatoMesher.FaceOutcome restated stage by stage: the path's discretized-face test, then layer 5's
    /// MeshFace, MeshRegion and MeshRegionOnce and MeshedOutcomeIn, inlined from the generated C#
    /// (FaceMeshingDomainOnce.g.cs) so each stage has its own clock. The outcome is the one MeshFace returns; the
    /// harness's one mode, which calls MeshFile itself, gives the same mesh digest.</summary>
    public static FaceRow Time(this BrepModel m, int i, IReadOnlyList<BrepEdgeIndex> edges, EdgeDiscretizations d,
        FaceMeshingCriteria c, MeshPath meshPath)
    {
        var f = new BrepFaceIndex(i);
        var ms = new double[FaceStages.All.Length];
        var path = "threw";
        var start = Stopwatch.GetTimestamp();
        var segments = edges.Sum(e => Math.Max(0, d.ByEdge[e.Value].Parameters.Count - 1));
        FaceRow Row(FaceMeshOutcome o, string exception = "", string message = "")
            => new(i, m.SurfaceName(f), m.Faces[i].Bounds.Count, edges.Count, Enumerable.All(edges, e => m.IsLine(e)), segments,
                o.Mesh.Triangles.Count, o.Mesh.Quads.Count, path, new(ms), Clock.Since(start), exception, message, o);
        T Stage<T>(FaceStage s, Func<T> step)
        {
            var (value, t) = Clock.Time(step);
            ms[(int)s] += t;
            return value;
        }
        try
        {
            if (!Stage(FaceStage.Check, () => meshPath.IsDiscretized(f, d)))
                return Row(f.UnmeshedOutcome("PIPELINE-FACE-UNDISCRETIZED",
                    "This face is not intact or cites an edge that has no discretization; it is not meshed."), path = "undiscretized");
            if (m.HasPolyLoop(f))
                return Row(f.UnmeshedOutcome("FACE-POLY-LOOP",
                    "This face is bounded by a poly loop, which version 1 does not mesh; the decoder should give it edges."), path = "poly-loop");
            var dom = Stage(FaceStage.Region, () => m.SurfaceDomainOf(f));
            var raw = Stage(FaceStage.Region, () => m.UnorientedRegionOfIn(f, dom, d, c));
            var r = Stage(FaceStage.Region, () => m.OrientRegion(f, raw));
            var chart = Stage(FaceStage.Chart, () => m.ChartOf(f, r));
            var tol = Stage(FaceStage.Chart, () => m.RegionToleranceIn(f, dom, chart));
            var flags = Stage(FaceStage.Simple, () => r.CollidingSegments(tol));
            if (Enumerable.Any(flags, x => (bool)x))
            {
                path = "not-simple";
                return Row(Stage(FaceStage.Advisory, () => f.NotSimpleOutcome(m.SelfIntersectionRequestsFrom(f, d, r, flags))));
            }
            var grid = Stage(FaceStage.Grid, () => m.FaceQuadGridIn(f, d, r, tol, c));
            var useGrid = !grid.IsNoGrid() && !grid.IsCountsDiffer();
            path = useGrid ? "grid" : "cdt";
            var t = useGrid ? grid.Grid_Mesh : Stage(FaceStage.Triangulate, () => r.TriangulateRegion(chart));
            if (!useGrid)
                t = Stage(FaceStage.Refine, () => m.RefineToSurface(f, r, chart, t, c));
            var advisory = Stage(FaceStage.Advisory, () => m.CoarseSegmentRequests(f, d, r, c).Concatenate(m.QuadCountRequests(d, grid, c)));
            var mesh = Stage(FaceStage.Lift, () => m.Lift(f, d, t));
            var diagnostics = Stage(FaceStage.Advisory, () => advisory.Map(q => q.RequestDiagnostic()).Concatenate(m.ReversalDiagnostics(f, raw)));
            return Row(new FaceMeshOutcome(mesh, advisory, diagnostics));
        }
        catch (Exception ex)
        {
            path = "threw";
            return Row(f.UnmeshedOutcome("HOST-LIBRARY-THREW", $"The library threw {ex.GetType().Name} meshing this face: {ex.Message}"),
                ex.GetType().Name, ex.Message);
        }
    }
}
