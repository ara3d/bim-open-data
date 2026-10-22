namespace Ara3D.Step.CorpusHarness;

/// <summary><c>time file prefix</c>: writes prefix.edges.csv, prefix.faces.csv, prefix.contract.csv (the faces that break
/// the version 1 contract, <see cref="ContractCheck"/>) and prefix.summary.json; the last line printed, which the all mode
/// keeps in all.csv, ends with the contract's verdict; a throw
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
            TimeTables.Write(prefix + ".contract.csv", FaceContract.CsvHeader, t.Contract.Failing.Select(f => f.Csv(t.Contract.Bound)).ToArray());
            Summary.Of(t).Write(prefix + ".summary.json");
            Console.WriteLine($"total {Format.Ms(t.TotalMs)} ms, edges {Format.Ms(t.EdgesMs)}, faces {Format.Ms(t.FacesMs)}, contract {Format.Ms(t.ContractMs)} ms, {t.Digest}, {t.Check}, {t.Boundary}, {t.Contract}");
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
