using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Integer3 = Ara3D.Geometry.Integer3;
using Point3D = Ara3D.Geometry.Point3D;
using TriangleMesh3D = Ara3D.Geometry.TriangleMesh3D;
using V3 = System.Numerics.Vector3;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>A shell as plain arrays: its points, each face's outer loop (profile) as point
/// indices, and each hole as the profile it cuts and its loop.</summary>
public sealed record ShellLoops(IReadOnlyList<V3> Points, IReadOnlyList<uint[]> Profiles, IReadOnlyList<(int Profile, uint[] Loop)> Holes)
{
    /// <summary>The loops of a Fragments shell; a BIG shell keeps 32-bit indices in big_profiles
    /// and big_holes instead of the 16-bit profiles and holes.</summary>
    public static ShellLoops From(Shell shell)
    {
        var points = new V3[shell.PointsLength];
        for (var i = 0; i < points.Length; i++)
        {
            var p = shell.Points(i)!.Value;
            points[i] = new V3(p.X, p.Y, p.Z);
        }

        var profiles = new List<uint[]>();
        var holes = new List<(int, uint[])>();
        if (shell.Type == ShellType.BIG)
        {
            for (var j = 0; j < shell.BigProfilesLength; j++)
                profiles.Add(shell.BigProfiles(j)!.Value.GetIndicesArray() ?? []);
            for (var j = 0; j < shell.BigHolesLength; j++)
            {
                var h = shell.BigHoles(j)!.Value;
                holes.Add((h.ProfileId, h.GetIndicesArray() ?? []));
            }
        }
        else
        {
            for (var j = 0; j < shell.ProfilesLength; j++)
                profiles.Add(Widen(shell.Profiles(j)!.Value.GetIndicesArray()));
            for (var j = 0; j < shell.HolesLength; j++)
            {
                var h = shell.Holes(j)!.Value;
                holes.Add((h.ProfileId, Widen(h.GetIndicesArray())));
            }
        }
        return new ShellLoops(points, profiles, holes);
    }

    private static uint[] Widen(ushort[]? indices)
        => indices == null ? [] : Array.ConvertAll(indices, i => (uint)i);
}

/// <summary>Triangulates the planar faces of a shell. Each profile is a polygon in 3D, possibly
/// with holes; it is projected onto the coordinate plane its normal is most aligned with,
/// triangulated by <see cref="Earcut"/>, and each triangle is wound to face the way the
/// profile's own loop does (its Newell normal).</summary>
public static class ShellMesher
{
    /// <summary>The mesh in the shell's own coordinates, with every point of the shell, and the
    /// number of profiles skipped because they are degenerate or index outside the points.</summary>
    public static (TriangleMesh3D Mesh, int SkippedProfiles) Triangulate(ShellLoops shell)
    {
        var holesByProfile = shell.Holes.ToLookup(h => h.Profile, h => h.Loop);
        var faces = new List<Integer3>();
        var skipped = 0;
        for (var p = 0; p < shell.Profiles.Count; p++)
            if (!TriangulateProfile(shell.Points, shell.Profiles[p], holesByProfile[p].ToList(), faces))
                skipped++;
        var points = shell.Points.Select(v => new Point3D(v.X, v.Y, v.Z)).ToList();
        return (new TriangleMesh3D(points, faces), skipped);
    }

    private static bool TriangulateProfile(IReadOnlyList<V3> points, uint[] outer, List<uint[]> holes, List<Integer3> faces)
    {
        if (outer.Length < 3 || holes.Prepend(outer).Any(loop => loop.Any(i => i >= points.Count || !IsFinite(points[(int)i]))))
            return false;

        var normal = NewellNormal(points, outer);
        if (normal.Length() == 0)
            return false;

        if (outer.Length == 3 && holes.Count == 0)
        {
            faces.Add(Oriented(points, (int)outer[0], (int)outer[1], (int)outer[2], normal));
            return true;
        }

        // Project onto the plane that drops the normal's largest component, keeping the remaining
        // two in cyclic order (y,z), (z,x), or (x,y).
        var axis = MaxAxis(normal);
        var loops = holes.Prepend(outer).ToList();
        var flat = new List<double>(2 * loops.Sum(l => l.Length));
        var holeStarts = new List<int>(holes.Count);
        var pointOf = new List<int>(flat.Capacity / 2);
        foreach (var loop in loops)
        {
            if (pointOf.Count > 0)
                holeStarts.Add(pointOf.Count);
            foreach (var i in loop)
            {
                var v = points[(int)i];
                var (u, w) = axis switch { 0 => (v.Y, v.Z), 1 => (v.Z, v.X), _ => (v.X, v.Y) };
                flat.Add(u);
                flat.Add(w);
                pointOf.Add((int)i);
            }
        }

        var triangles = Earcut.Triangulate(flat, holeStarts);
        for (var t = 0; t + 2 < triangles.Count; t += 3)
            faces.Add(Oriented(points, pointOf[triangles[t]], pointOf[triangles[t + 1]], pointOf[triangles[t + 2]], normal));
        return true;
    }

    private static Integer3 Oriented(IReadOnlyList<V3> points, int a, int b, int c, System.Numerics.Vector3 normal)
    {
        var n = V3.Cross(points[b] - points[a], points[c] - points[a]);
        return V3.Dot(n, normal) < 0 ? new Integer3(a, c, b) : new Integer3(a, b, c);
    }

    /// <summary>The Newell normal of a loop: the sum of its edges' cross products, robust for
    /// concave and nearly collinear loops.</summary>
    private static V3 NewellNormal(IReadOnlyList<V3> points, uint[] loop)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < loop.Length; i++)
        {
            var a = points[(int)loop[i]];
            var b = points[(int)loop[(i + 1) % loop.Length]];
            x += ((double)a.Y - b.Y) * ((double)a.Z + b.Z);
            y += ((double)a.Z - b.Z) * ((double)a.X + b.X);
            z += ((double)a.X - b.X) * ((double)a.Y + b.Y);
        }
        return new V3((float)x, (float)y, (float)z);
    }

    private static int MaxAxis(V3 n)
    {
        var (x, y, z) = (Math.Abs(n.X), Math.Abs(n.Y), Math.Abs(n.Z));
        return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
    }

    private static bool IsFinite(V3 v)
        => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
