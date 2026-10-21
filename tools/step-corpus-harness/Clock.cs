using System.Diagnostics;

namespace Ara3D.Step.CorpusHarness;

public static class Clock
{
    public static double Since(long start)
        => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    public static (T Value, double Ms) Time<T>(Func<T> f)
    {
        var start = Stopwatch.GetTimestamp();
        var value = f();
        return (value, Since(start));
    }
}
