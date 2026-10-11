using System.Diagnostics;
using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>Not part of the gate: times a first and a repeated call of bos_sql and of
/// bos_export_glb on the committed Schependomlaan sample, each through tools/call on a fresh
/// server, and prints the milliseconds. The second call shows what a BOS session saves.</summary>
[Explicit("timing only; prints milliseconds and asserts nothing about them")]
public sealed class BosSessionTimingTests
{
    private static readonly string Bos = RepoPaths.Samples("public", "schependomlaan.bos");

    [Test]
    public void Sql_FirstAndSecondCall()
        => Time("bos_sql", () => new JsonObject { ["bosPath"] = Bos, ["sql"] = "SELECT count(*) FROM EntityText" });

    [Test]
    public void ExportGlb_FirstAndSecondCall()
    {
        var scratch = RepoPaths.Artifacts("bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Time("bos_export_glb", () => new JsonObject { ["bosPath"] = Bos, ["outputPath"] = Path.Combine(scratch, "s.glb") });
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static void Time(string tool, Func<JsonObject> arguments)
    {
        using var cache = new IfcSessionCache();
        using var mcp = IfcMcpServer.Create(cache, McpTransport.Stdio);
        var watch = Stopwatch.StartNew();
        mcp.CallData(tool, arguments());
        var first = watch.ElapsedMilliseconds;
        watch.Restart();
        mcp.CallData(tool, arguments());
        TestContext.Progress.WriteLine($"{tool} on {Path.GetFileName(Bos)}: first call {first} ms, second call {watch.ElapsedMilliseconds} ms");
    }
}
