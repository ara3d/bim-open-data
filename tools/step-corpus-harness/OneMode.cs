using Ara3D.IO.StepParser;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>one file out.json</c>: one file meshed, its <see cref="Summary"/> written. With <c>--path meshfile</c>
/// the mesh comes from StepPlatoMesher.MeshFile itself (parallel rows since TKT-403), which checks that the time
/// mode's sequential restatement gives the same <see cref="MeshDigest"/>.</summary>
public static class OneMode
{
    public static int Run(string file, string json, Options options)
    {
        try
        {
            var summary = options.Path == Options.MeshFilePath
                ? MeshFile(file)
                : Summary.Of(TimedMesh.Run(file, options));
            summary.Write(json);
            Console.WriteLine($"total {Format.Ms(summary.TotalMs)} ms, {summary.Digest}, {summary.Check}");
            return 0;
        }
        catch (Exception ex)
        {
            Summary.Failed(file, options.Path, options.Parallel, ex).Write(json);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    static Summary MeshFile(string file)
    {
        var (mesh, ms) = Clock.Time(() =>
            new StepModel(new StepDocument(new FilePath(file))).MeshFile(TimedMesh.ChordalMillimetres));
        return new(file, Options.MeshFilePath, false, "done", mesh.Model.Edges.Count, mesh.Model.Faces.Count,
            mesh.Failures.Count(f => f.Kind == StepPlatoFailure.Edge), mesh.Failures.Count(f => f.Kind == StepPlatoFailure.Face),
            mesh.Outcomes.Count(o => o.Mesh.Triangles.Count + o.Mesh.Quads.Count > 0), 0, 0, 0, 0, 0, 0, 0, ms,
            MeshDigest.Of(mesh.Mesh), MeshCheck.Of(mesh.Mesh), mesh.Failures.Select(f => $"{f.Kind} {f.Index}: {f.Exception}: {f.Message}").ToArray());
    }
}
