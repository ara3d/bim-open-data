using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class ExtrusionTests
{
    [Test]
    public void UnitBox_HasEightPointsTwelveTrianglesVolumeOne()
    {
        var box = TestShapes.Box();
        Assert.That(box.Points, Has.Count.EqualTo(8));
        Assert.That(box.FaceIndices, Has.Count.EqualTo(12));
        Assert.That(MeshMeasure.SignedVolume(box), Is.EqualTo(1).Within(1e-6));
        var bounds = MeshMeasure.Bounds(box);
        Assert.That(bounds.Min.Z.Value, Is.EqualTo(0).Within(1e-6));
        Assert.That(bounds.Max.X.Value, Is.EqualTo(1).Within(1e-6));
        Assert.That(bounds.Max.Y.Value, Is.EqualTo(1).Within(1e-6));
        Assert.That(bounds.Max.Z.Value, Is.EqualTo(1).Within(1e-6));
    }

    [Test]
    public void SquareWithHole_IsClosedWithVolumeThree()
    {
        var box = TestShapes.Box(TestShapes.SquareWithHole());
        Assert.That(box.Points, Has.Count.EqualTo(16));
        Assert.That(box.FaceIndices, Has.Count.EqualTo(32));
        Assert.That(MeshMeasure.SignedVolume(box), Is.EqualTo(3).Within(1e-5));
        var edgeUses = new Dictionary<(int, int), int>();
        foreach (var f in box.FaceIndices)
            foreach (var (a, b) in new[] { (f.A, f.B), (f.B, f.C), (f.C, f.A) })
            {
                var key = a < b ? (a, b) : (b, a);
                edgeUses[key] = edgeUses.GetValueOrDefault(key) + 1;
            }
        Assert.That(edgeUses.Values, Has.All.EqualTo(2));
    }

    [Test]
    public void Ribbon_OfThreePoints_HasSixPointsFourTriangles()
    {
        Point3D[] row = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)];
        var ribbon = Extrusion.Ribbon(row, new Vector3(0, 0, 1));
        Assert.That(ribbon.Points, Has.Count.EqualTo(6));
        Assert.That(ribbon.FaceIndices, Has.Count.EqualTo(4));
    }
}
