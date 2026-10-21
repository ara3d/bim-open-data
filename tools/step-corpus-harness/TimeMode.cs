namespace Ara3D.Step.CorpusHarness;

/// <summary><c>time file prefix</c>: writes prefix.edges.csv, prefix.faces.csv and prefix.summary.json.</summary>
public static class TimeMode
{
    public static int Run(string file, string prefix, Options options)
    {
        var t = TimedMesh.Run(file, options);
        TimeTables.Write(prefix + ".edges.csv", TimeTables.EdgeHeader, t.EdgeRows.Select(e => e.Csv()).ToArray());
        TimeTables.Write(prefix + ".faces.csv", TimeTables.FaceHeader, t.FaceRows.Select(f => f.Csv()).ToArray());
        Summary.Of(t).Write(prefix + ".summary.json");
        Console.WriteLine($"total {Format.Ms(t.TotalMs)} ms, edges {Format.Ms(t.EdgesMs)}, faces {Format.Ms(t.FacesMs)}, {t.Digest}");
        return 0;
    }
}
