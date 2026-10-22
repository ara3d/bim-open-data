namespace Ara3D.Step.CorpusHarness;

/// <summary><c>time file prefix</c>: writes prefix.edges.csv, prefix.faces.csv and prefix.summary.json; a throw
/// before the rows (parse, decode, prepare) leaves a failed summary with the exception.</summary>
public static class TimeMode
{
    public static int Run(string file, string prefix, Options options)
    {
        try
        {
            var t = TimedMesh.Run(file, options);
            TimeTables.Write(prefix + ".edges.csv", TimeTables.EdgeHeader, t.EdgeRows.Select(e => e.Csv()).ToArray());
            TimeTables.Write(prefix + ".faces.csv", TimeTables.FaceHeader, t.FaceRows.Select(f => f.Csv()).ToArray());
            Summary.Of(t).Write(prefix + ".summary.json");
            Console.WriteLine($"total {Format.Ms(t.TotalMs)} ms, edges {Format.Ms(t.EdgesMs)}, faces {Format.Ms(t.FacesMs)}, {t.Digest}, {t.Check}");
            return 0;
        }
        catch (Exception ex)
        {
            Summary.Failed(file, options.Path, options.Parallel, ex).Write(prefix + ".summary.json");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
