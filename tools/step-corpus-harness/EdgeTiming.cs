using System.Diagnostics;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One edge's discretization and its timing. <see cref="Exception"/> is empty unless the library threw.
/// </summary>
public sealed record EdgeRow(
    int Index, string Curve, string Surfaces, int Segments, double Ms, string Exception, string Message,
    EdgeDiscretization Discretization);

public static class EdgeTiming
{
    /// <summary>One edge discretized along <paramref name="path"/>, timed; a throw becomes the undiscretized edge.
    /// </summary>
    public static EdgeRow Time(this BrepModel m, int i, string surfaces, DiscretizationCriteria c, MeshPath path)
    {
        var e = new BrepEdgeIndex(i);
        var curve = m.CurveName(e);
        var start = Stopwatch.GetTimestamp();
        try
        {
            var d = path.Tables.Discretized(m, e, c);
            return new(i, curve, surfaces, Math.Max(0, d.Parameters.Count - 1), Clock.Since(start), "", "", d);
        }
        catch (Exception ex)
        {
            return new(i, curve, surfaces, 0, Clock.Since(start), ex.GetType().Name, ex.Message, e.Undiscretized());
        }
    }
}
