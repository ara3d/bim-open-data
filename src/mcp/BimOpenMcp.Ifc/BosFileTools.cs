using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.BimOpenSchema.IO.Bcf;
using Ara3D.BimOpenSchema.IO.Fragments;
using Ara3D.BimOpenSchema.IO.Gltf;
using Ara3D.BimOpenSchema.IO.Usd;
using Ara3D.MCP;
using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>Tools over a .bos file rather than an open IFC session: reading a That Open Fragments
/// file into BOS, and writing a BOS model as GLB, OpenUSD, or BCF. A BOS path comes from
/// <c>ifc_to_bos</c> (its <c>bosPath</c>) or <c>frag_to_bos</c>, so one chain serves both sources.
/// Every tool here writes a file and reads its input whole on each call; none is cached.</summary>
public static class BosFileTools
{
    public static McpServer Register(this McpServer mcp)
        => mcp
            .Tool(
                "frag_to_bos",
                "Converts a That Open Fragments 2 file (.frag) to BIM Open Schema: entities, "
                + "properties, relations, and triangulated geometry. Also writes a DuckDB database "
                + "beside the .bos for SQL. Returns both paths; pass the bosPath to the bos_export_* "
                + "tools. Fails with a message naming the problem when the file is not Fragments 2.",
                McpSchema.Object()
                    .String("path", "Absolute path to the .frag file.", required: true)
                    .String("outputPath", "Path of the .bos file to write. Default: beside the .frag.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => FragToBos(args.GetRequiredString("path"), args.GetString("outputPath")),
                    ["bos_export_glb", "bos_export_usd", "bos_export_bcf"]))
            .Tool(
                "bos_export_glb",
                "Writes a BOS model's geometry to binary glTF (.glb), y-up in metres, one node per "
                + "instance with its entity index and GlobalId in the node's extras so a viewer pick "
                + "finds the element. Hidden instances are left out. Set 'ids' to entity indices "
                + "(rows of the Entities table) to export only those elements.",
                BosPath()
                    .Ids()
                    .String("outputPath", "Path of the .glb file to write.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportGlb(args.GetRequiredString("bosPath"), args.GetIds(), args.GetRequiredString("outputPath"))))
            .Tool(
                "bos_export_usd",
                "Writes a BOS model as an OpenUSD text stage (.usda) that Omniverse, Blender, Houdini "
                + "and usdview open: z-up, metres, one prim per element carrying GlobalId, name, "
                + "category and every parameter as typed bim: attributes, shared meshes as instanced "
                + "prototypes. Missing values are left out. Convert to .usdc or .usdz with usd-core "
                + "when size matters (about nine times smaller).",
                BosPath()
                    .String("outputPath", "Path of the .usda file to write.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportUsd(args.GetRequiredString("bosPath"), args.GetRequiredString("outputPath"))))
            .Tool(
                "bos_export_bcf",
                "Runs a read-only SQL query over a BOS model and writes its rows as a BCF 3.0 file "
                + "that Revit, Solibri, and BIMcollab open as issues. Each row needs GlobalId and "
                + "Title columns; rows sharing a Title become one topic. Optional columns: "
                + "Description, Status, Priority, Type. Each topic gets a viewpoint selecting its "
                + "elements with a camera framing them; a topic whose elements have no geometry gets "
                + "none, and its GlobalIds go in its description. The views are the ones ifc_sql "
                + "sees, e.g. EntityText.",
                BosPath()
                    .String("sql", "A single SELECT or WITH statement returning GlobalId and Title.", required: true)
                    .String("outputPath", "Path of the .bcf file to write.", required: true)
                    .String("guidSeed", "Optional; the same seed keeps topic GUIDs stable across reruns of one check.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportBcf(
                        args.GetRequiredString("bosPath"),
                        args.GetRequiredString("sql"),
                        args.GetRequiredString("outputPath"),
                        args.GetString("guidSeed"))));

    private static McpSchemaBuilder BosPath()
        => McpSchema.Object().String("bosPath", "Absolute path to the .bos file (from ifc_to_bos or frag_to_bos).", required: true);

    private static object FragToBos(string path, string? outputPath)
    {
        var input = new FilePath(path);
        if (!File.Exists(input.FullPath))
            throw new FileNotFoundException($"Fragments file not found: {input.FullPath}");
        var bos = Output(outputPath ?? Path.ChangeExtension(input.FullPath, ".bos"));
        var data = FragmentsToBos.Read(input);
        data.WriteToParquetZip(bos);
        var database = new FilePath(Path.ChangeExtension(bos.FullPath, ".duckdb"));
        if (File.Exists(database.FullPath))
            File.Delete(database.FullPath);
        bos.BosToDuckDB(database);
        return new
        {
            path = input.FullPath,
            bosPath = bos.FullPath,
            databasePath = database.FullPath,
            bosBytes = new FileInfo(bos.FullPath).Length,
            entities = data.Entities.Length,
            instances = data.Geometry.InstanceEntityIndex.Length,
        };
    }

    private static object ExportGlb(string bosPath, IReadOnlyList<int>? ids, string outputPath)
    {
        var output = Output(outputPath);
        var summary = BosGlb.WriteGlb(Input(bosPath).FullPath, output.FullPath, new GlbExportOptions { EntityIndices = ids });
        return new { bosPath, outputPath = output.FullPath, summary };
    }

    private static object ExportUsd(string bosPath, string outputPath)
    {
        var output = Output(outputPath);
        var summary = Read(bosPath).WriteUsda(output.FullPath);
        return new { bosPath, outputPath = output.FullPath, bytes = new FileInfo(output.FullPath).Length, summary };
    }

    private static object ExportBcf(string bosPath, string sql, string outputPath, string? guidSeed)
    {
        var data = Read(bosPath);
        using var conn = data.ToDuckDb();
        var issues = conn.Query(IfcDuck.ReadOnlyQuery(sql)).ToBcfIssues();
        var output = Output(outputPath);
        var summary = BcfWriter.WriteFile(
            output.FullPath, issues, new BcfOptions { GuidSeed = guidSeed ?? "" }, ElementBounds.FromBimData(data));
        return new { bosPath, outputPath = output.FullPath, bytes = new FileInfo(output.FullPath).Length, summary };
    }

    private static FilePath Input(string bosPath)
    {
        var input = new FilePath(bosPath);
        if (!File.Exists(input.FullPath))
            throw new FileNotFoundException($"BOS file not found: {input.FullPath}");
        return input;
    }

    private static IBimData Read(string bosPath)
        => ParquetUtils.ReadBimDataFromParquetZip(Input(bosPath));

    private static FilePath Output(string outputPath)
    {
        var output = new FilePath(outputPath);
        var directory = Path.GetDirectoryName(output.FullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return output;
    }
}
