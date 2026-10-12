using System.Diagnostics;

namespace Ara3D.Ifc.Mesher.Benchmarks;

/// <summary>One in-process stopwatch pass: explicit warm-up runs, then the median of timed runs, printed as a table.</summary>
public static class QuickRun
{
    const int Warmup = 2;
    const int Runs = 5;

    public static void Run()
    {
        Console.WriteLine($"{"file",-24}{"mesher ms",12}{"parse+mesh ms",16}{"web-ifc ms",12}{"mesher/web-ifc",16}");
        foreach (var path in BenchFiles.Existing())
        {
            using var parsed = Operations.Parse(path);
            var mesh = Median(() => Operations.Mesh(parsed));
            var both = Median(() => Operations.ParseAndMesh(path));
            var web = Median(() => Operations.WebIfc(path));
            Console.WriteLine($"{Path.GetFileName(path),-24}{mesh,12:F1}{both,16:F1}{web,12:F1}{mesh / web,16:F1}");
        }
    }

    static double Median(Func<int> op)
    {
        for (var i = 0; i < Warmup; i++) op();
        var times = new double[Runs];
        for (var i = 0; i < Runs; i++)
        {
            var sw = Stopwatch.StartNew();
            op();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return times[Runs / 2];
    }
}
