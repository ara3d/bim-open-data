using System.Text.Json;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One file's totals as JSON: what the one mode writes, and the time mode beside its CSVs.</summary>
public sealed record Summary(
    string File, string Path, bool Parallel, string Status, int Edges, int Faces, int EdgesThrew, int FacesThrew,
    int FacesMeshed, double ParseMs, double DecodeMs, double PrepareMs, double TablesMs, double EdgesMs, double FacesMs,
    double RefineMs, double AssembleMs, double TotalMs, MeshDigest? Digest, MeshCheck? Check, IReadOnlyList<string> Failures,
    string? Contract = null, bool? ClosedShellsClosed = null, string? Boundary = null, double? ContractMs = null,
    long? AllocatedBytes = null)
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static Summary Of(TimedMesh t)
        => new(t.File, t.Path, t.Parallel, "done", t.Edges, t.Faces,
            t.EdgeRows.Count(e => e.Exception != ""), t.FaceRows.Count(f => f.Exception != ""),
            t.FaceRows.Count(f => f.Triangles + f.Quads > 0), t.ParseMs, t.DecodeMs, t.PrepareMs, t.TablesMs, t.EdgesMs,
            t.FacesMs, t.RefineMs, t.AssembleMs, t.TotalMs, t.Digest, t.Check,
            t.EdgeRows.Where(e => e.Exception != "").Select(e => $"edge {e.Index} {e.Curve}: {e.Exception}: {e.Message}")
                .Concat(t.FaceRows.Where(f => f.Exception != "").Select(f => $"face {f.Index} {f.Surface}: {f.Exception}: {f.Message}"))
                .Concat(t.RefineFailures.Select(f => $"{f.Kind} round {f.Index}: {f.Exception}: {f.Message}"))
                .ToArray(),
            t.Contract.ToString(), t.Boundary.ClosedShellsAreClosed, t.Boundary.ToString(), t.ContractMs, t.AllocatedBytes);

    public static Summary Failed(string file, string path, bool parallel, Exception ex)
        => new(file, path, parallel, "failed", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, [ex.ToString()]);

    public void Write(string json)
        => System.IO.File.WriteAllText(json, JsonSerializer.Serialize(this, Indented));
}
