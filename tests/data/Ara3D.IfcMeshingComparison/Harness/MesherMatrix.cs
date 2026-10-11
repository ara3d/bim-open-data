using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>One mesher's output for one file: what it produced and how long it took. A mesher whose
/// dump is missing has <see cref="Available"/> false and the reason in <see cref="Error"/>.</summary>
public sealed record MesherRun(
    string Mesher,
    string Version,
    bool Available,
    int Meshes,
    int Instances,
    int Triangles,
    float[]? BoundsMin,
    float[]? BoundsMax,
    long ElapsedMs,
    string? Error);

/// <summary>The parity of one mesher scored against another as the oracle. The metrics are not
/// symmetric, so both orders appear in a matrix.</summary>
public sealed record PairScore(string Candidate, string Oracle, double Parity, IReadOnlyDictionary<string, double> Metrics);

public sealed record FileMatrix(string FileName, IReadOnlyList<MesherRun> Meshers, IReadOnlyList<PairScore> Pairs);

public sealed record MesherMatrixDocument(DateTime GeneratedUtc, IReadOnlyList<FileMatrix> Files);

/// <summary>Every mesher against every other on a set of files: the rows of
/// <c>bench/ifc-mesher/meshers.json</c> and the matrix in its RESULTS.md.</summary>
public static class MesherMatrix
{
    const string InProcess = "in process";

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static FileMatrix Build(FilePath ifcPath, IReadOnlyList<IMeshingBackend> backends)
    {
        var results = backends.Select(b => (Backend: b, Result: b.Build(ifcPath))).ToList();
        var runs = results.Select(r => ToRun(r.Backend, r.Result, ifcPath)).ToList();
        var built = results.Where(r => r.Result.Success).ToList();
        var pairs = built
            .SelectMany(candidate => built.Where(oracle => oracle.Backend != candidate.Backend).Select(oracle => (candidate, oracle)))
            .AsParallel().AsOrdered()
            .Select(pair =>
            {
                var comparison = ModelComparer.Compare(pair.candidate.Result.Model!, pair.oracle.Result.Model!, ifcPath.GetFileName());
                return new PairScore(pair.candidate.Backend.Name, pair.oracle.Backend.Name, comparison.ParityScore, comparison.MetricScores);
            })
            .ToList();
        return new FileMatrix(ifcPath.GetFileName(), runs, pairs);
    }

    public static void WriteJson(FilePath path, MesherMatrixDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path.Value)!);
        File.WriteAllText(path, JsonSerializer.Serialize(document, Json));
    }

    /// <summary>A Markdown section per file: the mesher rows, then the parity matrix with candidates
    /// down the side and oracles across the top.</summary>
    public static string FormatMarkdown(MesherMatrixDocument document)
    {
        var sb = new StringBuilder();
        foreach (var file in document.Files)
        {
            sb.AppendLine($"### {file.FileName}");
            sb.AppendLine();
            sb.AppendLine("| Mesher | Version | Meshes | Instances | Triangles | Bounds min | Bounds max | Time (ms) |");
            sb.AppendLine("|---|---|---:|---:|---:|---|---|---:|");
            foreach (var run in file.Meshers)
            {
                sb.AppendLine(run.Available
                    ? $"| {run.Mesher} | {run.Version} | {run.Meshes:N0} | {run.Instances:N0} | {run.Triangles:N0} | {Vec(run.BoundsMin)} | {Vec(run.BoundsMax)} | {run.ElapsedMs:N0} |"
                    : $"| {run.Mesher} | {run.Version} | | | | | | {run.Error} |");
            }
            sb.AppendLine();
            var names = file.Meshers.Where(m => m.Available).Select(m => m.Mesher).ToList();
            sb.AppendLine("Parity, row scored against column as the oracle:");
            sb.AppendLine();
            sb.AppendLine("| Candidate \\ Oracle | " + string.Join(" | ", names) + " |");
            sb.AppendLine("|---|" + string.Concat(names.Select(_ => "---:|")));
            foreach (var candidate in names)
            {
                var cells = names.Select(oracle => candidate == oracle
                    ? "–"
                    : file.Pairs.First(p => p.Candidate == candidate && p.Oracle == oracle).Parity.ToString("F3"));
                sb.AppendLine($"| {candidate} | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static MesherRun ToRun(IMeshingBackend backend, MeshingResult result, FilePath ifcPath)
    {
        var version = backend is DumpBackend dump ? DumpVersion(dump, ifcPath) : InProcess;
        if (!result.Success)
            return new MesherRun(backend.Name, version, false, 0, 0, 0, null, null, 0, string.Join("; ", result.Errors));
        var b = result.Bounds;
        return new MesherRun(
            backend.Name, version, true, result.MeshCount, result.InstanceCount, result.TriangleCount,
            [(float)b.Min.X.Value, (float)b.Min.Y.Value, (float)b.Min.Z.Value],
            [(float)b.Max.X.Value, (float)b.Max.Y.Value, (float)b.Max.Z.Value],
            result.ElapsedMs, null);
    }

    static string DumpVersion(DumpBackend backend, FilePath ifcPath)
    {
        var dump = backend.FindDump(ifcPath);
        return dump is null ? "no dump" : MeshDump.ReadHeader(dump.Value).MesherVersion;
    }

    static string Vec(float[]? v)
        => v is null ? "" : $"({v[0]:F2}, {v[1]:F2}, {v[2]:F2})";
}
