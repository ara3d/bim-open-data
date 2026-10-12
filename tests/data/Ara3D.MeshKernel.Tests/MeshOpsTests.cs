using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class MeshOpsTests
{
    [Test]
    public void MirroredBox_HasNegativeDeterminantAndStillPositiveVolume()
    {
        var mirror = Matrix4x4.CreateScale(-1, 1, 1);
        Assert.That(MeshOps.LinearDeterminant(mirror), Is.EqualTo(-1).Within(1e-6));
        var mirrored = MeshOps.Transform(TestShapes.Box(), mirror);
        Assert.That(MeshMeasure.SignedVolume(mirrored), Is.EqualTo(1).Within(1e-6));
    }

    [Test]
    public void MergeOfTwoBoxes_AddsPointsFacesAndVolume()
    {
        var box = TestShapes.Box();
        var merged = MeshOps.Merge([box, MeshOps.Transform(box, Matrix4x4.CreateTranslation(new Vector3(5, 0, 0)))]);
        Assert.That(merged.Points, Has.Count.EqualTo(16));
        Assert.That(merged.FaceIndices, Has.Count.EqualTo(24));
        Assert.That(MeshMeasure.SignedVolume(merged), Is.EqualTo(2).Within(1e-6));
    }

    [Test]
    public void BoundsOfEmptyMesh_IsEmpty()
        => Assert.That(MeshMeasure.Bounds(new TriangleMesh3D([], [])), Is.EqualTo(Bounds3D.Empty));
}
