using Ara3D.Ifc.Mesher;
using Ara3D.Ifc.Mesher.Tests;
using Ara3D.Ifc.Mesher.Tests.GeometryOracles;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Boolean clips in the corpus models.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusClipTests
{
    [Test]
    [Category("Slow")]
    public void SteelPlates_ClippedBeams_FirstPrinciples()
    {
        var path = TestFiles.ResolveOrIgnore("steelplates.ifc");
        using var file = new IfcFile(path, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        foreach (var id in new[] { 1193, 633, 1385 })
        {
            var entity = ctx.GetEntityOrDefault(id);
            if (entity is null)
            {
                Assert.Ignore($"#{id} missing in steelplates.ifc");
                return;
            }
            var mesh = ModelAssembler.BuildEntityMesh(ctx, entity);
            Assert.That(mesh, Is.Not.Null, $"#{id} should produce a mesh");
            var m = mesh!.Value;
            MeshTestAssert.MeshValid(m);
            Assert.That(Math.Abs(MeshHelpers.SignedVolume(m)), Is.GreaterThan(0), $"#{id} abs signed volume");
            Assert.That(TopologyOracle.CountOpenEdges(m), Is.LessThan(64),
                $"#{id} open-edge budget for clipped beam");
        }
    }
}
