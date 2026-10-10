using System.Numerics;
using Ara3D.BimOpenSchema.IO.Fragments.Schema;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>Geometry read from hand-built models: shells with holes and concave outlines, big
/// shells, circle extrusions, and the placement of a sample in BOS's z-up world.</summary>
public class GeometryTests
{
    private const float Tolerance = 2e-4f;

    /// <summary>A 4 by 4 square in the local x-y plane, wound counter-clockwise seen from +z,
    /// with a 2 by 2 square hole in its middle.</summary>
    private static TestShell SquareWithHole(bool big = false) => new(
        [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0), new(1, 1, 0), new(1, 3, 0), new(3, 3, 0), new(3, 1, 0)],
        [[0, 1, 2, 3]],
        [(0, [4, 5, 6, 7])],
        big);

    /// <summary>One wall item whose single sample places <paramref name="shell"/> or <paramref name="tube"/>.</summary>
    private static TestModel OneElement(TestShell? shell = null, TestTube? tube = null, TestTransform? global = null)
    {
        var model = new TestModel { Items = { new(7, "IFCWALL", "wall-guid", []) } };
        if (shell != null)
        {
            model.Shells.Add(shell);
            model.Representations.Add((RepresentationClass.SHELL, 0));
        }
        if (tube != null)
        {
            model.Tubes.Add(tube);
            model.Representations.Add((RepresentationClass.CIRCLE_EXTRUSION, 0));
        }
        model.MeshesItems.Add(0);
        model.GlobalTransforms.Add(global ?? TestTransform.Identity);
        model.Samples.Add(new TestSample(Item: 0, Representation: 0));
        return model;
    }

    private static BimGeometry Geometry(TestModel model)
    {
        var d = FragmentsToBos.Read(model.ToBytes(), "test", "test.frag");
        Assert.That(d.Geometry.InstanceEntityIndex, Has.Length.EqualTo(1));
        Assert.That(d.Category(d.Geometry.InstanceEntityIndex[0]), Is.EqualTo("IFCWALL"));
        return d.Geometry;
    }

    private static int TriangleCount(BimGeometry g) => g.Triangles(0).Count();

    [TestCase(false)]
    [TestCase(true)]
    public void ShellWithHole_CoversTheSquareLessTheHole(bool big)
    {
        var g = Geometry(OneElement(SquareWithHole(big)));
        Assert.Multiple(() =>
        {
            Assert.That(TriangleCount(g), Is.EqualTo(8), "4 outer + 4 hole points, bridged: n - 2 + 2 per hole");
            Assert.That(g.Area(0), Is.EqualTo(16 - 4).Within(Tolerance));
        });
    }

    [Test]
    public void ShellTriangles_FaceTheWayTheProfileIsWound()
    {
        // web-ifc places an IFC element (z-up) in its y-up world with x along x and y along -z, as
        // the Duplex's global transforms do; the profile's normal, local +z, then ends up BOS +z.
        var ifcInYUp = new TestTransform(Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ);
        var g = Geometry(OneElement(SquareWithHole(), global: ifcInYUp));
        Assert.That(g.Triangles(0).Select(t => Vector3.Cross(t.B - t.A, t.C - t.A).Z), Is.All.GreaterThan(0));
    }

    [Test]
    public void ConcaveProfile_IsTriangulatedInsideItsOutline()
    {
        // An L: a 2 by 2 square missing its top-right 1 by 1 corner, area 3, in the x-z plane.
        var l = new TestShell(
            [new(0, 0, 0), new(2, 0, 0), new(2, 0, 1), new(1, 0, 1), new(1, 0, 2), new(0, 0, 2)],
            [[0, 1, 2, 3, 4, 5]]);
        var g = Geometry(OneElement(l));
        Assert.Multiple(() =>
        {
            Assert.That(TriangleCount(g), Is.EqualTo(4));
            Assert.That(g.Area(0), Is.EqualTo(3).Within(Tolerance));
        });
    }

    [Test]
    public void ClosedShell_EnclosesPositiveVolume()
    {
        // A unit cube, each face wound counter-clockwise seen from outside.
        var cube = new TestShell(
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
            [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]);
        var g = Geometry(OneElement(cube, global: TestTransform.At(5, 6, 7)));
        Assert.Multiple(() =>
        {
            Assert.That(TriangleCount(g), Is.EqualTo(12));
            Assert.That(g.SignedVolume(0), Is.EqualTo(1).Within(Tolerance));
        });
    }

    [Test]
    public void ProfileIndexingPastThePoints_IsLeftOutAndReported()
    {
        var shell = new TestShell([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2], [0, 1, 9]]);
        var model = OneElement(shell);
        var d = FragmentsToBos.Read(model.ToBytes(), "test", "test.frag");
        Assert.Multiple(() =>
        {
            Assert.That(d.Geometry.Triangles(0).Count(), Is.EqualTo(1));
            Assert.That(d.Diagnostics.Select(x => d.Strings[(int)x.Message]), Has.Some.StartsWith("1 shell profiles left out"));
        });
    }

    [Test]
    public void Sample_IsPlacedInZUpWorldWithTheCoordinatesMoveUndone()
    {
        // In That Open's y-up world the element's origin is at (1, 2, 3) after the model was moved
        // by (10, 20, 30); the original y-up point (-9, -18, -27) is (-9, 27, -18) in z-up.
        var model = OneElement(new TestShell([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]), global: TestTransform.At(1, 2, 3));
        model.Coordinates = TestTransform.At(10, 20, 30);
        var g = Geometry(model);
        var origin = g.Triangles(0).Single().A;
        Assert.That(Vector3.Distance(origin, new Vector3(-9, 27, -18)), Is.LessThan(Tolerance), origin.ToString());
    }

    [Test]
    public void Sample_ComposesLocalThenGlobalTransform()
    {
        // The local transform turns x onto y (a quarter turn about z) and moves by 1 along x; the
        // global moves by (0, 0, 5) in y-up, which is (0, -5, 0) in z-up.
        var model = OneElement(new TestShell([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]), global: TestTransform.At(0, 0, 5));
        model.LocalTransforms[0] = new TestTransform(new Vector3(1, 0, 0), Vector3.UnitY, -Vector3.UnitX);
        var g = Geometry(model);
        var (a, b, _) = g.Triangles(0).Single();
        Assert.Multiple(() =>
        {
            Assert.That(Vector3.Distance(a, new Vector3(1, -5, 0)), Is.LessThan(Tolerance), a.ToString());
            Assert.That(Vector3.Distance(b, new Vector3(1, -5, 1)), Is.LessThan(Tolerance), b.ToString());
        });
    }

    [Test]
    public void StraightCircleExtrusion_IsAClosedTubeOfItsRadius()
    {
        // Radius 0.05 gives a ring of round(200 r) = 10 points; one wire 1 m long.
        const float r = 0.05f;
        var g = Geometry(OneElement(tube: new TestTube(r, [(Vector3.Zero, new Vector3(0, 0, 1))])));
        var ringArea = 0.5 * 10 * r * r * Math.Sin(2 * Math.PI / 10);
        Assert.Multiple(() =>
        {
            Assert.That(TriangleCount(g), Is.EqualTo(2 * 10 + 2 * (10 - 2)), "sides plus two capping fans");
            // BOS stores vertices in 0.1 mm steps, cut toward zero: a 5 cm radius loses up to 0.2 %.
            Assert.That(g.SignedVolume(0), Is.EqualTo(ringArea * 1).Within(0.5).Percent, "closed, faces outward");
        });
    }

    [Test]
    public void ArcCircleExtrusion_FollowsTheArc()
    {
        // A quarter turn of radius 1 about the y-up axis (y), from +x to -z in y-up, which is from
        // +x to +y in z-up: round(4 * pi/2 * 1) = 6 rings, each of 6 points (the minimum).
        const float r = 0.02f;
        var arc = new TestArc(MathF.PI / 2, Vector3.Zero, 1, Vector3.UnitY, Vector3.UnitX);
        var g = Geometry(OneElement(tube: new TestTube(r, [], [arc])));
        var points = g.Triangles(0).SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
        var min = points.Aggregate(Vector3.Min);
        var max = points.Aggregate(Vector3.Max);
        Assert.Multiple(() =>
        {
            Assert.That(TriangleCount(g), Is.EqualTo(2 * 6 * 5 * 1 + 2 * (6 - 2)), "6-point rings, 5 spans, two caps");
            Assert.That(max.X, Is.InRange(1, 1 + r + 1e-4));
            Assert.That(max.Y, Is.InRange(1, 1 + r + 1e-4));
            Assert.That(min.X, Is.InRange(-r - 1e-4, 0));
            Assert.That(min.Y, Is.InRange(-r - 1e-4, 0));
            Assert.That(max.Z - min.Z, Is.InRange(2 * r * MathF.Cos(MathF.PI / 6) - 1e-4, 2 * r + 1e-4), "a hexagonal ring across the arc's plane");
        });
    }
}
