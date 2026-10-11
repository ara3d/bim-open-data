using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Mesh deduplication on the AISC sculpture, whose bolts share hex heads but differ in shank
/// length, and the instance-set check on the quick files.</summary>
[TestFixture]
public sealed class SculptureDedupTests
{
    [Test]
    [Explicit("Diagnosis: dedup bucket analysis")]
    [Category("Slow")]
    public void AiscSculpture_DedupDiagnosis()
    {
        var ifcPath = TestFiles.AiscSculptureBrep;
        TestFiles.RequireExists(ifcPath);

        using var file = new IfcFile(ifcPath, includeGeometry: false);
        var (model, _) = ModelAssembler.BuildModel(file.EntityResolver);
        var oracle = ModelComparer.LoadOracle(ifcPath);
        var uniqueOracleGroups = GroupByTopology(oracle.Meshes.ToList());

        TestContext.WriteLine(
            $"candidate meshes={model.Meshes.Count}, oracle meshes={oracle.Meshes.Count}, " +
            $"unique oracle topologies={uniqueOracleGroups.Count}");
        Assert.That(model.Meshes.Count, Is.EqualTo(oracle.Meshes.Count));
    }

    [Test]
    [Category("Slow")]
    public void AiscSculpture_MeshCount_AtLeast130()
    {
        var ifcPath = TestFiles.AiscSculptureBrep;
        TestFiles.RequireExists(ifcPath);

        using var file = new IfcFile(ifcPath, includeGeometry: false);
        var (model, _) = ModelAssembler.BuildModel(file.EntityResolver);
        var oracle = ModelComparer.LoadOracle(ifcPath);

        TestContext.WriteLine(
            $"meshes {model.Meshes.Count}/{oracle.Meshes.Count}, " +
            $"inst {model.Instances.Count}/{oracle.Instances.Count}");

        Assert.That(model.Instances.Count, Is.EqualTo(oracle.Instances.Count));
        Assert.That(model.Meshes.Count, Is.GreaterThanOrEqualTo(130),
            "sculpture dedup should retain at least 130/145 oracle mesh buckets");
    }

    [Test]
    [Category("Slow")]
    public void AiscSculpture_Parity_AtLeast092()
    {
        var ifcPath = TestFiles.AiscSculptureBrep;
        TestFiles.RequireExists(ifcPath);

        var bfastPath = WebIfcBfastOracle.OraclePath(ifcPath);
        if (!bfastPath.Exists() || WebIfcBfastOracle.NeedsRegeneration(ifcPath, bfastPath))
            WebIfcBfastOracle.Generate(ifcPath, TestContext.WriteLine);

        var result = ModelComparer.CompareFile(ifcPath);
        TestContext.WriteLine(ModelComparer.FormatResult(result));

        Assert.That(result.ParityScore, Is.GreaterThanOrEqualTo(0.92));
        Assert.That(result.EntityInstances.KeyJaccard, Is.EqualTo(1.0).Within(0.001));
        Assert.That(result.MeshCount.Candidate, Is.GreaterThanOrEqualTo(130));
    }

    static List<List<int>> GroupByTopology(IReadOnlyList<TriangleMesh3D> meshes)
    {
        var groups = new List<List<int>>();
        for (var i = 0; i < meshes.Count; i++)
        {
            var found = -1;
            for (var g = 0; g < groups.Count; g++)
            {
                if (TopologyEqual(meshes[i], meshes[groups[g][0]]))
                {
                    found = g;
                    break;
                }
            }
            if (found < 0)
                groups.Add(new List<int> { i });
            else
                groups[found].Add(i);
        }
        return groups;
    }

    static bool TopologyEqual(TriangleMesh3D a, TriangleMesh3D b)
    {
        if (a.Points.Count != b.Points.Count || a.FaceIndices.Count != b.FaceIndices.Count)
            return false;
        for (var i = 0; i < a.Points.Count; i++)
            if (!a.Points[i].Equals(b.Points[i]))
                return false;
        for (var i = 0; i < a.FaceIndices.Count; i++)
        {
            var fa = a.FaceIndices[i];
            var fb = b.FaceIndices[i];
            if (fa.A != fb.A || fa.B != fb.B || fa.C != fb.C)
                return false;
        }
        return true;
    }
}
