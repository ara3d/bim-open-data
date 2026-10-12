using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Models;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>
/// AC20-FZK-Haus upper walls are cut by IFCBOOLEANCLIPPINGRESULT under the roof planes (TKT-188).
/// Bounds are compared with the web-ifc oracle, per product.
/// </summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class FzkHausRoofClipTests
{
    const double Tolerance = 0.05;

    /// <summary>Products more than 5 % off web-ifc before the roof-clip fix (measured 2026-10-11).</summary>
    const int BaselineProductsOff = 2;

    static readonly int[] RoofClippedWalls = [67536, 75347];

    [Test]
    [Category("Slow")]
    public void RoofClippedWalls_BoundsMatchOracle_AndNoNewDisagreements()
    {
        var path = TestFiles.Ac20FzkHaus;
        var mine = EntityBounds(ModelComparer.LoadCandidate(path));
        var theirs = EntityBounds(ModelComparer.LoadOracle(path));

        var off = theirs.Keys.Where(id => mine.TryGetValue(id, out var m)
                && !OracleComparison.BoundsClose(m, theirs[id], Tolerance)).ToList();
        TestContext.Out.WriteLine($"AC20-FZK-Haus products >5% off web-ifc: {off.Count} (baseline {BaselineProductsOff}): {string.Join(",", off)}");
        foreach (var id in RoofClippedWalls)
            TestContext.Out.WriteLine($"#{id}: mine {Describe(mine, id)} oracle {Describe(theirs, id)}");

        foreach (var id in RoofClippedWalls)
            Assert.That(OracleComparison.BoundsClose(mine[id], theirs[id], Tolerance), Is.True,
                $"#{id} bounds mine {Describe(mine, id)} oracle {Describe(theirs, id)}");
        Assert.That(off.Count, Is.LessThanOrEqualTo(BaselineProductsOff));
    }

    static string Describe(Dictionary<int, Bounds3D> map, int id)
        => map.TryGetValue(id, out var b) ? $"{b.Min}..{b.Max}" : "absent";

    static Dictionary<int, Bounds3D> EntityBounds(Model3D model)
    {
        var bounds = new Dictionary<int, Bounds3D>();
        foreach (var inst in model.Instances)
        {
            if (inst.EntityIndex < 0 || inst.MeshIndex < 0 || inst.MeshIndex >= model.Meshes.Count)
                continue;
            var b = MeshHelpers.GetBounds(MeshHelpers.Transform(model.Meshes[inst.MeshIndex], inst.Matrix4x4));
            bounds[inst.EntityIndex] = bounds.TryGetValue(inst.EntityIndex, out var e) ? e.Merge(b) : b;
        }
        return bounds;
    }
}
