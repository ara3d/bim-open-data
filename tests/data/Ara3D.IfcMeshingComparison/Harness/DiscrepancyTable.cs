using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>Per-product comparison of the meshers on one file: which products our mesher gets
/// different from the others, and by how much. Shape scores come from
/// <see cref="ModelComparer"/>'s entity-shape metric run on one product at a time; bounds closeness is
/// <see cref="OracleComparison.BoundsClose"/> at 5 %. The majority is defined in
/// <see cref="DiscrepancyTableConsensus"/>. The two web-ifc builds in the catalog count as two voters even
/// though they share an engine.</summary>
public static class DiscrepancyTable
{
    const double BoundsTolerance = 0.05;

    public static IReadOnlyList<ProductDiscrepancy> Build(FilePath ifc, IReadOnlyList<IMeshingBackend> backends)
        => FromResults(ifc, backends.Select(b => b.Build(ifc)).ToList());

    /// <summary>The table from meshes already built. Failed results are left out.</summary>
    public static IReadOnlyList<ProductDiscrepancy> FromResults(FilePath ifc, IReadOnlyList<MeshingResult> results)
    {
        var built = results.Where(r => r.Success && r.Model is not null).ToList();
        if (built.Count == 0)
            return [];
        var names = built.Select(r => r.BackendName).ToList();
        var meshes = built.Select(r => ModelComparer.EntityMeshes(r.Model!)).ToList();
        var kinds = DiscrepancyTableKinds.Read(ifc, built[0].Model!);
        var ids = meshes.SelectMany(m => m.Keys).Distinct().OrderBy(i => i).ToList();
        var ours = names.IndexOf(IfcMesher.Name);
        var rows = ids.AsParallel()
            .Select(id => Row(id, names, meshes.Select(m => m.TryGetValue(id, out var mesh) ? (TriangleMesh3D?)mesh : null).ToList(), ours, kinds))
            .ToList();
        return rows.OrderByDescending(r => r.Distance).ThenBy(r => r.ExpressId).ToList();
    }

    static ProductDiscrepancy Row(int id, IReadOnlyList<string> names, IReadOnlyList<TriangleMesh3D?> meshes, int ours, DiscrepancyTableKinds kinds)
    {
        var present = Enumerable.Range(0, names.Count).Where(i => meshes[i].HasValue).ToList();
        var shapes = PairShapes(id, meshes, present);
        double Sim(int a, int b) => shapes[(Math.Min(a, b), Math.Max(a, b))];
        var voters = present.Where(i => i != ours).ToList();
        var consensus = DiscrepancyTableConsensus.Find(voters, Sim);
        var anchor = consensus.Members.Count > 0 ? consensus.Members[0] : -1;
        var views = new Dictionary<string, MesherView>();
        foreach (var i in present)
        {
            var others = consensus.Members.Where(m => m != i).ToList();
            var score = others.Count == 0 ? 1.0 : others.Average(m => Sim(i, m));
            var bounds = MeshHelpers.GetBounds(meshes[i]!.Value);
            var close = anchor < 0 || i == anchor
                || OracleComparison.BoundsClose(bounds, MeshHelpers.GetBounds(meshes[anchor]!.Value), BoundsTolerance);
            views[names[i]] = new MesherView(meshes[i]!.Value.FaceIndices.Count, bounds, MeshHelpers.SignedVolume(meshes[i]!.Value), score, close);
        }
        var (verdict, distance) = Judge(ours, present, consensus, views, names);
        return new ProductDiscrepancy(id, kinds.Product(id), kinds.Geometry(id), views,
            consensus.Members.Select(m => names[m]).ToList(), verdict, distance);
    }

    static (string Verdict, double Distance) Judge(
        int ours, IReadOnlyList<int> present, Consensus consensus, Dictionary<string, MesherView> views, IReadOnlyList<string> names)
    {
        var oursPresent = ours >= 0 && present.Contains(ours);
        var others = present.Count - (oursPresent ? 1 : 0);
        if (!oursPresent)
            return (Verdicts.MissingInOurs, 1.0);
        if (others == 0)
            return (Verdicts.ExtraInOurs, 1.0);
        var score = views[names[ours]].ShapeScore;
        if (!consensus.Exists)
            return (Verdicts.NoConsensus, 1.0 - score);
        if (score < DiscrepancyTableConsensus.AgreeThreshold)
            return (Verdicts.OursDiffers, 1.0 - score);
        return (consensus.Members.Count == others ? Verdicts.AllAgree : Verdicts.OursAgrees, 1.0 - score);
    }

    /// <summary>Shape similarity for each pair (low index, high index) of meshers that have the product.</summary>
    static Dictionary<(int, int), double> PairShapes(int id, IReadOnlyList<TriangleMesh3D?> meshes, IReadOnlyList<int> present)
    {
        var result = new Dictionary<(int, int), double>();
        foreach (var a in present)
        foreach (var b in present.Where(b => b > a))
            result[(a, b)] = ShapeSimilarity(id, meshes[a]!.Value, meshes[b]!.Value);
        return result;
    }

    /// <summary>One product's shape score, a against b. When b has no comparable surface the metric
    /// compares nothing and reports 1, so the reverse order tells a one-sided gap (0) from two empty
    /// meshes (1).</summary>
    static double ShapeSimilarity(int id, TriangleMesh3D a, TriangleMesh3D b)
    {
        var forward = ModelComparer.Compare(Single(a, id), Single(b, id), "").EntityShape;
        if (forward.ComparedCount > 0)
            return forward.Score;
        var reverse = ModelComparer.Compare(Single(b, id), Single(a, id), "").EntityShape;
        return reverse.ComparedCount > 0 ? 0.0 : 1.0;
    }

    static Model3D Single(TriangleMesh3D mesh, int id)
    {
        var builder = new Model3DBuilder();
        builder.Meshes.Add(mesh);
        builder.AddInstance(0, Matrix4x4.Identity, Material.Default, id);
        return builder.Build();
    }
}
