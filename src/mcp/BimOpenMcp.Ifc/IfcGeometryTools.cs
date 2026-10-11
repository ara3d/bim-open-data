using System.Text.Json.Nodes;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO.Gltf;
using Ara3D.Ifc.Mesher;
using Ara3D.MCP;
using Ara3D.Utils;
using SharpGLTF.Schema2;

namespace BimOpenMcp.Ifc;

/// <summary>Geometry tools: mesh statistics, bounds, volume, GLB export, and a look at what failed to
/// mesh. The measurements come from the Approach1 mesher, a pure-C# reader of STEP geometry
/// definitions. It needs no native tessellator and no geometry-enabled reopen, so those tools run
/// against the same <see cref="IfcSession"/> the data tools opened with <c>includeGeometry: false</c>;
/// the meshed model is built once per session and reused. A model whose mesher failed is reported
/// through <c>ifc_meshing_diagnostics</c> rather than crashing the other tools.
/// The GLB export instead writes the session's BIM Open Schema conversion (web-ifc geometry, the
/// model the SQL tools read) from the cached <see cref="BosSession.Scene"/> through
/// <see cref="BosGlb"/>: on the AC20-FZK-Haus sample Approach1 leaves the four roof-clipped upper
/// walls unclipped, up to 2.8 m too tall, where the conversion clips them
/// (GlbSourceComparisonTests prints the per-element comparison).</summary>
public static class IfcGeometryTools
{
    /// <summary>Node extras key for the element's STEP id (the #123 in the .ifc file), written beside
    /// <see cref="BosGlb.EntityIndexKey"/> and <see cref="BosGlb.GlobalIdKey"/>.</summary>
    public const string StepIdKey = "stepId";

    public static McpServer Register(this McpServer mcp, IfcSessionCache cache)
        => mcp
            .Tool(
                "ifc_mesh",
                "Reports the triangle geometry of a model per element: instance, triangle and vertex "
                + "counts, signed volume, surface area and bounds. Set 'ids' to a comma-separated list "
                + "to restrict to specific elements. This returns measurements, not raw vertices — use "
                + "ifc_export_glb to get the geometry itself.",
                IfcToolArgs.Model().Ids().Paged().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Mesh(args.Session(cache), args.GetIds(), args.Skip(), args.Take()),
                    ["ifc_export_glb", "ifc_bounds", "ifc_volume"]))
            .Tool(
                "ifc_bounds",
                "Returns the axis-aligned bounding box of the whole model and, unless 'ids' narrows it, "
                + "of each element. Every box is given as min, max, center and size.",
                IfcToolArgs.Model().Ids().Paged().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Bounds(args.Session(cache), args.GetIds(), args.Skip(), args.Take())))
            .Tool(
                "ifc_volume",
                "Returns signed volume and surface area computed from the meshed geometry: model totals "
                + "plus a per-element breakdown. Signed volume is negative when a solid's faces wind "
                + "inward, so its magnitude is the physical volume. Set 'ids' to restrict the breakdown.",
                IfcToolArgs.Model().Ids().Paged().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Volume(args.Session(cache), args.GetIds(), args.Skip(), args.Take())))
            .Tool(
                "ifc_export_glb",
                "Writes a model's geometry to binary glTF (.glb), y-up in metres, returning the path, byte "
                + "size and counts. One node per instance, with its own material, and the element's "
                + "stepId, globalId and entityIndex (its row in the session's BOS tables, as ifc_sql "
                + "sees them) in the node's extras so a viewer pick finds the element. The geometry is "
                + "the model's BIM Open Schema conversion (web-ifc), built once per session; it can "
                + "differ from ifc_mesh, which uses the Approach1 mesher. Without 'ids', hidden "
                + "instances (spaces, zones, grids, annotations) are left out; 'ids' writes the named "
                + "elements, hidden or not, lists those that draw nothing in unmatchedIds, and fails when "
                + "none draws.",
                IfcToolArgs.Model()
                    .Ids()
                    .String("outputPath", "Path of the .glb file to write.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Export(args.Session(cache), args.GetIds(), args.GetRequiredString("outputPath"))))
            .Tool(
                "ifc_meshing_diagnostics",
                "Reports whether the mesher succeeded and every message and error it produced — the way "
                + "to see what geometry could not be built and why. Messages are paged; errors are "
                + "returned whole.",
                IfcToolArgs.Model().Paged().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Diagnostics(args.Session(cache), args.Skip(), args.Take())));

    private static object Mesh(IfcSession session, IReadOnlyList<int>? ids, int skip, int take)
        => new
        {
            path = session.Path.FullPath,
            model = ModelSummary(session.Meshing),
            elements = IfcShapes.Page(session.Elements(ids), skip, take),
        };

    private static object Bounds(IfcSession session, IReadOnlyList<int>? ids, int skip, int take)
    {
        var elements = session.Elements(ids);
        var boxes = new IfcElementBox[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            boxes[i] = new IfcElementBox(elements[i].Id, elements[i].Type, elements[i].Name, elements[i].Bounds);

        return new
        {
            path = session.Path.FullPath,
            model = IfcBox.From(session.Meshing.Bounds),
            elements = IfcShapes.Page<IfcElementBox>(boxes, skip, take),
        };
    }

    private static object Volume(IfcSession session, IReadOnlyList<int>? ids, int skip, int take)
    {
        var elements = session.Elements(ids);
        var quantities = new IfcElementQuantity[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            quantities[i] = new IfcElementQuantity(
                elements[i].Id, elements[i].Type, elements[i].Name, elements[i].SignedVolume, elements[i].SurfaceArea);

        return new
        {
            path = session.Path.FullPath,
            model = new { signedVolume = session.Meshing.SignedVolume, triangleCount = session.Meshing.TriangleCount },
            elements = IfcShapes.Page<IfcElementQuantity>(quantities, skip, take),
        };
    }

    private static object Export(IfcSession session, IReadOnlyList<int>? ids, string outputPath)
    {
        var bos = session.Bos;
        var data = bos.Data;
        var options = new GlbExportOptions
        {
            EntityIndices = ids == null ? null : EntityRows(data, ids),
            IncludeHidden = ids != null,
        };
        var (model, summary) = bos.Scene.ToGltf(options);
        var written = AddStepIds(model, data);
        var unmatched = ids?.Distinct().Where(id => !written.Contains(id)).ToList() ?? [];
        if (ids is { Count: > 0 } && unmatched.Count == ids.Distinct().Count())
            throw new ArgumentException(
                $"None of the {unmatched.Count} ids draws anything: each is not a STEP id in the file, "
                + "or its element has no geometry.");

        var output = new FilePath(outputPath);
        var directory = Path.GetDirectoryName(output.FullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        model.SaveGLB(output.FullPath);

        return new
        {
            path = session.Path.FullPath,
            outputPath = output.FullPath,
            bytes = new FileInfo(output.FullPath).Length,
            instanceCount = summary.Nodes,
            meshCount = summary.Meshes,
            materialCount = summary.Materials,
            triangleCount = summary.Triangles,
            unmatchedIds = unmatched,
        };
    }

    /// <summary>The rows of the Entities table whose LocalId, where the converter keeps the STEP id,
    /// is one of <paramref name="stepIds"/>.</summary>
    private static HashSet<int> EntityRows(IBimData data, IReadOnlyList<int> stepIds)
    {
        var wanted = stepIds.Select(id => (long)id).ToHashSet();
        var rows = new HashSet<int>();
        for (var row = 0; row < data.Entities.Length; row++)
            if (wanted.Contains(data.Entities[row].LocalId))
                rows.Add(row);
        return rows;
    }

    /// <summary>Adds the STEP id to the extras of every node whose entity has one, and returns the
    /// STEP ids written. An entity without one (LocalId -1) keeps only the ids it has.</summary>
    private static HashSet<int> AddStepIds(ModelRoot model, IBimData data)
    {
        var written = new HashSet<int>();
        foreach (var node in model.LogicalNodes)
        {
            if (node.Extras is not JsonObject extras || extras[BosGlb.EntityIndexKey] is not JsonValue row)
                continue;
            var stepId = data.Entities[row.GetValue<int>()].LocalId;
            if (stepId < 0)
                continue;
            extras[StepIdKey] = stepId;
            written.Add((int)stepId);
        }
        return written;
    }

    private static object Diagnostics(IfcSession session, int skip, int take)
    {
        var result = session.Meshing;
        return new
        {
            path = session.Path.FullPath,
            mesherName = result.MesherName,
            success = result.Success,
            meshCount = result.MeshCount,
            instanceCount = result.InstanceCount,
            triangleCount = result.TriangleCount,
            errorCount = result.Errors.Count,
            errors = result.Errors,
            messages = IfcShapes.Page(result.Messages, skip, take),
        };
    }

    private static object ModelSummary(IfcMeshingResult result)
        => new
        {
            success = result.Success,
            meshCount = result.MeshCount,
            instanceCount = result.InstanceCount,
            triangleCount = result.TriangleCount,
            signedVolume = result.SignedVolume,
            bounds = IfcBox.From(result.Bounds),
        };

    private readonly record struct IfcElementBox(int Id, string Type, string Name, IfcBox Bounds);

    private readonly record struct IfcElementQuantity(
        int Id, string Type, string Name, double SignedVolume, double SurfaceArea);
}
