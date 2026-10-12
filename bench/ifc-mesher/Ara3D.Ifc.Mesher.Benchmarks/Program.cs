using System.Diagnostics;
using Ara3D.Ifc.Mesher.Benchmarks;
using BenchmarkDotNet.Running;

// --quick: one stopwatch pass, no BenchmarkDotNet. Otherwise the full run, which writes timing.md and timing.json.
if (args.Contains("--quick") || Environment.GetEnvironmentVariable("MESHER_BENCH_QUICK") == "1")
{
    QuickRun.Run();
    return;
}

var summary = BenchmarkRunner.Run<MesherBenchmarks>();
TimingExport.Write(summary, GitCommit());

static string GitCommit()
{
    try
    {
        var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
        { WorkingDirectory = BenchFiles.RepoRoot(), RedirectStandardOutput = true };
        using var p = Process.Start(psi)!;
        var text = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit();
        return text;
    }
    catch { return "unknown"; }
}
