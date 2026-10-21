using System.Diagnostics;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One edge's discretization and its timing: <see cref="CheckMs"/> is the host path's CanDiscretize test (0 on the hoisted path),
/// <see cref="Ms"/> the whole edge. <see cref="Exception"/> is empty unless the library threw.</summary>
public sealed record EdgeRow(
    int Index, string Curve, string Surfaces, int Segments, double CheckMs, double Ms, string Exception, string Message,
    EdgeDiscretization Discretization);

public static class EdgeTiming
{
    /// <summary>One edge discretized along <paramref name="path"/>, timed; a throw becomes the undiscretized edge.
    /// </summary>
    public static EdgeRow Time(this BrepModel m, int i, string surfaces, MeshPath path)
    {
        var e = new BrepEdgeIndex(i);
        var curve = m.CurveName(e);
        var start = Stopwatch.GetTimestamp();
        var checkMs = 0.0;
        try
        {
            var can = path.CanDiscretize(e);
            checkMs = Clock.Since(start);
            var d = can ? path.Discretize(e) : e.Undiscretized();
            return new(i, curve, surfaces, Math.Max(0, d.Parameters.Count - 1), checkMs, Clock.Since(start), "", "", d);
        }
        catch (Exception ex)
        {
            return new(i, curve, surfaces, 0, checkMs, Clock.Since(start), ex.GetType().Name, ex.Message, e.Undiscretized());
        }
    }
}
