using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ara3D.BimOpenSchema.IO.Gltf;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Gltf.Tests;

/// <summary>Not part of the gate: writes every samples/public/*.bos to .glb, to print the
/// README's numbers and to run the Khronos glTF validator over the files.</summary>
[Explicit("writes every public sample; the validator needs node and npm ci in validator/")]
public class PublicSampleTests
{
    private static string OutputFolder()
        => Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "public-samples")).FullName;

    /// <summary>Writes each public sample to <paramref name="folder"/> and prints its numbers.</summary>
    private static void WritePublicSamples(string folder)
    {
        foreach (var bos in Directory.GetFiles(RepoPaths.Samples("public"), "*.bos").Order())
        {
            var path = Path.Combine(folder, Path.ChangeExtension(Path.GetFileName(bos), ".glb"));
            var watch = Stopwatch.StartNew();
            var data = ParquetUtils.ReadBimDataFromParquetZip(bos);
            var read = watch.ElapsedMilliseconds;
            var summary = data.WriteGlb(path);
            TestContext.Progress.WriteLine(
                $"{Path.GetFileName(bos)}: {new FileInfo(bos).Length} bytes in, {data.Geometry.InstanceEntityIndex.Length} instances, " +
                $"{summary.Nodes} nodes, {summary.Meshes} meshes, {summary.Triangles} triangles, {summary.Bytes} bytes out; " +
                $"read {read} ms, write {watch.ElapsedMilliseconds - read} ms");
        }
    }

    [Test]
    public void Measure_public_samples()
        => WritePublicSamples(OutputFolder());

    [Test]
    public void Public_samples_pass_the_Khronos_validator()
    {
        var validator = ValidatorFolder();
        if (!Directory.Exists(Path.Combine(validator, "node_modules", "gltf-validator")))
            Assert.Ignore($"run npm ci in {validator} first");

        var folder = OutputFolder();
        WritePublicSamples(folder);

        var start = new ProcessStartInfo("node", ["validate-gltf.mjs", folder])
        {
            WorkingDirectory = validator,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Win32Exception)
        {
            Assert.Ignore("node is not on PATH");
            return;
        }
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            TestContext.Progress.WriteLine(output.Result + error.Result);
            Assert.That(process.ExitCode, Is.Zero, output.Result + error.Result);
        }
    }

    private static string ValidatorFolder([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "validator");
}
