using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>Why one boundary edge of the assembled mesh (an undirected edge one element uses) is there, from the face
/// of that element, the shells holding the face, and the model edge the mesh edge lies on.</summary>
public enum BoundaryCause
{
    /// <summary>The face is in no shell the file declares closed: the boundary is the shell's own, expected.</summary>
    OpenShell,
    /// <summary>A closed shell, and the model edge is cited by one face only: the B-rep is open there (the file, or a
    /// repair in PrepareModel), not the mesher.</summary>
    EdgeCitedOnce,
    /// <summary>A closed shell, and another face citing the model edge has no mesh.</summary>
    BesideUnmeshedFace,
    /// <summary>A closed shell, every face citing the model edge is meshed, and their meshes do not share this mesh
    /// edge: a gap the mesher made.</summary>
    GapBetweenMeshedFaces,
    /// <summary>A closed shell, and the mesh edge lies on no model edge (its ends are face points, or two model vertices
    /// that no edge joins): a hole inside a face.</summary>
    InsideFace,
}

/// <summary>The boundary edges of an assembled mesh sorted by <see cref="BoundaryCause"/>, the closed shells that are
/// closed in the mesh, and, for the gaps, the model edges and face pairs they lie on.</summary>
public sealed record BoundaryCheck(
    IReadOnlyDictionary<BoundaryCause, int> Counts, int ClosedShells, int ClosedShellsClosed,
    IReadOnlyList<(int Edge, int Count, string Faces)> GapEdges)
{
    /// <summary>Closed as the version 1 contract means it: every shell the file declares closed has no boundary and no
    /// non-manifold edge in the mesh. An open shell's boundary is the shell's.</summary>
    public bool ClosedShellsAreClosed => ClosedShellsClosed == ClosedShells;

    public int Count(BoundaryCause c) => Counts.GetValueOrDefault(c);

    public static BoundaryCheck Of(BrepModel m, ModelMesh mesh, IReadOnlyList<FaceRow> faces, IReadOnlyList<BrepEdgeIndex>[] edgesByFace)
    {
        var shellsOfFace = new List<int>[m.Faces.Count];
        for (var s = 0; s < m.Shells.Count; s++)
            foreach (var f in m.Shells[s].Faces)
                (shellsOfFace[(int)f.Value] ??= new()).Add(s);
        bool InClosedShell(int f) => shellsOfFace[f] is { } l && Enumerable.Any(l, s => (bool)m.Shells[s].DeclaredClosed);

        var facesOfEdge = new List<int>[m.Edges.Count];
        for (var f = 0; f < edgesByFace.Length; f++)
            foreach (var e in edgesByFace[f])
                (facesOfEdge[(int)e.Value] ??= new()).Add(f);
        var edgeByEnds = new Dictionary<(int, int), int>();
        for (var e = 0; e < m.Edges.Count; e++)
            edgeByEnds[Key((int)m.Edges[e].Start.Value, (int)m.Edges[e].End.Value)] = e;

        var uses = new Dictionary<(int, int), List<int>>();
        var t = mesh.Triangles.Count;
        void Add(int a, int b, int element)
        {
            var k = Key(a, b);
            if (!uses.TryGetValue(k, out var l))
                uses[k] = l = new();
            l.Add(element);
        }
        for (var i = 0; i < t; i++)
        {
            var x = mesh.Triangles[i];
            int[] v = [(int)x.A.Value, (int)x.B.Value, (int)x.C.Value];
            for (var j = 0; j < 3; j++)
                Add(v[j], v[(j + 1) % 3], i);
        }
        for (var i = 0; i < mesh.Quads.Count; i++)
        {
            var x = mesh.Quads[i];
            int[] v = [(int)x.A.Value, (int)x.B.Value, (int)x.C.Value, (int)x.D.Value];
            for (var j = 0; j < 4; j++)
                Add(v[j], v[(j + 1) % 4], t + i);
        }

        int ModelEdge(int a, int b)
        {
            var pa = mesh.VertexProvenance[a];
            var pb = mesh.VertexProvenance[b];
            if ((bool)pa.IsOnEdge())
                return (int)pa.OnEdge_Edge.Value;
            if ((bool)pb.IsOnEdge())
                return (int)pb.OnEdge_Edge.Value;
            if ((bool)pa.IsOnVertex() && (bool)pb.IsOnVertex())
                return edgeByEnds.TryGetValue(Key((int)pa.OnVertex_Vertex.Value, (int)pb.OnVertex_Vertex.Value), out var e) ? e : -1;
            return -1;
        }
        bool Meshed(int f) => faces[f].Outcome.Mesh.Triangles.Count + faces[f].Outcome.Mesh.Quads.Count > 0;

        var counts = new Dictionary<BoundaryCause, int>();
        var gaps = new Dictionary<int, int>();
        var openShells = new HashSet<int>();
        foreach (var ((a, b), elements) in uses)
        {
            var face = (int)mesh.ElementFace[elements[0]].Value;
            if (elements.Count > 2 && InClosedShell(face))
                foreach (var s in shellsOfFace[face]!)
                    openShells.Add(s);
            if (elements.Count != 1)
                continue;
            BoundaryCause cause;
            if (!InClosedShell(face))
                cause = BoundaryCause.OpenShell;
            else
            {
                var e = ModelEdge(a, b);
                var citing = e >= 0 ? facesOfEdge[e] ?? new() : null;
                cause = citing is null ? BoundaryCause.InsideFace
                    : citing.Distinct().Count() < 2 && citing.Count < 2 ? BoundaryCause.EdgeCitedOnce
                    : !Enumerable.All(citing, Meshed) ? BoundaryCause.BesideUnmeshedFace
                    : BoundaryCause.GapBetweenMeshedFaces;
                if (cause == BoundaryCause.GapBetweenMeshedFaces)
                    gaps[e] = gaps.GetValueOrDefault(e) + 1;
                foreach (var s in shellsOfFace[face]!)
                    if ((bool)m.Shells[s].DeclaredClosed)
                        openShells.Add(s);
            }
            counts[cause] = counts.GetValueOrDefault(cause) + 1;
        }
        var closed = Enumerable.Range(0, m.Shells.Count).Where(s => (bool)m.Shells[s].DeclaredClosed).ToArray();
        var gapEdges = gaps.OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value, string.Join(" ", (facesOfEdge[kv.Key] ?? new()).Select(f => $"F{f}:{faces[f].Surface}:{faces[f].Path}"))))
            .ToArray();
        return new(counts, closed.Length, closed.Count(s => !openShells.Contains(s)), gapEdges);
    }

    static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    public override string ToString()
        => $"closed shells {ClosedShellsClosed}/{ClosedShells} closed; boundary " +
           string.Join(", ", Enum.GetValues<BoundaryCause>().Select(c => $"{c} {Count(c)}"));
}
