using System.Diagnostics;
using Ara3D.BimOpenSchema.IO.Gltf;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Gltf.Tests;

/// <summary>BosGlb.WriteGlb(path) reads only the tables an export uses (BosGlb.TablesRead). A table
/// left unread comes back empty, so a wrong choice would change the file silently: these tests write each
/// sample both ways and compare the two files byte for byte, including every node's name and GlobalId.</summary>
public class SelectiveReadTests
{
    [TestCase("duplex.bos")]
    [TestCase("schependomlaan.bos")]
    public void Export_from_a_path_equals_export_from_a_full_read(string sample)
    {
        var bos = RepoPaths.Samples("public", sample);
        if (!File.Exists(bos))
            Assert.Ignore($"{bos} not found");
        var folder = TestContext.CurrentContext.WorkDirectory;
        var selectivePath = Path.Combine(folder, $"selective-{sample}.glb");
        var fullPath = Path.Combine(folder, $"full-{sample}.glb");

        var selective = BosGlb.WriteGlb(bos, selectivePath);
        var full = ParquetUtils.ReadBimDataFromParquetZip(bos).WriteGlb(fullPath);
        TestContext.Progress.WriteLine($"{sample}: read {ReadMilliseconds(bos, BosGlb.TablesRead)} ms selective, {ReadMilliseconds(bos, BosTables.All)} ms full (best of 3, after the writes above warmed the code)");

        Assert.That(selective, Is.EqualTo(full));
        Assert.That(File.ReadAllBytes(selectivePath), Is.EqualTo(File.ReadAllBytes(fullPath)));
    }

    private static long ReadMilliseconds(string bos, BosTables tables)
    {
        var best = long.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            ParquetUtils.ReadBimDataFromParquetZip(bos, tables);
            best = Math.Min(best, watch.ElapsedMilliseconds);
        }
        return best;
    }

    [Test]
    public void Selective_read_leaves_the_tables_an_export_ignores_empty()
    {
        var bos = RepoPaths.Samples("public", "duplex.bos");
        if (!File.Exists(bos))
            Assert.Ignore($"{bos} not found");
        var data = ParquetUtils.ReadBimDataFromParquetZip(bos, BosGlb.TablesRead);
        Assert.That(data.Entities, Is.Not.Empty);
        Assert.That(data.Strings, Is.Not.Empty);
        Assert.That(data.Parameters, Is.Empty);
        Assert.That(data.Relations, Is.Empty);
    }
}
