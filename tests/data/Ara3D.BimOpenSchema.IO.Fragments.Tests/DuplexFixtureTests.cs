using System.Numerics;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>The Duplex architecture model read two ways: Fixtures/duplex.frag (written from
/// samples/nrc/duplex-base.ifc by That Open's importer, see Fixtures/make-fixture.mjs) through
/// this reader, and samples/public/duplex.bos (the same IFC file through Ara3D.Ifc.Bos).</summary>
public class DuplexFixtureTests
{
    public static readonly string FixturePath = Path.Combine(
        RepoPaths.Root, "tests", "data", "Ara3D.BimOpenSchema.IO.Fragments.Tests", "Fixtures", "duplex.frag");

    /// <summary>Classes That Open's importer does not keep by default; their GlobalIds are only in the IFC BOS.</summary>
    private static readonly string[] SkippedByImporter =
        ["IFCOPENINGELEMENT", "IFCDOORSTYLE", "IFCWINDOWSTYLE", "IFCDOORLININGPROPERTIES", "IFCWINDOWLININGPROPERTIES"];

    /// <summary>Ara3D.Ifc.Bos turns property and quantity sets into parameters, not entities.</summary>
    private static readonly string[] SetsKeptAsEntities = ["IFCPROPERTYSET", "IFCELEMENTQUANTITY"];

    private BimData _frag = null!;
    private BimData _ifc = null!;

    [OneTimeSetUp]
    public void Read()
    {
        _frag = FragmentsToBos.Read(FixturePath);
        _ifc = ((FilePath)RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip();
    }

    [Test]
    public void EveryClassInBothFiles_HasTheSameCount()
    {
        var frag = _frag.CountByCategory();
        var ifc = _ifc.CountByCategory();
        var shared = frag.Keys.Intersect(ifc.Keys).Order().ToList();
        Assert.That(shared, Does.Contain("IFCWALLSTANDARDCASE").And.Contain("IFCDOOR").And.Contain("IFCBUILDINGSTOREY"));
        Assert.Multiple(() =>
        {
            foreach (var c in shared)
                Assert.That(frag[c], Is.EqualTo(ifc[c]), c);
        });
    }

    [Test]
    public void GlobalIds_DifferOnlyInClassesOneSideDoesNotKeep()
    {
        var fragIds = GlobalIds(_frag);
        var ifcIds = GlobalIds(_ifc);
        var onlyIfc = ifcIds.Where(kv => !fragIds.ContainsKey(kv.Key)).Select(kv => kv.Value).Distinct();
        var onlyFrag = fragIds.Where(kv => !ifcIds.ContainsKey(kv.Key)).Select(kv => kv.Value).Distinct();
        Assert.Multiple(() =>
        {
            Assert.That(fragIds.Keys.Intersect(ifcIds.Keys).Count(), Is.EqualTo(271));
            Assert.That(onlyIfc, Is.SubsetOf(SkippedByImporter));
            Assert.That(onlyFrag, Is.SubsetOf(SetsKeptAsEntities));
        });
    }

    [Test]
    public void StoreyContainmentAndAggregation_MatchIfcBos()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_frag.RelationPairs(RelationType.ContainedIn), Has.Count.EqualTo(207));
            Assert.That(_frag.RelationPairs(RelationType.ContainedIn), Is.EquivalentTo(_ifc.RelationPairs(RelationType.ContainedIn)));
            Assert.That(_frag.RelationPairs(RelationType.MemberOf), Is.EquivalentTo(_ifc.RelationPairs(RelationType.MemberOf)));
        });
    }

    [Test]
    public void InstanceTypes_MatchIfcBos()
    {
        var typed = Enumerable.Range(0, _frag.Entities.Length).Where(i => (int)_frag.Entities[i].Type >= 0).ToList();
        Assert.That(typed, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var i in typed)
            {
                var ifcEntity = _ifc.Entities[_ifc.EntityByGlobalId(_frag.GlobalId(i))];
                Assert.That(_ifc.GlobalId((int)ifcEntity.Type), Is.EqualTo(_frag.GlobalId((int)_frag.Entities[i].Type)), _frag.GlobalId(i));
            }
        });
    }

    [Test]
    public void DoorAttributesAndProperties_KeepTheTypesTheFileStates()
    {
        var door = _frag.EntityByLocalId(6652);
        var parameters = _frag.ParametersOf(door).ToList();
        ParameterValue Get(string group, string name) => parameters.Single(p => p.Group == group && p.Name == name);

        Assert.Multiple(() =>
        {
            Assert.That(_frag.Category(door), Is.EqualTo("IFCDOOR"));
            Assert.That(_frag.Name(door), Is.EqualTo(_ifc.Name(_ifc.EntityByGlobalId(_frag.GlobalId(door)))));
            Assert.That(Get("IFCDOOR", "Ifc:OverallHeight"), Is.EqualTo(new ParameterValue("IFCDOOR", "Ifc:OverallHeight", ParameterType.Number, 2.01f)));
            Assert.That(Get("IFCDOOR", "Ifc:Tag"), Is.EqualTo(new ParameterValue("IFCDOOR", "Ifc:Tag", ParameterType.String, "146596")));
            Assert.That(Get("Pset_DoorCommon", "IsExternal"), Is.EqualTo(new ParameterValue("Pset_DoorCommon", "IsExternal", ParameterType.Int, 1)));
            Assert.That(Get("PSet_Revit_Type_Construction", "Function").Type, Is.EqualTo(ParameterType.Int));
        });
    }

    [Test]
    public void EveryPropertyOfTheIfcBos_IsOnTheSameElement()
    {
        // Property-set values only (Ifc:* attribute parameters differ in typing, see the README).
        var mismatches = new List<string>();
        foreach (var i in Enumerable.Range(0, _frag.Entities.Length).Where(i => _frag.Category(i) == "IFCWALLSTANDARDCASE"))
        {
            var ifcEntity = _ifc.EntityByGlobalId(_frag.GlobalId(i));
            var frag = _frag.ParametersOf(i).Where(p => !p.Name.StartsWith("Ifc:")).Select(p => (p.Group, p.Name)).ToHashSet();
            var ifc = _ifc.ParametersOf(ifcEntity).Where(p => !p.Name.StartsWith("Ifc:") && p.Value is not "").Select(p => (p.Group, p.Name));
            mismatches.AddRange(ifc.Where(p => !frag.Contains(p)).Select(p => $"{_frag.GlobalId(i)} {p.Group}/{p.Name}"));
        }
        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void EveryElementWithGeometry_SitsWhereTheIfcBosPutsIt()
    {
        var frag = BoxesByGlobalId(_frag);
        var ifc = BoxesByGlobalId(_ifc);
        Assert.That(frag.Keys, Is.EquivalentTo(ifc.Keys));
        Assert.That(frag, Has.Count.EqualTo(236));
        var off = frag.Keys
            .Where(k => Vector3.Distance(frag[k].Min, ifc[k].Min) > 0.002f || Vector3.Distance(frag[k].Max, ifc[k].Max) > 0.002f)
            .Select(k => $"{_frag.Category(_frag.EntityByGlobalId(k))} {k}: {frag[k]} vs {ifc[k]}");
        Assert.That(off, Is.Empty, "world boxes more than 2 mm apart");
    }

    [Test]
    public void VolumePerClass_MatchesIfcBos()
    {
        // Equal signed volumes per class show the triangles cover the same surfaces facing the same
        // way; That Open merges some coplanar triangles, so the counts may differ (see the README).
        var frag = PerClass(_frag);
        var ifc = PerClass(_ifc);
        foreach (var c in frag.Keys.Order())
            TestContext.WriteLine($"{c}: triangles {frag[c].Triangles} vs {ifc[c].Triangles}, volume {frag[c].Volume:F3} vs {ifc[c].Volume:F3}");
        Assert.That(frag.Keys, Is.EquivalentTo(ifc.Keys));
        Assert.Multiple(() =>
        {
            foreach (var c in frag.Keys)
                Assert.That(frag[c].Volume, Is.EqualTo(ifc[c].Volume).Within(0.05 + 1e-3 * Math.Abs(ifc[c].Volume)), c);
        });
    }

    private static Dictionary<string, (Vector3 Min, Vector3 Max)> BoxesByGlobalId(BimData d)
        => d.Geometry.EntityBoxes().ToDictionary(kv => d.GlobalId(kv.Key), kv => kv.Value);

    /// <summary>Triangles drawn and signed volume of every instance, summed by IFC class.</summary>
    private static Dictionary<string, (long Triangles, double Volume)> PerClass(BimData d)
        => Enumerable.Range(0, d.Geometry.InstanceEntityIndex.Length)
            .GroupBy(i => d.Category(d.Geometry.InstanceEntityIndex[i]))
            .ToDictionary(g => g.Key, g => (g.Sum(i => (long)d.Geometry.Triangles(i).Count()), g.Sum(d.Geometry.SignedVolume)));

    private static Dictionary<string, string> GlobalIds(BimData d)
        => Enumerable.Range(0, d.Entities.Length)
            .Where(i => d.GlobalId(i) != "")
            .ToDictionary(d.GlobalId, d.Category);
}
