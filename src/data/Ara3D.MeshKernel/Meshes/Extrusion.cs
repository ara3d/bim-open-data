using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>Prisms and ribbons made by moving a profile along a vector.</summary>
public static class Extrusion
{
    /// <summary>Closed prism: caps from the profile's triangulation plus a wall per ring edge.</summary>
    public static TriangleMesh3D Solid(
        PolygonWithHoles profile,
        Frame3D frame,
        Vector3 extrusion)
    {
        // Offset-ring triangulation resamples a single hole to match the outer vertex count.
        // Wall loops must use that same resampled ring or hole seams stay open.
        profile = AlignHoleRingsForExtrusion(profile);

        var bottomByKey = new Dictionary<(int X, int Y), int>();
        var points = new List<Point3D>();

        int AddBottom(Vector2 p2)
        {
            var key = Quantize(p2);
            if (bottomByKey.TryGetValue(key, out var idx))
                return idx;
            idx = points.Count;
            points.Add(frame.ToWorld(new Vector3(p2.X, p2.Y, 0)));
            bottomByKey[key] = idx;
            return idx;
        }

        foreach (var p in profile.Outer)
            AddBottom(p);
        foreach (var hole in profile.Holes)
            foreach (var p in hole)
                AddBottom(p);

        // Cap triangulation can introduce vertices not on the profile rings (a resampled inner
        // ring). Register them before duplicating the top ring so TopIndex(bottom) always has a
        // matching top vertex.
        var capTris = profile.Triangulate();
        foreach (var tri in capTris)
        {
            AddBottom(tri.A.Vector2);
            AddBottom(tri.B.Vector2);
            AddBottom(tri.C.Vector2);
        }

        var bottomCount = points.Count;
        var topOffset = bottomCount;
        for (var i = 0; i < bottomCount; i++)
            points.Add(points[i] + extrusion);

        int TopIndex(int bottomIdx) => bottomIdx + topOffset;

        var faces = new List<Integer3>();
        foreach (var tri in capTris)
        {
            var a = AddBottom(tri.A.Vector2);
            var b = AddBottom(tri.B.Vector2);
            var c = AddBottom(tri.C.Vector2);
            faces.Add(new Integer3(c, b, a));
            faces.Add(new Integer3(TopIndex(a), TopIndex(b), TopIndex(c)));
        }

        AddWallLoop(faces, profile.Outer, AddBottom, TopIndex);
        foreach (var hole in profile.Holes)
            AddWallLoop(faces, hole, AddBottom, TopIndex);

        return new TriangleMesh3D(points, faces);
    }

    /// <summary>
    /// Mirrors <see cref="PolygonWithHoles"/> offset-ring resampling so extrusion walls share
    /// the same hole vertices as the annular cap triangulation.
    /// </summary>
    static PolygonWithHoles AlignHoleRingsForExtrusion(PolygonWithHoles profile)
    {
        if (profile.Holes.Count != 1)
            return profile;
        var hole = profile.Holes[0];
        if (hole.Count == profile.Outer.Count || hole.Count < 3 || profile.Outer.Count < 3)
            return profile;
        if (PolygonWithHoles.HasSelfIntersection(profile.Outer) ||
            PolygonWithHoles.HasSelfIntersection(hole))
            return profile;

        var resampled = PolygonWithHoles.ResampleClosedRing(hole, profile.Outer.Count);
        return new PolygonWithHoles(profile.Outer, [resampled]);
    }

    static (int X, int Y) Quantize(Vector2 p)
        => ((int)MathF.Round(p.X * 1e6f), (int)MathF.Round(p.Y * 1e6f));

    static void AddWallLoop(
        List<Integer3> faces,
        IReadOnlyList<Vector2> loop,
        Func<Vector2, int> bottomIndex,
        Func<int, int> topIndex,
        bool reverse = false)
    {
        var n = loop.Count;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var bi = bottomIndex(loop[i]);
            var bj = bottomIndex(loop[j]);
            var ti = topIndex(bi);
            var tj = topIndex(bj);
            if (!reverse)
            {
                faces.Add(new Integer3(bi, bj, tj));
                faces.Add(new Integer3(bi, tj, ti));
            }
            else
            {
                faces.Add(new Integer3(bj, bi, ti));
                faces.Add(new Integer3(bj, ti, tj));
            }
        }
    }

    /// <summary>Extrudes a polyline into a single-sided ribbon (surface, not solid; no end caps).</summary>
    public static TriangleMesh3D Ribbon(IReadOnlyList<Point3D> bottomRow, Vector3 extrusion)
    {
        if (bottomRow.Count < 2)
            return new TriangleMesh3D([], []);

        var points = new List<Point3D>(bottomRow.Count * 2);
        foreach (var bottom in bottomRow)
        {
            points.Add(bottom);
            points.Add(bottom + extrusion);
        }

        var faces = new List<Integer3>();
        for (var i = 0; i < bottomRow.Count - 1; i++)
        {
            var b0 = i * 2;
            var b1 = (i + 1) * 2;
            var t0 = b0 + 1;
            var t1 = b1 + 1;
            faces.Add(new Integer3(b0, b1, t1));
            faces.Add(new Integer3(b0, t1, t0));
        }
        return new TriangleMesh3D(points, faces);
    }
}
