using System.Diagnostics;
using System.Text.Json;
using Ara3D.BimOpenSchema.IO;
using Ara3D.BimOpenSchema.IO.Usd;
using Ara3D.Utils;
using Platonic;

namespace BosToUsd;

/// <summary>
/// Command line over <see cref="BosUsdExport.WriteUsda(Ara3D.BimOpenSchema.IBimData, string)"/>:
/// <c>bos-to-usd input.bos output.usda</c> writes the stage and prints the export summary as JSON.
/// Convert the result to .usdc or .usdz with usd-core (see usd-web-viewer/tools/usd_to_usdz.py).
/// </summary>
[Impure]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: bos-to-usd <input.bos> <output.usda>");
            return 2;
        }
        try
        {
            var clock = Stopwatch.StartNew();
            var data = new FilePath(args[0]).ReadBimDataFromParquetZip();
            var readMs = clock.ElapsedMilliseconds;
            var summary = data.WriteUsda(args[1]);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                input = args[0],
                output = args[1],
                outputBytes = new FileInfo(args[1]).Length,
                readMs,
                writeMs = clock.ElapsedMilliseconds - readMs,
                summary,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
