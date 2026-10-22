using Ara3D.IO.StepParser;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Utils;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>The host's refinement loop on blocking requests (StepPlatoMesher.Refined): six u-blox faces whose first
/// pass is FACE-NOT-SIMPLE, chord artifacts that layer 5 asks to refine, mesh after it, and the assembled mesh around
/// them keeps every edge paired. The files are not committed: each is looked up under data/step-samples/u-blox (the
/// toolkit's scripts/fetch-step-samples.ps1) or the folder the STEP_CORPUS variable names (the 3D-Step-Models-Library
/// checkout), and the case is skipped when neither holds it.</summary>
public static class PlatoRefinementTests
{
    static readonly double Chordal = new StepMeshingOptions().MaxDeviation * 1000;

    [TestCase("POS/ANN-MB2.step", new[] { 260, 445, 1606, 1699 })]
    [TestCase("POS/CAM.STEP", new[] { 124, 146 })]
    public static void NotSimpleFacesMeshAfterRefinement(string file, int[] faces)
    {
        var mesh = new StepModel(new StepDocument(new FilePath(Find(file)))).MeshFile(Chordal);
        foreach (var f in faces)
        {
            var o = mesh.Outcomes[f];
            Assert.That(o.Mesh.Triangles.Count + o.Mesh.Quads.Count, Is.GreaterThan(0),
                $"face {f}: {string.Join("; ", o.Diagnostics.Select(d => (string)d.Code))}");
        }
        Assert.That(mesh.Failures.Where(x => x.Kind == StepPlatoMesher.RefineFailure), Is.Empty, "the loop ran to its end");
    }

    /// <summary>Faces refined on one side of an edge are re-meshed on the other side too: against the first pass
    /// alone, the refined mesh has no misoriented or non-manifold edge, and its boundary edges lie only on B-rep faces
    /// that had boundary edges before, never on the faces refinement meshed. CAM keeps 13 boundary edges on faces 525,
    /// 528, 571, 600, 736 and 991 either way, far from faces 124 and 146.</summary>
    [TestCase("POS/CAM.STEP", new[] { 124, 146 })]
    public static void RefinementClosesHolesAndOpensNone(string file, int[] faces)
    {
        var model = new StepModel(new StepDocument(new FilePath(Find(file))));
        var before = BoundaryFaces(model.MeshFile(Chordal, 0).Mesh, out _);
        var after = BoundaryFaces(model.MeshFile(Chordal).Mesh, out var counts);
        Assert.That((counts.Misoriented, counts.NonManifold), Is.EqualTo((0, 0)));
        Assert.That(after.Keys, Is.SubsetOf(before.Keys));
        Assert.That(after.Values.Sum(), Is.LessThan(before.Values.Sum()));
        Assert.That(after.Keys.Intersect(faces), Is.Empty);
    }

    /// <summary>Boundary edges (undirected edges used once) per B-rep face, over the assembled mesh's shared vertex
    /// numbering, with the counts of non-manifold edges (used more than twice) and misoriented ones (a directed edge
    /// used more than once).</summary>
    static Dictionary<int, int> BoundaryFaces(StepBrep.ModelMesh mesh, out (int NonManifold, int Misoriented) counts)
    {
        var triangles = mesh.Triangles.Count;
        var rings = mesh.Triangles.Select((t, i) => (Element: i, V: new[] { (int)t.A.Value, (int)t.B.Value, (int)t.C.Value }))
            .Concat(mesh.Quads.Select((q, i) => (Element: triangles + i, V: new[] { (int)q.A.Value, (int)q.B.Value, (int)q.C.Value, (int)q.D.Value })));
        var directed = rings.SelectMany(r => r.V.Select((a, k) => (From: a, To: r.V[(k + 1) % r.V.Length], Face: (int)mesh.ElementFace[r.Element].Value)))
            .ToList();
        var undirected = directed.GroupBy(e => (Math.Min(e.From, e.To), Math.Max(e.From, e.To))).ToList();
        counts = (undirected.Count(g => g.Count() > 2), directed.GroupBy(e => (e.From, e.To)).Count(g => g.Count() > 1));
        return undirected.Where(g => g.Count() == 1).GroupBy(g => g.First().Face).ToDictionary(g => g.Key, g => g.Count());
    }

    static string Find(string relative)
    {
        var corpus = Environment.GetEnvironmentVariable("STEP_CORPUS");
        if (!string.IsNullOrEmpty(corpus) && File.Exists(Path.Combine(corpus, relative)))
            return Path.Combine(corpus, relative);
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "step-samples", "u-blox", relative);
            if (File.Exists(candidate))
                return candidate;
        }
        Assert.Ignore($"{relative} not found under STEP_CORPUS or data/step-samples/u-blox");
        return "";
    }
}
