using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class SweepRingsTests
{
    static Point3D[] Ring(float z)
        => new[] { new Point3D(0, 0, z), new Point3D(1, 0, z), new Point3D(1, 1, z), new Point3D(0, 1, z) };

    [Test]
    public void Connect_TwoFourPointRings_GivesClosedBand()
    {
        var mesh = SweepRings.Connect(Ring(0), Ring(1));
        Assert.That(mesh.Points.Count, Is.EqualTo(8));
        Assert.That(mesh.FaceIndices.Count, Is.EqualTo(8));
        var first = mesh.FaceIndices[0];
        Assert.That(new[] { (int)first.A, (int)first.B, (int)first.C }, Is.EqualTo(new[] { 0, 1, 5 }));
    }
}
