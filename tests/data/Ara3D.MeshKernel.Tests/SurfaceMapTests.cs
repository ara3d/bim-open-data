using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class SurfaceMapTests
{
    static void AssertNear(Vector3 actual, Vector3 expected, float tolerance)
        => Assert.That((actual - expected).Length.Value, Is.LessThan(tolerance), $"{actual} vs {expected}");

    [Test]
    public void FacePlane_OfRing_SquareAtZ2_HasZNormalAndRoundTrips()
    {
        Vector3[] square = [new(0, 0, 2), new(1, 0, 2), new(1, 1, 2), new(0, 1, 2)];
        var plane = FacePlane.OfRing(square);

        Assert.That(MathF.Abs(plane.Normal.Z), Is.EqualTo(1f).Within(1e-6f));
        foreach (var p in square)
            AssertNear(plane.Unproject(plane.Project(p)), p, 1e-6f);
    }

    [Test]
    public void FacePlane_OfRing_CollinearPoints_FallsBackToUnitZ()
    {
        Vector3[] line = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)];
        Assert.That(FacePlane.OfRing(line).Normal, Is.EqualTo(Vector3.UnitZ));
    }

    [Test]
    public void CylinderMap_RingAcrossTheSeam_StepsWithoutA2PiJump()
    {
        var map = new CylinderMap(new Frame3D(new Point3D(0, 0, 0), new OrthonormalBasis3D(new Axes3D(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), true)), 2f);
        Vector3 OnCylinder(float angle) => new(2f * MathF.Cos(angle), 2f * MathF.Sin(angle), 0f);
        Vector3[] ring = [OnCylinder(0.25f * MathF.PI), OnCylinder(0.75f * MathF.PI), OnCylinder(-0.75f * MathF.PI)];

        var uv = map.ProjectRing(ring);

        Assert.That((float)(uv[1].X - uv[0].X), Is.EqualTo(MathF.PI).Within(1e-4f));
        Assert.That((float)(uv[2].X - uv[1].X), Is.EqualTo(MathF.PI).Within(1e-4f));
        for (var i = 0; i < ring.Length; i++)
            AssertNear(map.Unproject(uv[i]), ring[i], 1e-4f);
    }

    [Test]
    public void RevolutionMap_Build_RoundTripsAPointOnTheSurface()
    {
        var axis = new Ray3D(new Point3D(0, 0, 0), Vector3.UnitZ);
        var map = RevolutionMap.Build(axis, [new Vector3(1, 0, 0), new Vector3(1, 0, 1)]);
        Assert.That(map, Is.Not.Null);

        var p = new Vector3(0, 1, 0.5f);
        var uv = map!.ProjectRing([p]);
        AssertNear(map.Unproject(uv[0]), p, 1e-5f);
    }

    [Test]
    public void RevolutionMap_Build_MeridianOnTheAxis_IsNull()
    {
        var axis = new Ray3D(new Point3D(0, 0, 0), Vector3.UnitZ);
        Assert.That(RevolutionMap.Build(axis, [new Vector3(0, 0, 0), new Vector3(0, 0, 1)]), Is.Null);
    }

    [Test]
    public void BSplineSurfaceMap_DegreeOneNet_CentreUnprojectsToBilinearCentre()
    {
        Vector3[][] net = [[new(0, 0, 0), new(0, 2, 0)], [new(2, 0, 0), new(2, 2, 1)]];
        float[][] weights = [[1, 1], [1, 1]];
        float[] knots = [0, 0, 1, 1];
        var map = BSplineSurfaceMap.Build(new BSplineSurface(net, weights, knots, knots, 1, 1));
        Assert.That(map, Is.Not.Null);

        var far = map!.ProjectRing([new Vector3(2, 2, 1)])[0];
        AssertNear(map.Unproject(far / 2f), new Vector3(1, 1, 0.25f), 1e-5f);
    }

    [Test]
    public void BSplineSurfaceMap_WrongKnotCount_IsNull()
    {
        Vector3[][] net = [[new(0, 0, 0), new(0, 2, 0)], [new(2, 0, 0), new(2, 2, 0)]];
        float[][] weights = [[1, 1], [1, 1]];
        float[] knots = [0, 1];
        Assert.That(BSplineSurfaceMap.Build(new BSplineSurface(net, weights, knots, knots, 1, 1)), Is.Null);
    }

    [Test]
    public void BSplineKnots_Expand_RepeatsEachKnotByItsMultiplicity()
        => Assert.That(BSplineKnots.Expand([0, 1], [2, 2]), Is.EqualTo(new[] { 0f, 0f, 1f, 1f }));
}
