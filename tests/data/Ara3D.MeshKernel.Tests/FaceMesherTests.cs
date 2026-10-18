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

    /// <summary>The boundary is densified, so the patch has more than its four corners; earcut adds
    /// no interior points, so interior triangles are chords and the mesh's area is not the surface's.</summary>
    [Test]
    public void AddFace_CylinderQuarterPatch_IsDensifiedOntoTheSurfaceAndFacesOutward()
    {
        const float radius = 2f;
        var map = new CylinderMap(new Frame3D(Point3D.Zero, OrthonormalBasis3D.Identity), radius);
        Vector3 At(float angle, float z) => V(radius * MathF.Cos(angle), radius * MathF.Sin(angle), z);
        IReadOnlyList<Vector3> ring = [At(0, 0), At(MathF.PI / 2, 0), At(MathF.PI / 2, 1), At(0, 1)];

        var builder = new WeldedMeshBuilder();
        Assert.That(builder.AddFace(map, ring, [], sameSense: true), Is.Null);
        var mesh = builder.ToMesh();

        Assert.That(mesh.Points.Count, Is.GreaterThan(20));
        Assert.That(mesh.FaceIndices.Count, Is.EqualTo(mesh.Points.Count - 2));
        foreach (var p in mesh.Points)
        {
            var r = MathF.Sqrt(p.X.Value * p.X.Value + p.Y.Value * p.Y.Value);
            Assert.That(r, Is.EqualTo(radius).Within(1e-4f));
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
