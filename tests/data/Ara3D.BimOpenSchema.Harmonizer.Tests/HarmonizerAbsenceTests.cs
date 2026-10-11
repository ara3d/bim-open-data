using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace Ara3D.BimOpenSchema.Harmonizer.Tests;

/// <summary>Harmonizing data whose string, number, and name indices are -1 (absent, as the
/// specification defines it): nothing throws, and an absent value yields no canonical value.</summary>
[TestFixture]
public sealed class HarmonizerAbsenceTests
{
    private const BosTables BosLoadTables = BosTables.Entities | BosTables.Parameters | BosTables.Relations;

    private static (BimData Data, EntityIndex Wall, EntityIndex Storey) BuildIfcLikeData()
    {
        var bdb = new BimDataBuilder();
        var doc = bdb.AddDocument("doc", null);
        var none = BimDataBuilder.InvalidEntityIndex;
        var wallCategory = bdb.AddEntity(-1, null, doc, "IFCWALL", none, none);
        var storeyCategory = bdb.AddEntity(-1, null, doc, "IFCBUILDINGSTOREY", none, none);
        var unnamedCategory = bdb.AddEntity(-1, null, doc, null, none, none);
        var storey = bdb.AddEntity(1, null, doc, null, storeyCategory, none);
        var wall = bdb.AddEntity(2, null, doc, null, wallCategory, none);
        var orphan = bdb.AddEntity(3, null, doc, null, unnamedCategory, none);

        // Descriptors with no group: "Ifc:Room:Number" maps by name alone; "Height" maps
        // only in the Qto_WallBaseQuantities group, so it maps to nothing here.
        var number = bdb.AddDescriptor("Ifc:Room:Number", null, null, ParameterType.String);
        bdb.AddParameter(wall, "101", number);
        bdb.AddParameter(orphan, -1, number); // value index -1: absent string
        var height = bdb.AddDescriptor("Height", null, "Qto_WallBaseQuantities", ParameterType.Number);
        bdb.AddParameter(wall, -1, height); // value index -1: absent number
        var unnamed = bdb.AddDescriptor(null!, null, null, ParameterType.Number);
        bdb.AddParameter(wall, 2.5, unnamed);

        bdb.AddRelation(wall, storey, RelationType.ContainedIn);
        bdb.AddRelation(orphan, wall, RelationType.ContainedIn);
        return (bdb.Build(), wall, storey);
    }

    private static IEnumerable<Parameter> Canonical(IBimData data, string name)
        => data.Parameters.Where(p => data.Get(data.Descriptors[(int)p.Descriptor].Name) == "Bos:" + name);

    [Test]
    public void IsHarmonized_IsFalseWhenNoDescriptorHasAGroup()
        => Assert.That(BosHarmonizer.IsHarmonized(BuildIfcLikeData().Data), Is.False);

    [Test]
    public void DetectSource_SkipsUnnamedCategoriesAndDescriptors()
        => Assert.That(BosHarmonizer.DetectSource(BuildIfcLikeData().Data), Is.EqualTo(SourceKind.Ifc));

    [Test]
    public void Harmonize_AddsNoCanonicalValueForAnAbsentOne()
    {
        var (data, wall, storey) = BuildIfcLikeData();
        var result = BosHarmonizer.Harmonize(data);

        Assert.That(Canonical(result, "Category").Select(p => (p.Entity, result.Get((StringIndex)p.Value))),
            Is.EqualTo(new[] { (storey, (string?)"Level"), (wall, "Wall") }));
        Assert.That(Canonical(result, "Number").Select(p => (p.Entity, result.Get((StringIndex)p.Value))),
            Is.EqualTo(new[] { (wall, (string?)"101") }));
        Assert.That(Canonical(result, "Height"), Is.Empty);
        Assert.That(Canonical(result, "Level").Select(p => (p.Entity, (EntityIndex)p.Value)),
            Is.EqualTo(new[] { (wall, storey) }));
        Assert.That(result.Diagnostics.Select(d => result.Get(d.Message)),
            Has.Some.EqualTo("No canonical category for 1 entities whose category has no name."));
    }

    /// <summary>As bos.load does it: read the entity, parameter, and relation tables of a .bos
    /// file, then harmonize.</summary>
    [Test]
    public void Harmonize_AfterABosLoadStyleRead()
    {
        var (data, wall, _) = BuildIfcLikeData();
        var path = new FilePath(Path.Combine(Path.GetTempPath(), $"harmonizer-absence-{Guid.NewGuid():N}.bos"));
        try
        {
            data.WriteToParquetZip(path);
            var result = BosHarmonizer.Harmonize(path.ReadBimDataFromParquetZip(BosLoadTables));
            Assert.That(Canonical(result, "Number").Select(p => p.Entity), Is.EqualTo(new[] { wall }));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
