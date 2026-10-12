using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

/// <summary>Writes <c>bench/ifc-mesher/glb/</c>: one GLB per mesher per sample file and the manifest
/// the four-pane page reads (docs/plans/mesher-comparison-page.md).</summary>
[TestFixture]
public class GlbExportTests
{
    static readonly DirectoryPath GlbDir = TestFiles.BenchDir.RelativeFolder("glb");

    [Test]
    public void SampleFiles_WriteOneGlbPerMesher_AndAManifest()
    {
        var files = TestFiles.QuickComparisonFiles().Append(TestFiles.Duplex).ToList();
        foreach (var file in files)
            TestFiles.RequireExists(file);

        var manifest = GlbExportSet.Write(files, GlbDir);

        Assert.That(manifest.Files.Select(f => f.Name), Is.EqualTo(files.Select(f => f.GetFileName())));
        foreach (var file in manifest.Files)
        {
            Assert.That(file.Glbs.ContainsKey("Ara3D.Ifc.Mesher"), $"{file.Name}: Ara3D.Ifc.Mesher has no GLB");
            Assert.That(file.Glbs.ContainsKey("WebIfc"), $"{file.Name}: WebIfc has no GLB");
            foreach (var (mesher, entry) in file.Glbs)
            {
                var glb = GlbDir.RelativeFile(entry.Path);
                Assert.That(glb.Exists(), $"{glb} was not written");
                Assert.That(new FileInfo(glb).Length, Is.GreaterThan(1024), glb.ToString());
                AssertNodeCount(glb, entry.Instances);
            }
        }
    }

    static void AssertNodeCount(FilePath glb, int instances)
    {
        var root = SharpGLTF.Schema2.ModelRoot.Load(glb);
        Assert.That(root.LogicalNodes.Count, Is.EqualTo(instances), glb.ToString());
    }
}
