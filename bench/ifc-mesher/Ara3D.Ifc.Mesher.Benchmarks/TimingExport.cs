using System.Globalization;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Reports;

namespace Ara3D.Ifc.Mesher.Benchmarks;

/// <summary>Writes bench/ifc-mesher/timing.md and timing.json from a finished BenchmarkDotNet run.</summary>
public static class TimingExport
{
    public static void Write(Summary summary, string commit)
    {
        var rows = summary.Reports
            .Where(r => r.ResultStatistics != null)
            .Select(r => (File: r.BenchmarkCase.Parameters["File"]?.ToString() ?? "",
                          Op: r.BenchmarkCase.Descriptor.WorkloadMethod.Name,
                          Mean: r.ResultStatistics!.Mean / 1e6,
                          Median: r.ResultStatistics.Median / 1e6,
                          StdDev: r.ResultStatistics.StandardDeviation / 1e6))
            .ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException("No benchmark produced a result; timing files not written.");
        var dir = BenchFiles.BenchDir();
        var env = summary.HostEnvironmentInfo;
        var cpu = env.CpuInfo.Value.ProcessorName;
        var machine = $"{(string.IsNullOrWhiteSpace(cpu) ? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") : cpu)}, {Environment.ProcessorCount} logical cores, {env.OsVersion.Value}, {env.RuntimeVersion}";

        File.WriteAllText(Path.Combine(dir, "timing.json"), JsonSerializer.Serialize(new
        {
            commit, machine, unit = "ms",
            rows = rows.Select(r => new { file = r.File, operation = r.Op, meanMs = Round(r.Mean), medianMs = Round(r.Median), stdDevMs = Round(r.StdDev) }),
        }, new JsonSerializerOptions { WriteIndented = true }));

        var sb = new StringBuilder();
        sb.AppendLine("# Mesher timing (warm)").AppendLine();
        sb.AppendLine("Written by `Ara3D.Ifc.Mesher.Benchmarks` (BenchmarkDotNet, short job: 3 warm-ups, 8 timed runs each). Times are in milliseconds; the median is the figure to quote.").AppendLine();
        sb.AppendLine($"- Machine: {machine}");
        sb.AppendLine($"- Commit: {commit}").AppendLine();
        sb.AppendLine("| file | mesher only (ms) | parse + mesh (ms) | web-ifc DLL (ms) | mesher / web-ifc |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var g in rows.GroupBy(r => r.File))
        {
            double M(string op) => g.First(r => r.Op == op).Median;
            sb.AppendLine($"| {g.Key} | {F(M("MesherOnly"))} | {F(M("ParseAndMesh"))} | {F(M("WebIfc"))} | {F(M("MesherOnly") / M("WebIfc"))} |");
        }
        File.WriteAllText(Path.Combine(dir, "timing.md"), sb.ToString());
    }

    static double Round(double v) => Math.Round(v, 2);
    static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
