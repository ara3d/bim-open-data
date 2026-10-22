using Ara3D.IO.StepParser;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>agree file...</c>: the harness's face rows (the time mode's path, <see cref="TimedMesh"/>) against
/// StepPlatoMesher.MeshFile on every face of each file: the same face mesh and the same diagnostic codes. Exits 1 when
/// any face differs, so the tables a corpus report reads describe the mesh the host makes.</summary>
public static class AgreeMode
{
    public static int Run(IReadOnlyList<string> files, Options options)
    {
        var differ = 0;
        foreach (var file in files)
        {
            var timed = TimedMesh.Run(file, options);
            var host = new StepModel(new StepDocument(new FilePath(file))).MeshFile(TimedMesh.ChordalMillimetres);
            var bad = Enumerable.Range(0, timed.FaceRows.Count).Where(i =>
                !timed.FaceRows[i].Outcome.Mesh.Equals(host.Outcomes[i].Mesh)
                || !Codes(timed.FaceRows[i].Outcome).SequenceEqual(Codes(host.Outcomes[i]))).ToList();
            differ += bad.Count;
            Console.WriteLine($"{file}: {timed.FaceRows.Count} faces, {bad.Count} differ"
                + (bad.Count > 0 ? $" (first: {string.Join(", ", Enumerable.Take(bad, 10))})" : "")
                + $"; digest {timed.Digest.Hash} against MeshFile's {MeshDigest.Of(host.Mesh).Hash}");
        }
        return differ == 0 ? 0 : 1;
    }

    static IEnumerable<string> Codes(FaceMeshOutcome o) => o.Diagnostics.Select(d => (string)d.Code);
}
