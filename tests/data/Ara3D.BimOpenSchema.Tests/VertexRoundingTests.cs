using Ara3D.Geometry;
using Ara3D.Models;

namespace Ara3D.BimOpenSchema.Tests;

/// <summary>
/// BimGeometryBuilder stores each vertex coordinate as an int in units of 0.1 mm.
/// The conversion must round to the nearest unit; truncating toward zero shrinks every shape toward the origin.
/// </summary>
[TestFixture]
public sealed class VertexRoundingTests
{
    [TestCase(0.12345f, 1235)]
    [TestCase(-0.12345f, -1235)]
    [TestCase(0.12344f, 1234)]
    [TestCase(-0.12344f, -1234)]
    [TestCase(0f, 0)]
    public void Coordinate_RoundsToNearestTenthOfMillimetre(float metres, int expectedUnits)
    {
        var geometry = Build(Triangle(metres));

        Assert.That(geometry.VertexX[1], Is.EqualTo(expectedUnits));
        Assert.That(geometry.VertexY[2], Is.EqualTo(expectedUnits));
        Assert.That(geometry.VertexZ[0], Is.EqualTo(expectedUnits));
    }

    [Test]
    public void CoordinateBeyondOneAndAHalfKilometres_IsNotLostToFloatArithmetic()
    {
        // 2048.0625 m is exact in a float and is 20_480_625 units. That is odd and above 16_777_216,
        // where a float product can only hold even integers, so float arithmetic would give ...624 or ...626.
        var geometry = Build(Triangle(2048.0625f));

        Assert.That(geometry.VertexX[1], Is.EqualTo(20_480_625));
    }

    [Test]
    public void CoordinateBeyondIntRange_SaturatesInsteadOfWrapping()
    {
        var geometry = Build(Triangle(300_000f));

        Assert.That(geometry.VertexX[1], Is.EqualTo(int.MaxValue));
        Assert.That(Build(Triangle(-300_000f)).VertexX[1], Is.EqualTo(int.MinValue));
    }

    [Test]
    public void MeshWithNaNVertex_IsLeftEmptyAndCounted_WithoutDisturbingItsNeighbours()
    {
        var builder = new BimGeometryBuilder();
        builder.AddMesh(Triangle(1f));
        builder.AddMesh(Triangle(float.NaN));
        builder.AddMesh(Triangle(float.PositiveInfinity));
        builder.AddMesh(Triangle(2f));
        for (var i = 0; i < 4; i++)
            builder.AddInstance(i, 0, i, 0, 0);

        var geometry = builder.BuildModel();

        Assert.That(builder.NonFiniteMeshCount, Is.EqualTo(2));
        Assert.That(geometry.MeshVertexOffset, Is.EqualTo(new[] { 0, 3, 3, 3 }));
        Assert.That(geometry.MeshIndexOffset, Is.EqualTo(new[] { 0, 3, 3, 3 }));
        Assert.That(geometry.VertexX, Is.EqualTo(new[] { 10_000, 10_000, 0, 20_000, 20_000, 0 }));
        Assert.That(geometry.InstanceMeshIndex, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        Assert.That(geometry.IndexBuffer, Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2 }));
    }

    [Test]
    public void CoordinatesBeyondIntRange_AreCounted()
    {
        var builder = new BimGeometryBuilder();
        builder.AddMesh(Triangle(300_000f));
        builder.AddMesh(Triangle(1f));

        builder.BuildModel();

        // Triangle(v) has five nonzero coordinates: (v, v, v), (v, 0, 0), (0, v, 0).
        Assert.That(builder.ClampedCoordinateCount, Is.EqualTo(5));
        Assert.That(builder.NonFiniteMeshCount, Is.EqualTo(0));
        builder.BuildModel();
        Assert.That(builder.ClampedCoordinateCount, Is.EqualTo(5), "counts describe the last build, not a running total");
    }

    [Test]
    public void EveryVertexOfATube_IsWithinHalfAUnitOfItsSource()
    {
        // Rounding to the nearest 0.1 mm unit moves each coordinate by at most 0.05 mm, plus the float error of
        // the source (a float near 0.5 m is exact to about 6e-8 m, far below a unit).
        var tube = Tube(radius: 0.05f, length: 1f, segments: 256);
        var rounded = Build(tube).GetMesh(0);

        Assert.That(rounded.Points.Count, Is.EqualTo(tube.Points.Count));
        for (var i = 0; i < tube.Points.Count; i++)
        {
            Assert.That((double)rounded.Points[i].X, Is.EqualTo((double)tube.Points[i].X).Within(5.01e-5));
            Assert.That((double)rounded.Points[i].Y, Is.EqualTo((double)tube.Points[i].Y).Within(5.01e-5));
            Assert.That((double)rounded.Points[i].Z, Is.EqualTo((double)tube.Points[i].Z).Within(5.01e-5));
        }
    }

    [Test]
    public void FiveCentimetreTube_VolumeErrorStaysWithinTheWorstCaseBound()
    {
        // Guarantee. Each vertex moves by at most 0.05 mm per axis, so at most sqrt(3) * 0.05 mm = 0.087 mm in
        // any direction. To first order the volume changes by (surface area x normal displacement) at most.
        // Area = 2 pi r L + 2 pi r^2 = 0.330 m^2, so |dV| <= 0.330 * 8.7e-5 = 2.9e-5 m^3 against
        // V = pi r^2 L = 7.85e-3 m^3: 0.37 %. Use 0.4 %. This holds for any segment count.
        var tube = Tube(radius: 0.05f, length: 1f, segments: 64);

        Assert.That(RelativeVolumeError(tube), Is.LessThan(4e-3));
    }

    [Test]
    public void FiveCentimetreTube_RoundingDoesNotShrinkItTowardTheOrigin()
    {
        // Observation, not a guarantee. Rounding errors are random and unbiased, so they cancel as 1/sqrt(vertices):
        // the standard deviation of one normal displacement is about 0.03 mm, so at 4096 segments (8194 vertices)
        // the mean is about 3e-7 m and the volume error about 1e-5 (0.001 %). The bound 1e-4 (0.01 %) is
        // 10 standard deviations. Truncation toward the origin loses 0.26 % at any segment count.
        var tube = Tube(radius: 0.05f, length: 1f, segments: 4096);

        Assert.That(RelativeVolumeError(tube), Is.LessThan(1e-4));
    }

    static double RelativeVolumeError(TriangleMesh3D tube)
    {
        var expected = SignedVolume(tube.Points, tube.FaceIndices);
        var roundTripped = Build(tube).GetMesh(0);
        var actual = SignedVolume(roundTripped.Points, roundTripped.FaceIndices);
        Assert.That(expected, Is.GreaterThan(0.0));
        return Math.Abs(actual - expected) / expected;
    }

    static BimGeometry Build(TriangleMesh3D mesh)
    {
        var builder = new BimGeometryBuilder();
        builder.AddMesh(mesh);
        return builder.BuildModel();
    }

    // Three vertices (0 = (v, v, v), 1 = (v, 0, 0), 2 = (0, v, 0)); the tests read X of 1, Y of 2, and Z of 0.
    static TriangleMesh3D Triangle(float v)
        => new(
            new List<Point3D> { new(v, v, v), new(v, 0, 0), new(0, v, 0) },
            new List<Integer3> { new(0, 1, 2) });

    // A closed cylinder along Z centred on the origin, so truncation toward zero would shrink it on every axis.
    static TriangleMesh3D Tube(float radius, float length, int segments)
    {
        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        var half = length / 2;
        for (var i = 0; i < segments; i++)
        {
            var angle = 2 * Math.PI * i / segments;
            var x = (float)(radius * Math.Cos(angle));
            var y = (float)(radius * Math.Sin(angle));
            points.Add(new Point3D(x, y, -half));
            points.Add(new Point3D(x, y, half));
        }

        var bottomCentre = points.Count;
        points.Add(new Point3D(0, 0, -half));
        var topCentre = points.Count;
        points.Add(new Point3D(0, 0, half));

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            int b0 = 2 * i, t0 = 2 * i + 1, b1 = 2 * next, t1 = 2 * next + 1;
            faces.Add(new Integer3(b0, b1, t1));
            faces.Add(new Integer3(b0, t1, t0));
            faces.Add(new Integer3(bottomCentre, b1, b0));
            faces.Add(new Integer3(topCentre, t0, t1));
        }

        return new TriangleMesh3D(points, faces);
    }

    static double SignedVolume(IReadOnlyList<Point3D> points, IReadOnlyList<Integer3> faces)
    {
        double volume = 0;
        foreach (var f in faces)
        {
            var a = points[f.A];
            var b = points[f.B];
            var c = points[f.C];
            volume += (double)a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y)
                    - (double)a.Y * ((double)b.X * c.Z - (double)b.Z * c.X)
                    + (double)a.Z * ((double)b.X * c.Y - (double)b.Y * c.X);
        }

        return volume / 6.0;
    }
}
