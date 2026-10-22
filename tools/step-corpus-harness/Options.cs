namespace Ara3D.Step.CorpusHarness;

/// <summary>The flags every mode takes: <c>--parallel</c> runs the edge and face rows on every core, and
/// <c>--path</c> is <c>host</c>, the host's entries timed per row (<see cref="MeshPath"/>), or, for <c>one</c>,
/// <c>meshfile</c>, StepPlatoMesher.MeshFile itself, untimed per row, and <c>--warmup</c> meshes the file once
/// before the timed run, so the timings leave out the JIT compilation of the generated library.</summary>
public sealed record Options(bool Parallel, string Path, bool Warmup, string[] Rest, string Child = "time")
{
    /// <summary><c>--child laws</c>: the mode the all mode runs on each file, <c>time</c> by default.</summary>
    public const string ChildFlag = "--child";
    public const string ParallelFlag = "--parallel";
    public const string PathFlag = "--path";
    public const string MeshFilePath = "meshfile";
    public const string WarmupFlag = "--warmup";

    public static Options Parse(string[] args)
    {
        var rest = new List<string>();
        var parallel = false;
        var warmup = false;
        var path = MeshPath.HostName;
        var child = "time";
        for (var i = 0; i < args.Length; i++)
            if (args[i] == ChildFlag && i + 1 < args.Length)
                child = args[++i];
            else if (args[i] == ParallelFlag)
                parallel = true;
            else if (args[i] == WarmupFlag)
                warmup = true;
            else if (args[i] == PathFlag && i + 1 < args.Length)
                path = args[++i];
            else
                rest.Add(args[i]);
        return new(parallel, path, warmup, rest.ToArray(), child);
    }

    /// <summary>The flags as arguments again, for the child processes of the <c>all</c> mode.</summary>
    public IReadOnlyList<string> Flags
        => [PathFlag, Path, .. Parallel ? [ParallelFlag] : Array.Empty<string>(), .. Warmup ? [WarmupFlag] : Array.Empty<string>()];

    public static int Usage()
    {
        Console.Error.WriteLine("""
            step-corpus-harness one <file.step> <out.json> [--path host|meshfile] [--parallel] [--warmup]
            step-corpus-harness all <corpus folder> <out folder> <seconds per file> [--path ...] [--parallel] [--warmup]
            step-corpus-harness time <file.step> <out prefix> [--path host] [--parallel] [--warmup]
            step-corpus-harness laws <file.step> <out prefix> [--parallel]       (all ... --child laws runs it on every file)
            step-corpus-harness gallery <file.step | witness:Name[:x]> <out.glb> [--faces a,b,c] [--ring n] [--chordal x] [--parallel] [--warmup]
            step-corpus-harness extract <file.step | witness:Name[:x]> <face,face,...> <out.plato> [--ring n] [--name Name] [--chordal x] [--parallel]
            """);
        return 2;
    }
}
