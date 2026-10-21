using System.Text;

namespace Ara3D.Step.CorpusHarness;

/// <summary>The time mode's two CSV tables, one row per edge and one per face.</summary>
public static class TimeTables
{
    public const string EdgeHeader = "index,curve,surfaces,segments,check_ms,ms,exception,message";

    public static readonly string FaceHeader =
        $"index,surface,loops,edges,all_lines,segments,triangles,quads,path,{FaceStages.Header},ms,exception,message";

    public static string Csv(this EdgeRow e)
        => Join(e.Index.ToString(), e.Curve, e.Surfaces, e.Segments.ToString(), Format.Ms(e.CheckMs), Format.Ms(e.Ms),
            e.Exception, e.Message);

    public static string Csv(this FaceRow f)
        => Join(f.Index.ToString(), f.Surface, f.Loops.ToString(), f.Edges.ToString(), f.AllLines ? "1" : "0",
            f.Segments.ToString(), f.Triangles.ToString(), f.Quads.ToString(), f.Path) + "," + f.Stages.Csv() + ","
            + Join(Format.Ms(f.Ms), f.Exception, f.Message);

    public static void Write(string path, string header, IReadOnlyList<string> rows)
    {
        var text = new StringBuilder(header).Append('\n');
        foreach (var r in rows)
            text.Append(r).Append('\n');
        File.WriteAllText(path, text.ToString());
    }

    public static string Join(params string[] fields)
        => string.Join(',', fields.Select(Format.Field));
}
