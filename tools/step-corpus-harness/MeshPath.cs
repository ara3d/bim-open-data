using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>How each edge and face reaches the library: the host's own once-per-model tables (StepPlatoTables, what
/// StepPlatoMesher.MeshFile builds since TKT-402), timed, then the host's per-edge and per-face entries. Before
/// TKT-402 the harness also offered the host's per-item rescans (CanDiscretize, Discretize, IsDiscretizedFace); the
/// numbers are in bim-open-toolkit's docs/plans/step-mesher-ideas-evaluation.md, run A.</summary>
public sealed record MeshPath(string Name, double TablesMs, StepPlatoTables Tables)
{
    public const string HostName = "host";

    public static MeshPath Of(string name, BrepModel m)
        => name == HostName
            ? Host(m)
            : throw new ArgumentException($"--path is {HostName} or {Options.MeshFilePath}, not {name}", nameof(name));

    public static MeshPath Host(BrepModel m)
    {
        var (tables, ms) = Clock.Time(() => StepPlatoTables.Of(m));
        return new(HostName, ms, tables);
    }
}
