using System.Diagnostics;
using Ara3D.Geometry;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>A mesher's output for one file, ready to write: the model (Z-up), the time it took, and the mesher's version.</summary>
public readonly record struct MesherOutput(Model3D Model, long ElapsedMs, string Version);

/// <summary>Writes one GLB per mesher per file and the manifest the comparison page reads.</summary>
public static class GlbExportSet
{
    /// <summary>The four panes, in order: folder name and flat colour (muted, distinct at a glance).</summary>
    public static readonly IReadOnlyList<(string Name, string Color)> Meshers =
    [
        ("Ara3D.Ifc.Mesher", "#4f8fc0"),
        (MesherCatalog.WebIfc, "#c08a4f"),
        (MesherCatalog.IfcLite, "#7aa86a"),
        (MesherCatalog.IfcOpenShell, "#b06a8e"),
    ];

    public static GlbManifest Write(IReadOnlyList<FilePath> ifcFiles, DirectoryPath glbDir)
    {
        var versions = new Dictionary<string, string>();
        var files = new List<GlbManifestFile>();
        foreach (var ifc in ifcFiles)
        {
            var glbs = new Dictionary<string, GlbManifestEntry>();
            foreach (var (name, color) in Meshers)
            {
                var output = Mesh(name, ifc);
                if (output is null)
                    continue;
                versions.TryAdd(name, output.Value.Version);
                var model = GlbExport.ToYUp(GlbExport.WithColor(output.Value.Model, ParseColor(color)));
                var glbFile = ifc.GetFileNameWithoutExtension() + ".glb";
                var entry = GlbExport.WriteGlb(model, glbDir.RelativeFolder(name).RelativeFile(glbFile), output.Value.ElapsedMs);
                glbs[name] = GlbManifestEntry.Of(name, glbFile, entry);
            }
            files.Add(new GlbManifestFile(ifc.GetFileName(), glbs));
        }
        var infos = Meshers.Select(m => new GlbMesherInfo(m.Name, versions.GetValueOrDefault(m.Name, "unknown"), m.Color)).ToList();
        var manifest = new GlbManifest(DateTime.UtcNow.AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond)), infos, files);
        manifest.Write(glbDir.RelativeFile("manifest.json"));
        return manifest;
    }

    /// <summary>The mesher's model of the file, or null when it has none (a missing dump, a failed run).</summary>
    public static MesherOutput? Mesh(string mesher, FilePath ifc)
        => mesher switch
        {
            "Ara3D.Ifc.Mesher" => Warm(new IfcMesherBackend(), ifc, "bim-open-data " + GitCommit()),
            MesherCatalog.WebIfc => Warm(new WebIfcBackend(), ifc, "in process"),
            _ => FromDump(mesher, ifc),
        };

    /// <summary>Meshes once untimed (JIT, caches), then reports the second run's time.</summary>
    static MesherOutput? Warm(IMeshingBackend backend, FilePath ifc, string version)
    {
        if (!backend.Build(ifc).Success)
            return null;
        var sw = Stopwatch.StartNew();
        var result = backend.Build(ifc);
        sw.Stop();
        return result is { Success: true, Model: { } model } ? new MesherOutput(model, sw.ElapsedMilliseconds, version) : null;
    }

    static MesherOutput? FromDump(string mesher, FilePath ifc)
    {
        var backend = MesherCatalog.Dumped.FirstOrDefault(b => b.Name == mesher);
        var dump = backend?.FindDump(ifc);
        if (dump is null)
            return null;
        var (header, model) = MeshDump.Read(dump.Value);
        return new MesherOutput(model, header.ElapsedMs, header.MesherVersion);
    }

    static Color ParseColor(string hex)
        => new(Convert.ToInt32(hex[1..3], 16) / 255f, Convert.ToInt32(hex[3..5], 16) / 255f, Convert.ToInt32(hex[5..7], 16) / 255f, 1f);

    static string GitCommit()
    {
        var info = new ProcessStartInfo("git", "rev-parse --short HEAD")
            { WorkingDirectory = BimOpenData.TestSupport.RepoPaths.Root, RedirectStandardOutput = true };
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output.Length > 0 ? output : "unknown";
    }
}
