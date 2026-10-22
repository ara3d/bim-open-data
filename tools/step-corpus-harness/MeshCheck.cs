using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>Closed and oriented checks over an assembled mesh's shared vertex numbering: an undirected edge used once
/// is a boundary edge, more than twice non-manifold; a directed edge used twice is a misoriented pair.</summary>
public sealed record MeshCheck(int BoundaryEdges, int NonManifoldEdges, int MisorientedEdges)
{
    public bool Closed => BoundaryEdges == 0 && NonManifoldEdges == 0;
    public bool Oriented => MisorientedEdges == 0;

    public static MeshCheck Of(ModelMesh mesh)
    {
        var directed = new Dictionary<(int, int), int>();
        void Add(int a, int b) => directed[(a, b)] = directed.GetValueOrDefault((a, b)) + 1;
        void Ring(params int[] v)
        {
            for (var i = 0; i < v.Length; i++)
                Add(v[i], v[(i + 1) % v.Length]);
        }
        foreach (var t in mesh.Triangles)
            Ring((int)t.A.Value, (int)t.B.Value, (int)t.C.Value);
        foreach (var q in mesh.Quads)
            Ring((int)q.A.Value, (int)q.B.Value, (int)q.C.Value, (int)q.D.Value);
        var undirected = new Dictionary<(int, int), int>();
        foreach (var ((a, b), n) in directed)
        {
            var key = a < b ? (a, b) : (b, a);
            undirected[key] = undirected.GetValueOrDefault(key) + n;
        }
        return new(undirected.Values.Count(n => n == 1), undirected.Values.Count(n => n > 2), directed.Values.Count(n => n > 1));
    }

    public override string ToString()
        => $"closed {Closed} oriented {Oriented} boundary {BoundaryEdges} nonmanifold {NonManifoldEdges} misoriented {MisorientedEdges}";
}
