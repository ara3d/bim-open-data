using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.Models;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>
/// example.ifc beams are extrusions of IFCARBITRARYPROFILEDEFWITHVOIDS (a square hollow section,
/// TKT-65). The mesher must keep the void: its volume for #12530 matches web-ifc's.
/// </summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class ExampleBeamTests
{
    const int HollowBeam = 12530;
    /// <summary>The five meshers put #12530 between 0.005 and 0.007 (bench/ifc-mesher/discrepancies/example.md),
    /// so one oracle's number carries about 20 % of spread.</summary>
    const double VolumeTolerance = 0.20;
    const double ShapeFloor = 0.9;

    [Test]
    public void HollowBeam_VolumeWithinOracleSpreadOfWebIfc()
    {
        var ifc = TestFiles.Example;
        TestFiles.RequireExists(ifc);
        var ours = ModelComparer.EntityMeshes(ModelComparer.LoadCandidate(ifc));
        var theirs = ModelComparer.EntityMeshes(ModelComparer.LoadOracle(ifc));

        var below = ours.Keys.Where(id => theirs.ContainsKey(id)
            && ShapeScore(ours[id], theirs[id], id) < ShapeFloor).ToList();
        TestContext.Out.WriteLine($"example.ifc products with shape score < {ShapeFloor} against web-ifc: {below.Count} of {ours.Count}");

        var mine = MeshHelpers.SignedVolume(ours[HollowBeam]);
        var oracle = MeshHelpers.SignedVolume(theirs[HollowBeam]);
        TestContext.Out.WriteLine($"#{HollowBeam} signed volume mine {mine:F5} web-ifc {oracle:F5}");
        Assert.That(mine, Is.EqualTo(oracle).Within(Math.Abs(oracle) * VolumeTolerance));
    }

    static double ShapeScore(TriangleMesh3D a, TriangleMesh3D b, int id)
        => ModelComparer.Compare(Single(a, id), Single(b, id), "").EntityShape.Score;

    static Model3D Single(TriangleMesh3D mesh, int id)
    {
        var builder = new Model3DBuilder();
        builder.Meshes.Add(mesh);
        builder.AddInstance(0, Matrix4x4.Identity, Material.Default, id);
        return builder.Build();
    }
}
