using Ara3D.Geometry;
using Ara3D.Models;

namespace Ara3D.MeshKernel.Tests;

public sealed class MeshDedupTests
{
    static TriangleMesh3D Quad(float dx = 0)
        => new(
            new[] { new Point3D(dx, 0, 0), new Point3D(dx + 1, 0, 0), new Point3D(dx + 1, 1, 0), new Point3D(dx, 1, 0) },
            new[] { new Integer3(0, 1, 2), new Integer3(0, 2, 3) });

    [Test]
    public void SameQuadTwice_SameScope_GivesOneIndex()
    {
        var builder = new Model3DBuilder();
        var buckets = new Dictionary<long, List<int>>();
        var a = MeshDedup.GetOrAddMesh(builder, buckets, 0, Quad());
        var b = MeshDedup.GetOrAddMesh(builder, buckets, 0, Quad());
        Assert.That(b, Is.EqualTo(a));
        Assert.That(builder.Meshes.Count, Is.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SameQuad_DifferentScopes_GivesTwoIndices(int otherScope)
    {
        var builder = new Model3DBuilder();
        var buckets = new Dictionary<long, List<int>>();
        var a = MeshDedup.GetOrAddMesh(builder, buckets, 0, Quad());
        var b = MeshDedup.GetOrAddMesh(builder, buckets, otherScope, Quad());
        Assert.That(b, Is.Not.EqualTo(a));
        Assert.That(builder.Meshes.Count, Is.EqualTo(2));
    }

    [Test]
    public void QuadAndItsTranslate_GiveTwoIndices()
    {
        var builder = new Model3DBuilder();
        var buckets = new Dictionary<long, List<int>>();
        var a = MeshDedup.GetOrAddMesh(builder, buckets, 0, Quad());
        var b = MeshDedup.GetOrAddMesh(builder, buckets, 0, Quad(5));
        Assert.That(b, Is.Not.EqualTo(a));
    }
}
