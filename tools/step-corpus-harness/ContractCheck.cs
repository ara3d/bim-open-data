using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One face measured against the version 1 mesh contract (bim-open-toolkit's sandbox/step-brep-mesh/README.md,
/// "Version 1 mesh contract"). <see cref="Deviation"/> is the larger of two distances: from the surface, at sample
/// points of an element's parameter triangles, to the element in 3D, and from sample points of the element in 3D to the
/// surface (by a projection seeded at the sample's own parameters, made only where it could matter: see
/// <see cref="ContractCheck.ProjectAbove"/>). A triangle's surface samples are measured to the triangle; a quad's, for each of its two
/// diagonal splits, to that split as a whole (the nearer of its two triangles), so a surface point that a non-uniform
/// parametrization places beside one triangle of a split, on the other, is not counted against the split (TKT-455);
/// the worse split counts, since a renderer may draw either. <see cref="CornerGap"/>
/// is the largest distance between an element corner's position and the surface at its parameter point, which a
/// file's edge-to-surface gap explains and the mesher does not; <see cref="Flipped"/> counts elements whose 3D normal
/// points against the face normal at their parameter centroid, <see cref="Degenerate"/> the slivers: twice the area
/// over the longest side squared below <see cref="ContractCheck.SliverRatio"/>. A sliver's normal and its distance to
/// a point are rounding noise, so it is left out of the flip test and of both distance measures (TKT-463: RCB-F9T face
/// 874's collinear triangle read as 8.53 over a 1.00 bound, and slivers on NEO, PAM and NORA read as flipped).</summary>
public sealed record FaceContract(int Face, string Surface, int Elements, double Deviation, double CornerGap, int Flipped, int Degenerate, bool Explained,
    int Excused = 0)
{
    /// <summary>Chordal is broken when the deviation exceeds the bound by more than the corner gap: the measure is a
    /// lower bound on the true deviation, so an excess is real, and the gap is the part the file is answerable for.</summary>
    public bool OverChordal(double bound) => Deviation > bound + CornerGap;

    public bool Fails(double bound) => OverChordal(bound) || Flipped > 0 || Degenerate > 0 || !Explained;

    public const string CsvHeader = "face,surface,elements,deviation,corner_gap,bound,flipped,degenerate,explained,excused";

    public string Csv(double bound)
        => TimeTables.Join(Face.ToString(), Surface, Elements.ToString(), Format.Number(Deviation), Format.Number(CornerGap), Format.Number(bound),
            Flipped.ToString(), Degenerate.ToString(), Explained.ToString(), Excused.ToString());
}

/// <summary>The version 1 contract on every face of one file, checked from the face meshes and the surfaces alone,
/// independently of the library's own checks (layer 7), so a defect in those checks or in the laws over them cannot
/// hide a defect here: chordal by sampling, orientation against the surface normal, no degenerate element, and every
/// face meshed or explained by a diagnostic. With <see cref="MeshCheck"/> (closed, oriented across faces) it covers
/// the contract's closed, watertight, oriented, covering and chordal bullets on the mesh the host assembles.</summary>
public sealed record ContractCheck(int FacesChecked, double Bound, int OverChordal, int WithFlipped, int WithDegenerate, int Unexplained,
    double WorstExcess, IReadOnlyList<FaceContract> Failing, int WithExcused = 0)
{
    /// <summary>The face diagnostics that explain degenerate or flipped elements the file forces on a face: layer 7's
    /// ForcedDegenerateExplanations (v1-gate, V1G-19). On a face whose outcome carries one, its slivers and flipped
    /// elements are counted as <see cref="FaceContract.Excused"/>, not failures; its deviation and corner gap are
    /// still checked. A copy until the regenerated library has the function; then read
    /// ForcedDegenerateExplanations(0).Select(r => (string)r.Code).</summary>
    public static readonly string[] ForcedDegenerateCodes = ["FACE-ZERO-WIDTH", "FACE-CUSP"];

    public bool Holds => OverChordal == 0 && WithFlipped == 0 && WithDegenerate == 0 && Unexplained == 0;

    /// <summary>Twice a triangle's area over its longest side squared, below which it is a sliver: its corners are
    /// collinear to within about 1e-9 of its length (an equilateral triangle has 0.87).</summary>
    public const double SliverRatio = 1e-9;

    /// <summary>The fraction of the bound below which a sample is not projected: the distance from a sample of a drawn
    /// element to the surface point at the sample's own parameters is an upper bound on its distance to the surface,
    /// so a sample whose bound is under a quarter of the chordal bound cannot decide the verdict. Planes, whose
    /// parameters are affine, never project; projecting every sample cost 43% of meshing on RCB-F9T.</summary>
    public const double ProjectAbove = 0.25;

    /// <summary>Barycentric sample points of a triangle: the centroid, the side midpoints, and three points between
    /// the centroid and the corners. A flat element over a curved surface deviates most near its centroid, and a side
    /// that is not a boundary segment deviates most near its midpoint.</summary>
    static readonly (double A, double B, double C)[] Samples =
    [
        (1.0 / 3, 1.0 / 3, 1.0 / 3), (0.5, 0.5, 0), (0, 0.5, 0.5), (0.5, 0, 0.5),
        (2.0 / 3, 1.0 / 6, 1.0 / 6), (1.0 / 6, 2.0 / 3, 1.0 / 6), (1.0 / 6, 1.0 / 6, 2.0 / 3),
    ];

    public static ContractCheck Of(BrepModel m, IReadOnlyList<FaceRow> faces, double chordal, bool parallel)
    {
        double bound = chordal + (double)m.Tolerances.Length.Value;
        var rows = new FaceContract[faces.Count];
        if (parallel)
            Parallel.For(0, faces.Count, i => rows[i] = Face(m, faces[i], bound));
        else
            for (var i = 0; i < faces.Count; i++)
                rows[i] = Face(m, faces[i], bound);
        var failing = rows.Where(r => r.Fails(bound)).ToArray();
        return new(rows.Length, bound, rows.Count(r => r.OverChordal(bound)), rows.Count(r => r.Flipped > 0),
            rows.Count(r => r.Degenerate > 0), rows.Count(r => !r.Explained),
            rows.Select(r => r.Deviation - r.CornerGap - bound).DefaultIfEmpty(0).Max(), failing, rows.Count(r => r.Excused > 0));
    }

    static FaceContract Face(BrepModel m, FaceRow row, double bound)
    {
        var fm = row.Outcome.Mesh;
        var elements = fm.Triangles.Count + fm.Quads.Count;
        var explained = elements > 0 || row.Outcome.Diagnostics.Count > 0;
        if (elements == 0)
            return new(row.Index, row.Surface, 0, 0, 0, 0, 0, explained);
        try
        {
            var f = new BrepFaceIndex(row.Index);
            var s = m.SurfaceOf(f);
            var surface = m.Geometry.SurfaceAt(s);
            var closedForm = (bool)m.Geometry.HasClosedFormProjection(s);
            // The distance from p to the surface, measured only when the surface point at p's own parameters (the
            // same barycentric mix of the corners' parameters) is farther than ProjectAbove of the bound, then by a
            // projection seeded there (closed form where the surface has one).
            double ToSurface(P p, (double U, double V) uvAt)
            {
                var seed = new UvCoordinate(uvAt.U, uvAt.V);
                var here = V(m.Geometry.Eval(s, seed));
                if (Dist(p, here) <= ProjectAbove * bound)
                    return 0;
                var q = new Point3D(p.X, p.Y, p.Z);
                var uv = closedForm ? m.Geometry.Project(s, q) : surface.ProjectNearBySearchCounted(m.Geometry, q, seed).Value;
                return Dist(p, V(m.Geometry.Eval(s, uv)));
            }
            var uv = fm.UvPoints.Select(p => ((double)p.U, (double)p.V)).ToArray();
            var at = fm.UvPoints.Select(p => V(m.Geometry.Eval(s, p))).ToArray();
            var corner = Enumerable.Range(0, uv.Length).Select(i => V(fm.Positions[(int)fm.LocalVertex[i].Value])).ToArray();
            var normals = fm.Normals.Select(n => V(n.Vector)).ToArray();
            double deviation = 0, gap = 0;
            int flipped = 0, degenerate = 0;
            bool Sliver(int a, int b, int c)
            {
                var n = Cross(Sub(corner[b], corner[a]), Sub(corner[c], corner[a]));
                var longest = Math.Max(Dist(corner[a], corner[b]), Math.Max(Dist(corner[b], corner[c]), Dist(corner[c], corner[a])));
                return Math.Sqrt(Dot(n, n)) <= SliverRatio * longest * longest;
            }
            // Corner checks, orientation and the mesh-to-surface direction of one drawn triangle; its
            // surface-to-mesh samples are measured by the caller, against the triangle or its split. A sliver
            // counts as degenerate and is measured no further.
            void Triangle(int a, int b, int c)
            {
                gap = Math.Max(gap, Math.Max(Dist(at[a], corner[a]), Math.Max(Dist(at[b], corner[b]), Dist(at[c], corner[c]))));
                var n = Cross(Sub(corner[b], corner[a]), Sub(corner[c], corner[a]));
                if (Sliver(a, b, c))
                {
                    degenerate++;
                    return;
                }
                var mid = Mix(uv[a], uv[b], uv[c], (1.0 / 3, 1.0 / 3, 1.0 / 3));
                var fallback = Add(Add(normals[a], normals[b]), normals[c]);
                var face = V(m.FaceNormalAt(f, new UvCoordinate(mid.U, mid.V), new Vector3D(fallback.X, fallback.Y, fallback.Z)));
                if (Dot(n, face) < 0)
                    flipped++;
                foreach (var w in Samples)
                    deviation = Math.Max(deviation, ToSurface(Mix3(corner[a], corner[b], corner[c], w), Mix(uv[a], uv[b], uv[c], w)));
            }
            // The largest distance from the surface at the samples of the parameter triangles listed to the
            // nearest of the 3D triangles listed: one triangle, or one split of a quad; slivers left out (the other
            // part of a split covers a quad's area).
            double SurfaceToMesh((int A, int B, int C)[] all)
            {
                var parts = all.Where(t => !Sliver(t.A, t.B, t.C)).ToArray();
                double worst = 0;
                foreach (var (a, b, c) in parts)
                    foreach (var w in Samples)
                    {
                        var p = Mix(uv[a], uv[b], uv[c], w);
                        var on = V(m.Geometry.Eval(s, new UvCoordinate(p.U, p.V)));
                        worst = Math.Max(worst, parts.Min(t => DistanceToTriangle(on, corner[t.A], corner[t.B], corner[t.C])));
                    }
                return worst;
            }
            foreach (var t in fm.Triangles)
            {
                int a = (int)t.A.Value, b = (int)t.B.Value, c = (int)t.C.Value;
                Triangle(a, b, c);
                deviation = Math.Max(deviation, SurfaceToMesh([(a, b, c)]));
            }
            foreach (var q in fm.Quads)
            {
                int a = (int)q.A.Value, b = (int)q.B.Value, c = (int)q.C.Value, d = (int)q.D.Value;
                Triangle(a, b, c);
                Triangle(a, c, d);
                Triangle(a, b, d);
                Triangle(b, c, d);
                deviation = Math.Max(deviation, Math.Max(SurfaceToMesh([(a, b, c), (a, c, d)]), SurfaceToMesh([(a, b, d), (b, c, d)])));
            }
            return Enumerable.Any(row.Outcome.Diagnostics, x => ForcedDegenerateCodes.Contains((string)x.Code))
                ? new(row.Index, row.Surface, elements, deviation, gap, 0, 0, explained, flipped + degenerate)
                : new(row.Index, row.Surface, elements, deviation, gap, flipped, degenerate, explained);
        }
        catch (Exception)
        {
            // A surface the library cannot evaluate at a mesh point is itself a broken contract: report it as
            // unexplained rather than let one face end the file's check.
            return new(row.Index, row.Surface, elements, double.PositiveInfinity, 0, 0, 0, false);
        }
    }

    readonly record struct P(double X, double Y, double Z);

    static P V(Point3D p) => new(p.X, p.Y, p.Z);
    static P V(Vector3D v) => new(v.X, v.Y, v.Z);
    static P Sub(P a, P b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    static P Add(P a, P b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    static P Scale(P a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    static double Dot(P a, P b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static P Cross(P a, P b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    static double Dist(P a, P b) => Math.Sqrt(Dot(Sub(a, b), Sub(a, b)));

    static (double U, double V) Mix((double U, double V) a, (double U, double V) b, (double U, double V) c, (double A, double B, double C) w)
        => (w.A * a.U + w.B * b.U + w.C * c.U, w.A * a.V + w.B * b.V + w.C * c.V);

    static P Mix3(P a, P b, P c, (double A, double B, double C) w)
        => new(w.A * a.X + w.B * b.X + w.C * c.X, w.A * a.Y + w.B * b.Y + w.C * c.Y, w.A * a.Z + w.B * b.Z + w.C * c.Z);

    /// <summary>The distance from p to the closest point of triangle abc (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    static double DistanceToTriangle(P p, P a, P b, P c)
    {
        var ab = Sub(b, a);
        var ac = Sub(c, a);
        var ap = Sub(p, a);
        double d1 = Dot(ab, ap), d2 = Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return Dist(p, a);
        var bp = Sub(p, b);
        double d3 = Dot(ab, bp), d4 = Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return Dist(p, b);
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return Dist(p, Add(a, Scale(ab, d1 / (d1 - d3))));
        var cp = Sub(p, c);
        double d5 = Dot(ab, cp), d6 = Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return Dist(p, c);
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return Dist(p, Add(a, Scale(ac, d2 / (d2 - d6))));
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            return Dist(p, Add(b, Scale(Sub(c, b), (d4 - d3) / (d4 - d3 + (d5 - d6)))));
        var denom = va + vb + vc;
        if (denom == 0) return Dist(p, a);
        return Dist(p, Add(a, Add(Scale(ab, vb / denom), Scale(ac, vc / denom))));
    }

    public override string ToString()
        => $"contract {(Holds ? "holds" : "broken")} faces {FacesChecked} over-chordal {OverChordal} flipped {WithFlipped} degenerate {WithDegenerate} unexplained {Unexplained} worst-excess {WorstExcess:G3} excused {WithExcused}";
}
