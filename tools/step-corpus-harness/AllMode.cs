using System.Diagnostics;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>all corpus outdir seconds</c>: every .step and .stp file under the corpus folder meshed by the time mode
/// in its own process, killed after the wall-clock limit, so outdir holds each file's edge and face tables and
/// summary; outdir/all.csv names each file's status, time and the last stage line it printed (<see cref="Progress"/>).
/// </summary>
public static class AllMode
{
    public static int Run(string corpus, string outDir, int seconds, Options options)
    {
        Directory.CreateDirectory(outDir);
        var files = Directory.EnumerateFiles(corpus, "*", SearchOption.AllDirectories)
            .Where(IsStep)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rows = files.Select(f => RunOne(corpus, f, outDir, seconds, options)).ToArray();
        TimeTables.Write(Path.Combine(outDir, "all.csv"), "file,status,ms,last_stage", rows);
        return 0;
    }

    static bool IsStep(string f)
        => f.EndsWith(".step", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".stp", StringComparison.OrdinalIgnoreCase);

    static string RunOne(string corpus, string file, string outDir, int seconds, Options options)
    {
        var name = Path.GetRelativePath(corpus, file);
        var prefix = Path.Combine(outDir, name.Replace(Path.DirectorySeparatorChar, '_'));
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "time", file, prefix }.Concat(options.Flags))
            start.ArgumentList.Add(a);
        var clock = Stopwatch.GetTimestamp();
        using var p = Process.Start(start)!;
        var last = "";
        p.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) last = line; };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        var finished = p.WaitForExit(seconds * 1000);
        if (!finished)
            p.Kill(true);
        p.WaitForExit();
        var status = !finished ? "timeout" : p.ExitCode == 0 ? "done" : "failed";
        var row = TimeTables.Join(name, status, Format.Ms(Clock.Since(clock)), last);
        Console.WriteLine(row);
        return row;
    }
}
