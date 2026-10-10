using System.Text;

namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes a BOS model as an OpenUSD text stage (.usda): z-up, metres, default prim /Model.
/// The text is streamed, so memory stays proportional to the BOS model, not to the output.
/// See README.md for the prim layout and attribute names.
/// </summary>
public static class BosUsdExport
{
    private const int BufferSize = 1 << 16;

    /// <summary>Writes the stage to <paramref name="usdaPath"/>, replacing any file there. The
    /// text goes to a temporary file in the same folder, which is moved into place only when
    /// the whole stage is written; if writing fails, the temporary file is deleted and any
    /// earlier file at <paramref name="usdaPath"/> is left as it was.</summary>
    public static UsdExportSummary WriteUsda(this IBimData data, string usdaPath)
    {
        var fullPath = Path.GetFullPath(usdaPath);
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!,
            "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            UsdExportSummary summary;
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize))
            using (var text = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), BufferSize))
                summary = data.WriteUsda(text);
            File.Move(temporary, fullPath, overwrite: true);
            return summary;
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    /// <summary>Writes the stage to <paramref name="output"/>. Numbers are formatted in the
    /// invariant culture whatever the writer's culture is; the caller owns and flushes the writer.</summary>
    public static UsdExportSummary WriteUsda(this IBimData data, TextWriter output)
    {
        var g = data.Geometry ?? new BimGeometry();
        var w = new UsdaWriter(output);

        w.Line("#usda 1.0").Line("(").Line()
            .Text("    defaultPrim = \"").Text(UsdNames.Root).Text('"').End()
            .Line("    doc = \"BIM Open Schema model written by Ara3D.BimOpenSchema.IO.Usd\"")
            .Line("    metersPerUnit = 1")
            .Line("    upAxis = \"Z\"")
            .Line(")").End();

        w.Line().Text("def Xform \"").Text(UsdNames.Root).Text('"').OpenMetadata();
        w.Line("kind = \"assembly\"");
        w.CloseMetadata().Open();
        var materials = UsdGeometry.WriteMaterials(w, g);
        var prototypes = UsdGeometry.WritePrototypes(w, g);
        var summary = UsdElements.Write(w, data, g, materials, prototypes);
        w.Close();
        return summary;
    }
}
