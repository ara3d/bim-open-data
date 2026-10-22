using System.Diagnostics;
using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One face's outcome and timing. <see cref="Path"/> names what MeshFace did, read from its outcome: grid, cdt
/// or grid+cdt for a meshed face (quads, triangles, or both), undiscretized when an edge of the face has no
/// discretization, the face diagnostic's code without its FACE- prefix and in lower case for an unmeshed face
/// (poly-loop, loop-not-simple, zero-width, ...), "refined" after the host's refinement loop, "threw" when the library
/// threw.</summary>
public sealed record FaceRow(
    int Index, string Surface, int Loops, int Edges, bool AllLines, int Segments, int Triangles, int Quads, string Path,
    FaceStages Stages, double Ms, string Exception, string Message, FaceMeshOutcome Outcome);

public static class FaceTiming
{
    /// <summary>The row of a face the host's refinement loop meshed again: its final outcome, path "refined", and its
    /// first-pass time and stages.</summary>
    public static FaceRow Refined(this FaceRow f, FaceMeshOutcome o)
        => f with { Outcome = o, Path = "refined", Triangles = o.Mesh.Triangles.Count, Quads = o.Mesh.Quads.Count, Exception = "", Message = "" };

    /// <summary>StepPlatoTables.FaceOutcome, the call StepPlatoMesher.MeshFile makes for each face, timed in its two
    /// parts: the host's discretized-face test (check) and layer 5's MeshFace (mesh). Nothing of MeshFace is restated
    /// here, so the harness meshes as the library does whatever its face path becomes; the agree mode checks every face
    /// against MeshFile.</summary>
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
            if (!Stage(FaceStage.Check, () => meshPath.Tables.IsDiscretized(m, f, d)))
                return Row(StepPlatoTables.UndiscretizedOutcome(f), path = "undiscretized");
            var o = Stage(FaceStage.Mesh, () => m.MeshFace(f, d, c));
            path = PathOf(o);
            return Row(o);
        }
        catch (Exception ex)
        {
            path = "threw";
            return Row(f.UnmeshedOutcome("HOST-LIBRARY-THREW", $"The library threw {ex.GetType().Name} meshing this face: {ex.Message}"),
                ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>What MeshFace did, from its outcome (see <see cref="FaceRow"/>).</summary>
    public static string PathOf(FaceMeshOutcome o)
    {
        int triangles = o.Mesh.Triangles.Count, quads = o.Mesh.Quads.Count;
        if (triangles + quads > 0)
            return quads == 0 ? "cdt" : triangles == 0 ? "grid" : "grid+cdt";
        var code = o.Diagnostics.Select(x => (string)x.Code).FirstOrDefault(x => x.StartsWith("FACE-")) ?? o.Diagnostics.Select(x => (string)x.Code).FirstOrDefault() ?? "unmeshed";
        return (code.StartsWith("FACE-") ? code[5..] : code).ToLowerInvariant();
    }
}
