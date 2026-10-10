using System.Numerics;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

[TestFixture]
public sealed class BcfCameraTests
{
    private static readonly ElementBox Box = new(new Vector3(10, 20, 0), new Vector3(14, 22, 3));

    [Test]
    public void TheCameraLooksAtTheBoxCentreFromAbove()
    {
        var camera = BcfCamera.Frame(Box, 60, 1);
        var toCentre = Vector3.Normalize(Box.Center - camera.ViewPoint);
        Assert.Multiple(() =>
        {
            Assert.That(Vector3.Distance(toCentre, camera.Direction), Is.LessThan(1e-5f), "aimed at the centre");
            Assert.That(camera.ViewPoint.Z, Is.GreaterThan(Box.Max.Z), "above the box");
            Assert.That(Math.Abs(Vector3.Dot(camera.Up, camera.Direction)), Is.LessThan(1e-6f), "up is perpendicular to the view");
            Assert.That(camera.Up.Z, Is.GreaterThan(0), "up points up");
        });
    }

    /// <summary>A narrow view (aspect below 1) is limited by its horizontal angle, so the camera
    /// stands further back than for a square one.</summary>
    [TestCase(1.0)]
    [TestCase(0.5)]
    public void EveryCornerIsInsideTheNarrowerFieldOfView(double aspect)
    {
        var camera = BcfCamera.Frame(Box, 60, aspect);
        var halfVertical = 30 * Math.PI / 180;
        var halfNarrowest = Math.Min(halfVertical, Math.Atan(Math.Tan(halfVertical) * aspect));
        foreach (var corner in DuplexBcfTests.Corners(Box))
        {
            var angle = Math.Acos(Vector3.Dot(Vector3.Normalize(corner - camera.ViewPoint), camera.Direction));
            Assert.That(angle, Is.LessThanOrEqualTo(halfNarrowest + 1e-5), $"corner {corner}");
        }
    }

    [Test]
    public void APointLikeBoxIsFramedFromTheMinimumRadius()
    {
        var point = new ElementBox(Vector3.One, Vector3.One);
        var camera = BcfCamera.Frame(point, 60, 1);
        var expected = BcfCamera.MinimumRadius / Math.Sin(30 * Math.PI / 180);
        Assert.That(Vector3.Distance(camera.ViewPoint, Vector3.One), Is.EqualTo(expected).Within(1e-5));
    }
}
