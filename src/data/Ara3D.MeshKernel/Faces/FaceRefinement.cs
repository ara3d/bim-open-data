using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Brings a face's triangles, in its surface's domain, within a deviation bound of the surface by
/// making every interior edge's sag (<see cref="SurfaceMap.Sag"/>) at most <see cref="EdgeSag"/>.
/// Triangles that already meet it, every one on a plane, come back unchanged.
/// </summary>
/// <remarks>
/// First an edge over the sag is flipped to the quad's other diagonal when that one sags less:
/// earcut joins far-apart boundary points, and on a cylinder flipping alone turns its long
/// diagonals into ones between neighbouring columns, with no new point. Edges still over are then
/// split at their domain midpoint, every such edge at once, each triangle re-triangulated by how
/// many of its edges were split, and the flips run again. Splitting before flipping would stack thin
/// triangles across the curved direction, whose area grows without bound (Schwarz's lantern).
/// Boundary edges are never flipped or split, so the face keeps the boundary points its neighbours
/// share; <see cref="RingDensification"/> holds them to the same sag.
/// </remarks>
public static class FaceRefinement
{
    /// <summary>Rounds of splitting, and the triangle count past which no round starts: a face that
    /// reaches either keeps the triangles it has, nearer the surface than before.</summary>
    public const int MaxRounds = 16;
    public const int MaxTriangles = 1 << 16;

    /// <summary>The edge sag that keeps a whole triangle within <paramref name="maxDeviation"/>. Over a
    /// locally quadratic surface a flat triangle's distance from it is the sum of l_i l_j k_ij over its
    /// edges ij (l the barycentric coordinates, k_ij four times the edge's sag), at most k_max / 3, so
    /// edges held to 3/4 of the bound hold every point of the triangle to the bound.</summary>
    public static float EdgeSag(float maxDeviation)
        => 0.75f * maxDeviation;

    public static IReadOnlyList<Triangle2D> Refine(this SurfaceMap map, IReadOnlyList<Triangle2D> tris, float maxDeviation)
    {
        var mesh = new Mesh(map, tris, EdgeSag(maxDeviation));
        var changed = mesh.Flip();
        for (var round = 0; round < MaxRounds && mesh.TriangleCount < MaxTriangles && mesh.Split(); round++)
        {
            mesh.Flip();
            changed = true;
        }
        return changed ? mesh.ToTriangles() : tris;
    }

    /// <summary>The triangles as indices into domain points, each with its surface point.</summary>
    sealed class Mesh
    {
        /// <summary>The most passes of flips, each flipping edges whose triangles no other flip in the
        /// pass touched. Flips end on their own (each lowers the total sag); this caps the work.</summary>
        const int MaxFlipPasses = 256;

        readonly SurfaceMap _map;
        readonly float _maxSag;
        readonly List<Vector2> _uv = [];
        readonly List<Vector3> _xyz = [];
        readonly HashSet<(int, int)> _boundary = [];
        List<Integer3> _triangles = [];

        public Mesh(SurfaceMap map, IReadOnlyList<Triangle2D> tris, float maxSag)
        {
            _map = map;
            _maxSag = maxSag;
            var indices = new Dictionary<System.Numerics.Vector2, int>();
            int Index(Point2D p)
            {
                if (!indices.TryGetValue(p.Vector2.Value, out var i))
                    indices[p.Vector2.Value] = i = Add(p.Vector2);
                return i;
            }
            foreach (var t in tris)
                _triangles.Add(new Integer3(Index(t.A), Index(t.B), Index(t.C)));
            foreach (var (e, sides) in Sides())
                if (sides.Count == 1)
                    _boundary.Add(e);
        }

        public int TriangleCount => _triangles.Count;

        public IReadOnlyList<Triangle2D> ToTriangles()
            => _triangles.Select(t => new Triangle2D(_uv[t.A], _uv[t.B], _uv[t.C])).ToList();

        /// <summary>Flips interior edges over the sag to the other diagonal of their quad when it sags
        /// less and both new triangles keep the winding; true when any edge was flipped.</summary>
        public bool Flip()
        {
            var any = false;
            for (var pass = 0; pass < MaxFlipPasses; pass++)
            {
                var touched = new HashSet<int>();
                foreach (var (e, sides) in Sides())
                {
                    if (sides.Count != 2 || touched.Contains(sides[0]) || touched.Contains(sides[1]))
                        continue;
                    var sag = Sag(e.Item1, e.Item2);
                    if (sag <= _maxSag)
                        continue;
                    var (a, b, c) = Opposite(_triangles[sides[0]], e);
                    var d = Opposite(_triangles[sides[1]], e).C;
                    if (Area2(a, d, c) <= 0 || Area2(d, b, c) <= 0 || Sag(c, d) >= sag)
                        continue;
                    _triangles[sides[0]] = new Integer3(a, d, c);
                    _triangles[sides[1]] = new Integer3(d, b, c);
                    touched.Add(sides[0]);
                    touched.Add(sides[1]);
                }
                if (touched.Count == 0)
                    break;
                any = true;
            }
            return any;
        }

        /// <summary>Splits every interior edge over the sag at its domain midpoint; false when none is.</summary>
        public bool Split()
        {
            var midpoints = new Dictionary<(int, int), int>();
            foreach (var (e, sides) in Sides())
                if (sides.Count == 2 && Sag(e.Item1, e.Item2) > _maxSag)
                    midpoints[e] = Add((_uv[e.Item1] + _uv[e.Item2]) * 0.5f);
            if (midpoints.Count == 0)
                return false;

            var next = new List<Integer3>(_triangles.Count * 2);
            foreach (var t in _triangles)
                AddSubdivided(t, midpoints, next);
            _triangles = next;
            return true;
        }

        /// <summary>Replaces <paramref name="t"/> by the triangles its split edges call for, each
        /// winding as it does. Rotated so a, b, c start at the first split edge (one split) or just
        /// after the unsplit one (two): one split gives two triangles, two give three (the quad left
        /// over is cut along its shorter diagonal), three give four.</summary>
        void AddSubdivided(Integer3 t, Dictionary<(int, int), int> midpoints, List<Integer3> result)
        {
            int[] v = [t.A, t.B, t.C];
            var m = new int[3];
            for (var k = 0; k < 3; k++)
                m[k] = midpoints.TryGetValue(Key(v[k], v[(k + 1) % 3]), out var mid) ? mid : -1;
            var splits = m.Count(x => x >= 0);
            var r = splits switch
            {
                1 => Array.FindIndex(m, x => x >= 0),
                2 => (Array.FindIndex(m, x => x < 0) + 1) % 3,
                _ => 0,
            };
            var (a, b, c) = (v[r], v[(r + 1) % 3], v[(r + 2) % 3]);
            var (ab, bc, ca) = (m[r], m[(r + 1) % 3], m[(r + 2) % 3]);
            switch (splits)
            {
                case 0:
                    result.Add(t);
                    break;
                case 1:
                    result.Add(new Integer3(a, ab, c));
                    result.Add(new Integer3(ab, b, c));
                    break;
                case 2:
                    result.Add(new Integer3(ab, b, bc));
                    if (DistanceSquared(a, bc) <= DistanceSquared(ab, c))
                    {
                        result.Add(new Integer3(a, ab, bc));
                        result.Add(new Integer3(a, bc, c));
                    }
                    else
                    {
                        result.Add(new Integer3(a, ab, c));
                        result.Add(new Integer3(ab, bc, c));
                    }
                    break;
                default:
                    result.Add(new Integer3(a, ab, ca));
                    result.Add(new Integer3(ab, b, bc));
                    result.Add(new Integer3(ca, bc, c));
                    result.Add(new Integer3(ab, bc, ca));
                    break;
            }
        }

        /// <summary>Each undirected edge with the triangles on its sides, in triangle order.</summary>
        Dictionary<(int, int), List<int>> Sides()
        {
            var sides = new Dictionary<(int, int), List<int>>();
            for (var i = 0; i < _triangles.Count; i++)
            {
                var t = _triangles[i];
                foreach (var e in (ReadOnlySpan<(int, int)>)[Key(t.A, t.B), Key(t.B, t.C), Key(t.C, t.A)])
                {
                    if (!sides.TryGetValue(e, out var list))
                        sides[e] = list = [];
                    list.Add(i);
                }
            }
            return sides;
        }

        /// <summary>The triangle's corners rotated so its edge <paramref name="e"/> runs a to b.</summary>
        static (int A, int B, int C) Opposite(Integer3 t, (int, int) e)
            => Key(t.A, t.B) == e ? (t.A, t.B, t.C)
            : Key(t.B, t.C) == e ? (t.B, t.C, t.A)
            : (t.C, t.A, t.B);

        float Sag(int i, int j)
            => _map.ChordDeviation((_uv[i] + _uv[j]) * 0.5f, (_xyz[i] + _xyz[j]) * 0.5f);

        float Area2(int i, int j, int k)
        {
            var (a, b, c) = (_uv[i].Value, _uv[j].Value, _uv[k].Value);
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        float DistanceSquared(int i, int j)
            => System.Numerics.Vector2.DistanceSquared(_uv[i].Value, _uv[j].Value);

        int Add(Vector2 uv)
        {
            _uv.Add(uv);
            _xyz.Add(_map.Unproject(uv));
            return _uv.Count - 1;
        }

        static (int, int) Key(int i, int j)
            => i < j ? (i, j) : (j, i);
    }
}
