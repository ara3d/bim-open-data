using System.Text;
using System.Text.RegularExpressions;
using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;
using Ara3D.Ifc.Mesher.Tests;
using Ara3D.Ifc.Mesher.Tests.GeometryOracles;
using Ara3D.IfcLoader;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Tests.Support;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Corpus;

/// <summary>Boolean clips in the corpus models.</summary>
[TestFixture]
[Category("IfcMesherCorrectness")]
public sealed class CorpusClipTests
{
    [Test]
    [Category("Slow")]
    public void SteelPlates_ClippedBeams_FirstPrinciples()
    {
        var path = TestFiles.ResolveOrIgnore("steelplates.ifc");
        using var file = new IfcFile(path, includeGeometry: false);
        var ctx = new MeshingContext(file.EntityResolver);
        foreach (var id in new[] { 1193, 633, 1385 })
        {
            var entity = ctx.GetEntityOrDefault(id);
            if (entity is null)
            {
                Assert.Ignore($"#{id} missing in steelplates.ifc");
                return;
            }
            var mesh = ModelAssembler.BuildEntityMesh(ctx, entity);
            Assert.That(mesh, Is.Not.Null, $"#{id} should produce a mesh");
            var m = mesh!.Value;
            MeshTestAssert.MeshValid(m);
            Assert.That(Math.Abs(MeshHelpers.SignedVolume(m)), Is.GreaterThan(0), $"#{id} abs signed volume");
            Assert.That(TopologyOracle.CountOpenEdges(m), Is.LessThan(64),
                $"#{id} open-edge budget for clipped beam");
        }
    }

    /// <summary>Models whose products carry IFCHALFSPACESOLID clips and that build in seconds.</summary>
    static readonly string[] HalfSpaceFiles =
    [
        "steelplates.ifc", "example.ifc", "IfcOpenHouse_IFC4.ifc", "AC20-FZK-Haus.ifc", "small.ifc",
        "ISSUE_021_Mini Project.ifc", "dental_clinic.ifc", "Office_A_20110811.ifc",
    ];

    const double BoundsTolerance = 0.05;

    /// <summary>
    /// Clipped products still off web-ifc by more than 5 percent, whose cause is not the half-space side: both are
    /// dental_clinic.ifc window-frame parts whose minimum X differs by 0.12 m. Measured 2026-10-11: with the old
    /// two-reading rule 14 products were off, with one reading and the extrusion-end special case 5, with
    /// one reading alone these 2.
    /// </summary>
    static readonly string[] KnownOff = ["dental_clinic.ifc #1951", "dental_clinic.ifc #2092"];

    /// <summary>
    /// One reading of IFCHALFSPACESOLID.AgreementFlag (TKT-188): for every product clipped by a half-space,
    /// our bounds must match web-ifc's whatever the plane direction. The agreement flags are listed so the
    /// table shows both TRUE and FALSE cases and Z-aligned and oblique planes.
    /// </summary>
    [Test]
    [Category("Slow")]
    public void HalfSpaceClippedProducts_MatchWebIfcBounds()
    {
        var table = new StringBuilder();
        var off = new List<string>();
        var checkedProducts = 0;
        foreach (var name in HalfSpaceFiles)
        {
            var path = TestFiles.ResolveIfc(name);
            if (!path.Exists())
                continue;
            var clipped = ClippedProducts(File.ReadAllText(path));
            if (clipped.Count == 0)
                continue;
            var mine = ModelComparer.EntityMeshes(ModelComparer.LoadCandidate(path));
            var theirs = ModelComparer.EntityMeshes(ModelComparer.LoadOracle(path));
            foreach (var (id, flags) in clipped.OrderBy(c => c.Key))
            {
                if (!mine.TryGetValue(id, out var m) || !theirs.TryGetValue(id, out var o))
                    continue;
                checkedProducts++;
                var mb = MeshHelpers.GetBounds(m);
                var ob = MeshHelpers.GetBounds(o);
                var close = OracleComparison.BoundsClose(mb, ob, BoundsTolerance);
                table.AppendLine($"{name} #{id} agreement {string.Join("/", flags)} bounds {(close ? "match" : "DIFFER")} "
                    + $"mine {mb.Min}..{mb.Max} web-ifc {ob.Min}..{ob.Max}");
                if (!close)
                    off.Add($"{name} #{id}");
            }
        }
        TestContext.Out.WriteLine(table.ToString());
        TestContext.Out.WriteLine($"half-space products checked {checkedProducts}, off web-ifc {off.Count}: {string.Join(", ", off)}");
        if (checkedProducts == 0)
            Assert.Ignore("No model with half-space clips available");
        Assert.That(off.Except(KnownOff), Is.Empty);
    }

    static readonly Regex EntityLine = new(@"#(\d+)\s*=\s*(\w+)\s*\(([^;]*)\)\s*;", RegexOptions.Compiled);
    static readonly Regex Reference = new(@"#(\d+)", RegexOptions.Compiled);

    static readonly string[] PassThrough =
        ["IFCBOOLEAN", "IFCSHAPEREPRESENTATION", "IFCPRODUCTDEFINITIONSHAPE", "IFCMAPPEDITEM", "IFCREPRESENTATIONMAP",
         "IFCSTYLEDITEM", "IFCGEOMETRICSET"];

    /// <summary>
    /// Product express id (the first entity above a half-space clip that is not part of a representation) to the
    /// AgreementFlag values found under it, read from the STEP text.
    /// </summary>
    static Dictionary<int, SortedSet<string>> ClippedProducts(string text)
    {
        var names = new Dictionary<int, string>();
        var args = new Dictionary<int, string>();
        var parents = new Dictionary<int, List<int>>();
        foreach (Match line in EntityLine.Matches(text))
        {
            var id = int.Parse(line.Groups[1].Value);
            names[id] = line.Groups[2].Value.ToUpperInvariant();
            args[id] = line.Groups[3].Value;
            foreach (Match r in Reference.Matches(args[id]))
            {
                var child = int.Parse(r.Groups[1].Value);
                if (!parents.TryGetValue(child, out var list))
                    parents[child] = list = [];
                list.Add(id);
            }
        }

        var result = new Dictionary<int, SortedSet<string>>();
        foreach (var (halfSpace, name) in names.Where(n => n.Value is "IFCHALFSPACESOLID" or "IFCPOLYGONALBOUNDEDHALFSPACE"))
        {
            var flag = args[halfSpace].Contains(".T.") ? "T" : "F";
            var seen = new HashSet<int> { halfSpace };
            var queue = new Queue<int>([halfSpace]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var parent in parents.GetValueOrDefault(current) ?? [])
                {
                    if (!seen.Add(parent))
                        continue;
                    var parentName = names[parent];
                    if (PassThrough.Any(parentName.StartsWith))
                        queue.Enqueue(parent);
                    else
                    {
                        if (!result.TryGetValue(parent, out var flags))
                            result[parent] = flags = [];
                        flags.Add(flag);
                    }
                }
            }
        }
        return result;
    }
}
