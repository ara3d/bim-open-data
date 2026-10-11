using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>The BIM Open Schema conversion of one IFC file: a .bos archive in
/// <see cref="DerivedFiles"/>, in a folder named by the IFC file's path, size and write time, so a
/// restarted server reuses it and a changed file gets a new one. The <see cref="IfcSession"/> that
/// asked for it opens the archive as a <see cref="BosSession"/>, which holds the tables, the scene,
/// and the DuckDB database.
/// Conversion is a second whole-file parse that also loads geometry — <see cref="IfcToBosConverter"/>
/// hardcodes <c>includeGeometry: true</c> — so it is paid once per file, not per query.</summary>
public sealed class IfcBosArtifacts
{
    /// <summary>Finds the conversion of <paramref name="source"/>, or converts it;
    /// <paramref name="rebuild"/> converts even when a current conversion exists.</summary>
    public IfcBosArtifacts(FilePath source, DerivedFiles files, bool rebuild = false)
    {
        Source = source;
        var name = Path.GetFileNameWithoutExtension(source.FullPath) + ".bos";
        BosPath = files.GetOrBuild("bos", source, name, folder =>
        {
            Build(source, new FilePath(Path.Combine(folder, name)));
            return [];
        }, rebuild).Path;
        BuiltUtc = File.GetLastWriteTimeUtc(BosPath.FullPath);
    }

    public FilePath Source { get; }

    public FilePath BosPath { get; }

    /// <summary>When the .bos was written, by this server or an earlier one.</summary>
    public DateTime BuiltUtc { get; }

    /// <summary>Runs the converter directly rather than through <c>IfcToBosConverter.Convert</c>,
    /// which never disposes the <c>IfcFile</c> it opens; a long-lived server cannot afford to leak
    /// a pinned whole-file buffer and a native web-ifc model per conversion.</summary>
    private static void Build(FilePath input, FilePath output)
    {
        var converter = new IfcToBosConverter(input);
        try
        {
            converter.SaveToBos(output);
        }
        finally
        {
            converter.IfcFile?.Dispose();
        }
    }
}
