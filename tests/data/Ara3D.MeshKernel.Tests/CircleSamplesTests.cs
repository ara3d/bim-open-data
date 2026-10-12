using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class CircleSamplesTests
{
    static void AssertNear(Vector2 actual, float x, float y)
    {
        Assert.That(actual.X.Value, Is.EqualTo(x).Within(1e-6f), $"{actual}");
        Assert.That(actual.Y.Value, Is.EqualTo(y).Within(1e-6f), $"{actual}");
    }

    [Test]
    public void Ring_RadiusOneFourSegments_IsTheFourAxisPoints()
    {
        var ring = CircleSamples.Ring(1, 4);
        Assert.That(ring, Has.Count.EqualTo(4));
        AssertNear(ring[0], 1, 0);
        AssertNear(ring[1], 0, 1);
        AssertNear(ring[2], -1, 0);
        AssertNear(ring[3], 0, -1);
    }

    [Test]
    public void Ring_RadiusTwo32Segments_AllPointsAtDistanceTwo()
    {
        var ring = CircleSamples.Ring(2, 32);
        Assert.That(ring, Has.Count.EqualTo(32));
        foreach (var p in ring)
            Assert.That(MathF.Sqrt(p.X.Value * p.X.Value + p.Y.Value * p.Y.Value), Is.EqualTo(2f).Within(1e-5f));
    }

    [Test]
    public void Ring_ZeroSegments_IsEmpty()
        => Assert.That(CircleSamples.Ring(1, 0), Is.Empty);

    [Test]
    public void Arc_QuarterTurnAboutOneOne_FivePointsFromRightToTop()
    {
        var arc = CircleSamples.Arc(new Vector2(1, 1), 1, 0, MathF.PI / 2, 4);
        Assert.That(arc, Has.Count.EqualTo(5));
        AssertNear(arc[0], 2, 1);
        AssertNear(arc[4], 1, 2);
    }
}
