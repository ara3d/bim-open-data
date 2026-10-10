using Ara3D.BimOpenSchema.IO.Gltf;
using SharpGLTF.Schema2;

namespace Ara3D.BimOpenSchema.IO.Gltf.Tests;

/// <summary>A BimGeometry built by hand, as a converter or a test would, with the defaults it
/// leaves (an empty InstanceFlags column) and indices past the end of their tables. The
/// export skips or defaults each bad instance and counts it; it never throws.</summary>
public class HandBuiltGeometryTests
{
    /// <summary>One triangle mesh, one red material, one identity transform, and five instances
    /// of entities 0 to 4: good; material 7 (no such row); transform 5 (no such row); mesh 4 (no
    /// such row); no material (-1).</summary>
    private static BimData Model()
    {
        var geometry = new BimGeometry
        {
            VertexX = [0, 10_000, 0],
            VertexY = [0, 0, 10_000],
            VertexZ = [0, 0, 0],
            IndexBuffer = [0, 1, 2],
            MeshVertexOffset = [0],
            MeshIndexOffset = [0],
            MaterialRed = [255], MaterialGreen = [0], MaterialBlue = [0], MaterialAlpha = [255],
            MaterialMetallic = [0], MaterialRoughness = [128],
            TransformTX = [0], TransformTY = [0], TransformTZ = [0],
            TransformQX = [0], TransformQY = [0], TransformQZ = [0], TransformQW = [1],
            TransformSX = [1], TransformSY = [1], TransformSZ = [1],
            InstanceEntityIndex = [0, 1, 2, 3, 4],
            InstanceMeshIndex = [0, 0, 0, 4, 0],
            InstanceMaterialIndex = [0, 7, 0, 0, -1],
            InstanceTransformIndex = [0, 0, 5, 0, 0],
        };
        Assert.That(geometry.InstanceFlags, Is.Empty, "the default this test is about");
        return new BimData
        {
            Geometry = geometry,
            Strings = ["g0", "g1", "g2", "g3", "g4"],
            Entities = Enumerable.Range(0, 5)
                .Select(e => new Entity(e, (StringIndex)e, (DocumentIndex)0, (StringIndex)e, (EntityIndex)(-1), (EntityIndex)(-1)))
                .ToArray(),
        };
    }

    [Test]
    public void Bad_indices_are_skipped_or_defaulted_and_counted()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "hand-built.glb");
        var summary = Model().WriteGlb(path);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Nodes, Is.EqualTo(3), "entities 0, 1, and 4");
            Assert.That(summary.SkippedEmpty, Is.EqualTo(1), "entity 3: mesh 4");
            Assert.That(summary.SkippedBadTransform, Is.EqualTo(1), "entity 2: transform 5");
            Assert.That(summary.DefaultedMaterials, Is.EqualTo(1), "entity 1: material 7; -1 is not counted");
            Assert.That(summary.Materials, Is.EqualTo(2), "red and the default, shared by entities 1 and 4");
            Assert.That(summary.Meshes, Is.EqualTo(2));
            Assert.That(summary.Triangles, Is.EqualTo(3));
        });

        var nodes = ModelRoot.Load(path).DefaultScene.VisualChildren.ToList();
        Assert.That(nodes.Select(n => (int)n.Extras![BosGlb.EntityIndexKey]!), Is.EqualTo(new[] { 0, 1, 4 }));
        Assert.That(nodes[1].Mesh, Is.SameAs(nodes[2].Mesh), "a bad material index and no material both draw with the default");
    }

    [Test]
    public void Entities_whose_only_instances_are_bad_are_unmatched()
    {
        var summary = Model().ToGltf(new GlbExportOptions { EntityIndices = [1, 2, 3] }).Summary;
        Assert.That(summary.Nodes, Is.EqualTo(1));
        Assert.That(summary.UnmatchedEntityIndices, Is.EqualTo(2), "entities 2 and 3");
    }
}
