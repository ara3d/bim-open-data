using Ara3D.IfcLoader;
using BenchmarkDotNet.Attributes;

namespace Ara3D.Ifc.Mesher.Benchmarks;

/// <summary>Warm timings per model: mesher alone, parse plus mesh, and the web-ifc DLL.</summary>
[SimpleJob(warmupCount: 3, iterationCount: 8, invocationCount: 1, id: "Short")]
public class MesherBenchmarks
{
    public static IEnumerable<string> Files() => BenchFiles.Existing().Select(p => Path.GetFileName(p)!);

    [ParamsSource(nameof(Files))]
    public string File { get; set; } = "";

    string _path = "";
    IfcFile? _parsed;

    [GlobalSetup]
    public void Setup()
    {
        _path = BenchFiles.Resolve(File)!;
        _parsed = Operations.Parse(_path);
    }

    [GlobalCleanup]
    public void Cleanup() => _parsed?.Dispose();

    [Benchmark] public int MesherOnly() => Operations.Mesh(_parsed!);
    [Benchmark] public int ParseAndMesh() => Operations.ParseAndMesh(_path);
    [Benchmark] public int WebIfc() => Operations.WebIfc(_path);
}
