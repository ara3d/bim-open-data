using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using Platonic;

namespace IfcToBos;

/// <summary>
/// Command line over <see cref="IfcToBosConverter"/>, the call behind the IFC MCP server's
/// <c>ifc_to_bos</c> tool: <c>ifc-to-bos input.ifc output.bos</c> writes the same .bos file, geometry
/// included. The first output line is the tool's name and version; the second is a JSON object with
/// the output size and the conversion time.
/// </summary>
[Impure]
internal static class Program
{
    private static int Main(string[] args)
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: ifc-to-bos <input.ifc> <output.bos>");
            return 2;
        }
        IfcToBosConverter? converter = null;
        try
        {
            Console.WriteLine($"ifc-to-bos {version}");
            var output = new FilePath(args[1]);
            var folder = Path.GetDirectoryName(output.FullPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            var clock = Stopwatch.StartNew();
            converter = new IfcToBosConverter(new FilePath(args[0]));
            var readMs = clock.ElapsedMilliseconds;
            converter.SaveToBos(output);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                input = args[0],
                output = args[1],
                outputBytes = new FileInfo(args[1]).Length,
                readMs,
                writeMs = clock.ElapsedMilliseconds - readMs,
            }));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        finally
        {
            // IfcToBosConverter.Convert never disposes the file it opens; this does.
            converter?.IfcFile?.Dispose();
        }
    }
}
