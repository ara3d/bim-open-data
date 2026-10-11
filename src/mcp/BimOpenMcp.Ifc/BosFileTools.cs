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
/// file into BOS, querying a BOS model, and writing it as GLB, OpenUSD, or BCF. A BOS path comes
/// from <c>ifc_to_bos</c> (its <c>bosPath</c>), <c>frag_to_bos</c>, or disk, so one chain serves
/// every source. Each tool works on the model's <see cref="BosSession"/>, opened once per file and
/// kept in the server's <see cref="BosSessionCache"/>.</summary>
public static class BosFileTools
{
    public static McpServer Register(this McpServer mcp, BosSessionCache sessions)
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
                    () => FragToBos(sessions, args.GetRequiredString("path"), args.GetString("outputPath")),
                    ["bos_table", "bos_sql", "bos_export_glb", "bos_export_usd", "bos_export_bcf"]))
            .Tool(
                "bos_table",
                "Lists the tables and views bos_sql sees in a .bos model, with their row counts and "
                + "column types, so a query can be written without guessing. They are the ones "
                + "ifc_table describes for an IFC model: start with the EntityText, ParameterText "
                + "and RelationText views, and see ifc_table for StoreyOfElement and MetricCatalog. "
                + "Set 'table' to describe just one.",
                BosPath()
                    .String("table", "Optional single table to describe, e.g. Entities.")
                    .Paged()
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => IfcShapes.Page(
                        IfcDuck.Tables(Session(sessions, args).DatabasePath, args.GetString("table")),
                        args.Skip(),
                        args.Take()),
                    ["bos_sql"]))
            .Tool(
                "bos_sql",
                "Runs a read-only SQL query (DuckDB dialect) over a .bos model, from any source, and "
                + "returns a page of rows plus the unpaged row count. The views are the ones ifc_sql "
                + "sees: EntityText, ParameterText, RelationText, StoreyOfEntity, StoreyOfElement, "
                + "MetricCatalog; bos_table lists them. The first call builds a DuckDB database in a "
                + "temporary folder; later calls reuse it until the .bos file changes.",
                BosPath()
                    .String("sql", "A single SELECT or WITH statement.", required: true)
                    .Paged()
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => IfcDuck.Query(
                        Session(sessions, args).DatabasePath,
                        args.GetRequiredString("sql"),
                        args.Skip(),
                        args.Take())))
            .Tool(
                "bos_export_glb",
                "Writes a BOS model's geometry to binary glTF (.glb), y-up in metres, one node per "
                + "instance with its entity index and GlobalId in the node's extras so a viewer pick "
                + "finds the element. Hidden instances are left out. Set 'entityIndices' to export only "
                + "some elements; fails when none of them draws anything.",
                BosPath()
                    .String("entityIndices", "Optional comma-separated entity indices (EntityText.EntityIndex, the row in the Entities table; not STEP ids), e.g. '1796,1802'. Omit for the whole model.")
                    .String("outputPath", "Path of the .glb file to write.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportGlb(Session(sessions, args), args.GetIntList("entityIndices"), args.GetRequiredString("outputPath"))))
            .Tool(
                "bos_export_usd",
                "Writes a BOS model as an OpenUSD text stage (.usda) that Omniverse, Blender, Houdini "
                + "and usdview open: z-up, metres, one prim per entity (an Xform when it has geometry, "
                + "a Scope when it has none, such as a storey or a type) carrying GlobalId, name, "
                + "category and every parameter as typed bim: attributes, relations as bim: "
                + "relationships (bim:containedIn, bim:hostedBy, ...), shared meshes as instanced "
                + "prototypes. Missing values are left out. Convert to .usdc or .usdz with usd-core "
                + "when size matters (about eight times smaller).",
                BosPath()
                    .String("outputPath", "Path of the .usda file to write.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportUsd(Session(sessions, args), args.GetRequiredString("outputPath"))))
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
                    .String("guidSeed", "Optional; the same seed keeps topic GUIDs stable across reruns of one check. Default: the .bos file name, so two models' topics never share a GUID.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => ExportBcf(
                        Session(sessions, args),
                        args.GetRequiredString("sql"),
                        args.GetRequiredString("outputPath"),
                        args.GetString("guidSeed"))));

    private static McpSchemaBuilder BosPath()
        => McpSchema.Object().String("bosPath", "Absolute path to the .bos file (from ifc_to_bos or frag_to_bos).", required: true);

    private static BosSession Session(BosSessionCache sessions, McpToolArgs args)
        => sessions.Get(args.GetRequiredString("bosPath"));

    /// <summary>Reads the .frag, writes the .bos, and holds the model as a session, so the bos_*
    /// tools given the returned bosPath do not read it again. The database beside the .bos is a
    /// copy of the session's.</summary>
    private static object FragToBos(BosSessionCache sessions, string path, string? outputPath)
    {
        var input = new FilePath(path);
        if (!File.Exists(input.FullPath))
            throw new FileNotFoundException($"Fragments file not found: {input.FullPath}");
        var bos = Output(outputPath ?? Path.ChangeExtension(input.FullPath, ".bos"));
        var data = FragmentsToBos.Read(input);
        data.WriteToParquetZip(bos);
        var session = sessions.Put(new BosSession(bos, data));
        var database = new FilePath(Path.ChangeExtension(bos.FullPath, ".duckdb"));
        CopyInPlace(session.DatabasePath, database);
        return new
        {
            path = input.FullPath,
            bosPath = bos.FullPath,
            databasePath = database.FullPath,
            bosBytes = session.BosBytes,
            entities = data.Entities.Length,
            instances = data.Geometry.InstanceEntityIndex.Length,
        };
    }

    private static object ExportGlb(BosSession session, IReadOnlyList<int>? entityIndices, string outputPath)
    {
        var output = Output(outputPath);
        var options = new GlbExportOptions { EntityIndices = entityIndices };
        var summary = session.Scene.WriteGlb(output.FullPath, options);
        var requested = entityIndices?.Distinct().Count() ?? 0;
        if (requested > 0 && summary.UnmatchedEntityIndices == requested)
        {
            File.Delete(output.FullPath);
            throw new ArgumentException(
                $"None of the {requested} entity indices draws anything: each is out of range, has no geometry, "
                + "or only hidden instances. entityIndices are EntityText.EntityIndex values, not STEP ids.");
        }
        return new { bosPath = session.BosPath.FullPath, outputPath = output.FullPath, summary };
    }

    private static object ExportUsd(BosSession session, string outputPath)
    {
        var output = Output(outputPath);
        var summary = session.Scene.WriteUsda(output.FullPath);
        return new { bosPath = session.BosPath.FullPath, outputPath = output.FullPath, bytes = new FileInfo(output.FullPath).Length, summary };
    }

    private static object ExportBcf(BosSession session, string sql, string outputPath, string? guidSeed)
    {
        IReadOnlyList<BcfIssue> issues;
        using (var conn = BosDuckDb.Open(session.DatabasePath))
            issues = conn.Query(IfcDuck.ReadOnlyQuery(sql)).ToBcfIssues();
        var output = Output(outputPath);
        var options = new BcfOptions { GuidSeed = guidSeed ?? Path.GetFileNameWithoutExtension(session.BosPath.FullPath) };
        var summary = BcfWriter.WriteFile(output.FullPath, issues, options, ElementBounds.FromScene(session.Scene));
        return new { bosPath = session.BosPath.FullPath, outputPath = output.FullPath, bytes = new FileInfo(output.FullPath).Length, summary };
    }

    /// <summary>Copies under a temporary name and moves into place, so a failed copy never leaves
    /// a half-written database where a reader would take it for a whole one.</summary>
    private static void CopyInPlace(FilePath source, FilePath destination)
    {
        var partial = destination.FullPath + ".partial";
        File.Copy(source.FullPath, partial, overwrite: true);
        File.Move(partial, destination.FullPath, overwrite: true);
    }

    private static FilePath Output(string outputPath)
    {
        var output = new FilePath(outputPath);
        var directory = Path.GetDirectoryName(output.FullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return output;
    }
}
