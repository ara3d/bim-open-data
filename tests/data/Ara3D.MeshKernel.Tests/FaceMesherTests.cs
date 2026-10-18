using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

public sealed class FaceMesherTests
{
    static Vector3 V(float x, float y, float z) => new(x, y, z);

    static IReadOnlyList<Vector3> Corners(TriangleMesh3D mesh, Integer3 f)
        => [mesh.Points[f.A].Vector3, mesh.Points[f.B].Vector3, mesh.Points[f.C].Vector3];

    static Vector3 Normal(IReadOnlyList<Vector3> t)
        => Vector3.Cross(t[1] - t[0], t[2] - t[0]);

    static float Area(TriangleMesh3D mesh)
        => mesh.FaceIndices.Sum(f => Normal(Corners(mesh, f)).Length.Value / 2);

    /// <summary>4 by 4 square at z = 1, counter-clockwise seen from +Z.</summary>
    static readonly IReadOnlyList<Vector3> SquareOuter = [V(0, 0, 1), V(4, 0, 1), V(4, 4, 1), V(0, 4, 1)];

    /// <summary>2 by 2 square hole running the same way as <see cref="SquareOuter"/>, vertex i
    /// beside outer vertex i: the ring band's case.</summary>
    static readonly IReadOnlyList<Vector3> AlignedSquareHole = [V(1, 1, 1), V(3, 1, 1), V(3, 3, 1), V(1, 3, 1)];

    /// <summary>The same hole running clockwise, against <see cref="SquareOuter"/>: a band joining
    /// vertex i to vertex i twists and covers 24, twice the face (TKT-308), so earcut must take it.</summary>
    static readonly IReadOnlyList<Vector3> ClockwiseSquareHole = [V(1, 1, 1), V(1, 3, 1), V(3, 3, 1), V(3, 1, 1)];

    /// <summary>Clockwise triangular hole of area 2: earcut's case.</summary>
    static readonly IReadOnlyList<Vector3> TriangleHole = [V(1, 1, 1), V(1, 3, 1), V(3, 1, 1)];

    static TriangleMesh3D MeshPlanar(IReadOnlyList<Vector3> hole, bool sameSense)
    {
        var mesh = new WeldedMeshBuilder();
        var drop = mesh.AddFace(new PlanarMap(FacePlane.OfRing(SquareOuter)), SquareOuter, [hole], sameSense);
        Assert.That(drop, Is.Null);
        return mesh.ToMesh();
    }

    static void AssertPlanarFaceWithHole(TriangleMesh3D mesh, int corners, float area, float normalSign)
    {
        Assert.That(mesh.Points.Count, Is.EqualTo(corners));
        Assert.That(Area(mesh), Is.EqualTo(area).Within(1e-4f));
        foreach (var p in mesh.Points)
            Assert.That(p.Z.Value, Is.EqualTo(1f));
        foreach (var f in mesh.FaceIndices)
            Assert.That(Normal(Corners(mesh, f)).Z.Value * normalSign, Is.GreaterThan(0f));
    }

    [Test]
    public void AddFace_PlanarQuadWithQuadHole_BandCoversTheRingAndWeldsItsCorners()
        => AssertPlanarFaceWithHole(MeshPlanar(AlignedSquareHole, sameSense: true), 8, 12f, 1f);

    [Test]
    public void AddFace_PlanarQuadWithClockwiseQuadHole_CoversTheRingOnceFacingOneWay()
        => AssertPlanarFaceWithHole(MeshPlanar(ClockwiseSquareHole, sameSense: true), 8, 12f, 1f);

    [Test]
    public void AddFace_PlanarQuadWithTriangleHole_EarcutCoversTheRingAndWeldsItsCorners()
        => AssertPlanarFaceWithHole(MeshPlanar(TriangleHole, sameSense: true), 7, 14f, 1f);

    [Test]
    public void AddFace_SameSenseFalse_ReversesEveryTriangle()
        => AssertPlanarFaceWithHole(MeshPlanar(TriangleHole, sameSense: false), 7, 14f, -1f);

    const float Radius = 2f;

    static Vector3 OnCylinder(float angle, float z) => V(Radius * MathF.Cos(angle), Radius * MathF.Sin(angle), z);

    /// <summary>Quarter of a cylinder of radius 2 around Z, height 1, given by its four corners: its area is pi.</summary>
    static TriangleMesh3D MeshQuarterCylinder(float maxDeviation)
    {
        var map = new CylinderMap(new Frame3D(Point3D.Zero, OrthonormalBasis3D.Identity), Radius);
        IReadOnlyList<Vector3> ring = [OnCylinder(0, 0), OnCylinder(MathF.PI / 2, 0), OnCylinder(MathF.PI / 2, 1), OnCylinder(0, 1)];
        var builder = new WeldedMeshBuilder();
        Assert.That(builder.AddFace(map, ring, [], sameSense: true, maxDeviation), Is.Null);
        return builder.ToMesh();
    }

    static float DistanceFromCylinder(Vector3 p)
        => MathF.Abs(MathF.Sqrt(p.X.Value * p.X.Value + p.Y.Value * p.Y.Value) - Radius);

    static float MaxDeviation(TriangleMesh3D mesh)
        => MaxOverTriangles(mesh, DistanceFromCylinder);

    /// <summary>The largest <paramref name="distance"/> over a grid of 66 points on each triangle.</summary>
    static float MaxOverTriangles(TriangleMesh3D mesh, Func<Vector3, float> distance)
    {
        const int n = 10;
        var worst = 0f;
        foreach (var f in mesh.FaceIndices)
        {
            var t = Corners(mesh, f);
            for (var i = 0; i <= n; i++)
                for (var j = 0; i + j <= n; j++)
                    worst = MathF.Max(worst, distance(t[0] + (t[1] - t[0]) * (i / (float)n) + (t[2] - t[0]) * (j / (float)n)));
        }
        return worst;
    }

    const float SphereTop = MathF.PI / 3;
    static readonly float SpherePatchArea = MathF.PI / 2 * Radius * Radius * MathF.Sin(SphereTop);

    static Vector3 OnSphere(float angle, float latitude)
        => V(Radius * MathF.Cos(latitude) * MathF.Cos(angle), Radius * MathF.Cos(latitude) * MathF.Sin(angle), Radius * MathF.Sin(latitude));

    /// <summary>A quarter of the sphere of radius 2 between the equator and latitude 60 degrees, as a
    /// revolution of a meridian sampled at 257 points (meridian sag under 1e-5).</summary>
    static TriangleMesh3D MeshSpherePatch(float maxDeviation)
    {
        var meridian = Enumerable.Range(0, 257).Select(i => OnSphere(0, MathF.PI / 2 * i / 256)).ToList();
        var map = RevolutionMap.Build(new Ray3D(Point3D.Zero, Vector3.UnitZ), meridian)!;
        IReadOnlyList<Vector3> ring = [OnSphere(0, 0), OnSphere(MathF.PI / 2, 0), OnSphere(MathF.PI / 2, SphereTop), OnSphere(0, SphereTop)];
        var builder = new WeldedMeshBuilder();
        Assert.That(builder.AddFace(map, ring, [], sameSense: true, maxDeviation), Is.Null);
        return builder.ToMesh();
    }

    static float MaxSphereDeviation(TriangleMesh3D mesh)
        => MaxOverTriangles(mesh, p => MathF.Abs(p.Length.Value - Radius));

    /// <summary>Every corner lies on the surface, inside the quarter, and every triangle faces outward.</summary>
    [Test]
    public void AddFace_CylinderQuarterPatch_LiesOnTheSurfaceAndFacesOutward()
    {
        var mesh = MeshQuarterCylinder(Tolerance.ChordDeviation);
        foreach (var p in mesh.Points)
        {
            Assert.That(DistanceFromCylinder(p.Vector3), Is.LessThan(1e-4f));
            Assert.That(p.X.Value, Is.GreaterThan(-1e-4f));
            Assert.That(p.Y.Value, Is.GreaterThan(-1e-4f));
            Assert.That(p.Z.Value, Is.InRange(0f, 1f));
        }
        foreach (var f in mesh.FaceIndices)
        {
            var t = Corners(mesh, f);
            var centroid = (t[0] + t[1] + t[2]) / 3f;
            Assert.That(Vector3.Dot(Normal(t), V(centroid.X, centroid.Y, 0)).Value, Is.GreaterThan(0f));
        }
    }

    /// <summary>The boundary arcs are cut to 3/4 of the bound and earcut's long diagonals are
    /// flipped to join neighbouring columns, so no interior point is needed (triangles = points - 2).
    /// A strip of chords that sag s has area 1 - s / 3r of the arc's, so the area is within pi tol / r.</summary>
    [Test]
    public void AddFace_CylinderQuarterPatch_IsWithinTheBoundWithNoInteriorPoint()
    {
        const float tol = Tolerance.ChordDeviation;
        var mesh = MeshQuarterCylinder(tol);

        Assert.That(MaxDeviation(mesh), Is.LessThanOrEqualTo(tol));
        Assert.That(Area(mesh), Is.EqualTo(MathF.PI).Within(MathF.PI * tol / Radius));
        Assert.That(mesh.FaceIndices.Count, Is.EqualTo(mesh.Points.Count - 2));
    }

    /// <summary>A doubly curved patch needs interior points (triangles more than points - 2). The
    /// slack of 1e-5 is the sag of the sampled meridian the map revolves.</summary>
    [Test]
    public void AddFace_SpherePatch_GetsInteriorPointsUntilWithinTheBound()
    {
        const float tol = Tolerance.ChordDeviation;
        var mesh = MeshSpherePatch(tol);

        Assert.That(MaxSphereDeviation(mesh), Is.LessThanOrEqualTo(tol + 1e-5f));
        Assert.That(Area(mesh), Is.EqualTo(SpherePatchArea).Within(SpherePatchArea * 2 * tol / Radius));
        Assert.That(mesh.FaceIndices.Count, Is.GreaterThan(mesh.Points.Count - 2));
    }

    /// <summary>A plane is never densified or refined however small the bound (TKT-190 counts
    /// triangles against web-ifc, and flat faces are most of a model).</summary>
    [Test]
    public void AddFace_PlanarFace_KeepsItsTrianglesUnderAnyBound()
    {
        TriangleMesh3D Mesh(float maxDeviation)
        {
            var builder = new WeldedMeshBuilder();
            Assert.That(builder.AddFace(new PlanarMap(FacePlane.OfRing(SquareOuter)), SquareOuter, [TriangleHole], true, maxDeviation), Is.Null);
            return builder.ToMesh();
        }
        var unrefined = Mesh(float.MaxValue);
        var refined = Mesh(1e-9f);

        Assert.That(refined.Points, Is.EqualTo(unrefined.Points));
        Assert.That(refined.FaceIndices, Is.EqualTo(unrefined.FaceIndices));
        Assert.That(refined.FaceIndices.Count, Is.EqualTo(7));
    }

    [Test]
    public void AddFace_RingThatCollapsesToTwoPoints_IsDroppedAndAddsNothing()
    {
        var builder = new WeldedMeshBuilder();
        IReadOnlyList<Vector3> ring = [V(0, 0, 0), V(1, 0, 0), V(1, 0, 0), V(0, 0, 0)];

        var drop = builder.AddFace(new PlanarMap(FacePlane.OfRing(ring)), ring, [], sameSense: true);

        Assert.That(drop, Is.EqualTo(new FaceDrop(FaceDropKind.TooFewDistinctPoints)));
        Assert.That(builder.FaceCount, Is.EqualTo(0));
    }

    [Test]
    public void FaceDrop_Message_MatchesTheMesherDiagnostics()
    {
        Assert.That(new FaceDrop(FaceDropKind.AreaOff, 0.125).Message, Is.EqualTo("area off by 12.5 %"));
        Assert.That(new FaceDrop(FaceDropKind.TooFewBoundaryPoints).Message, Is.EqualTo("fewer than 3 boundary points"));
    }
}
