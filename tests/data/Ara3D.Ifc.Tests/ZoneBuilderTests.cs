using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>
/// TKT-49: the Duplex has no zones, so NRC's zoning is written into a copy as IfcZone groups. The
/// additions are exactly the zones and their relationships, removing them restores the source byte
/// for byte, and a second build gives the same bytes.
/// </summary>
public static class ZoneBuilderTests
{
    private static string Duplex => RepoPaths.Samples("nrc", "duplex-base.ifc");

    private static readonly (string Zone, string[] SpaceGlobalIds)[] Zoning =
    [
        ("Unit A Level 1", ["0BTBFw6f90Nfh9rP1dlXrr", "0BTBFw6f90Nfh9rP1dlXr2"]),
        ("Unit A Level 2", ["0BTBFw6f90Nfh9rP1dlXri"]),
    ];

    private static IfcZoneBuilder Build(IfcSourceFile file, out int[] zoneIds)
    {
        var spaces = file.GlobalIdToEntityId();
        var builder = new IfcZoneBuilder(file.MaxId + 1, file.FirstIdOfType("IFCOWNERHISTORY"), file.Schema);
        zoneIds = Zoning.Select(z => builder.AddZone(z.Zone, z.SpaceGlobalIds.Select(g => spaces[g]).ToArray())).ToArray();
        return builder;
    }

    [Test]
    public static void SchemaIsReadFromTheHeader()
    {
        using var file = IfcSourceFile.Load(Duplex);
        Assert.That(file.Schema, Is.EqualTo(IfcSchema.Ifc2x3));
    }

    [Test]
    public static void AppendZonesDiffsAndRestores()
    {
        using var original = IfcSourceFile.Load(Duplex);
        var builder = Build(original, out var zoneIds);
        Assert.That(builder.Lines, Has.Count.EqualTo(4), "one IFCZONE and one IFCRELASSIGNSTOGROUP per zone");

        var folder = Path.Combine(Path.GetTempPath(), "ara3d-zone-builder", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var modifiedPath = Path.Combine(folder, "duplex-zones.ifc");
            File.WriteAllBytes(modifiedPath, IfcPatcher.Append(original, builder.Lines));

            using var modified = IfcSourceFile.Load(modifiedPath);
            var diff = IfcDiff.Compare(original, modified);
            Assert.That(diff.Added, Is.EqualTo(builder.Ids));
            Assert.That(diff.Deleted, Is.Empty);
            Assert.That(diff.Changed, Is.Empty);
            Assert.That(zoneIds.Select(id => modified.GetSpan(id)!.Value.TypeName),
                Is.All.EqualTo("IFCZONE"));
            Assert.That(modified.GetSpan(zoneIds[0] + 1)!.Value.TypeName, Is.EqualTo("IFCRELASSIGNSTOGROUP"));

            var restoredPath = Path.Combine(folder, "duplex-zones-restored.ifc");
            File.WriteAllBytes(restoredPath, IfcPatcher.Remove(modified, diff.Added));
            Assert.That(File.ReadAllBytes(restoredPath), Is.EqualTo(File.ReadAllBytes(Duplex)),
                "Removing the added entities must restore the original file byte-for-byte");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public static void TwoBuildsProduceIdenticalLines()
    {
        using var original = IfcSourceFile.Load(Duplex);
        var a = Build(original, out _);
        var b = Build(original, out _);
        Assert.That(b.Lines, Is.EqualTo(a.Lines));
        Assert.That(b.Ids, Is.EqualTo(a.Ids));
    }

    [Test]
    public static void GlobalIdsFollowTheZoneNameNotItsPosition()
    {
        var first = new IfcZoneBuilder(100, 7, IfcSchema.Ifc2x3);
        first.AddZone("Unit A Level 1", [1, 2]);
        var second = new IfcZoneBuilder(500, 7, IfcSchema.Ifc2x3);
        second.AddZone("Unit B Level 1", [3]);
        second.AddZone("Unit A Level 1", [1, 2]);

        Assert.That(GuidOf(second.Lines[2]), Is.EqualTo(GuidOf(first.Lines[0])));
        Assert.That(GuidOf(second.Lines[0]), Is.Not.EqualTo(GuidOf(first.Lines[0])));
        Assert.That(IfcGuid.IsValid(GuidOf(first.Lines[0])), Is.True);
    }

    [Test]
    public static void Ifc2x3LayoutMatchesTheSchema()
    {
        var builder = new IfcZoneBuilder(100, 7, IfcSchema.Ifc2x3);
        var id = builder.AddZone("Unit A Level 1", [42, 43], "Dwelling A, first floor");

        Assert.That(id, Is.EqualTo(100));
        // GlobalId, OwnerHistory, Name, Description, ObjectType.
        Assert.That(builder.Lines[0], Does.StartWith("#100=IFCZONE('"));
        Assert.That(builder.Lines[0], Does.EndWith(",#7,'Unit A Level 1','Dwelling A, first floor',$);"));
        // GlobalId, OwnerHistory, Name, Description, RelatedObjects, RelatedObjectType, RelatingGroup.
        Assert.That(builder.Lines[1], Does.StartWith("#101=IFCRELASSIGNSTOGROUP('"));
        Assert.That(builder.Lines[1], Does.EndWith(",#7,'Unit A Level 1','Dwelling A, first floor',(#42,#43),$,#100);"));
    }

    [Test]
    public static void Ifc4ZoneHasALongNameAttribute()
    {
        var builder = new IfcZoneBuilder(100, 7, IfcSchema.Ifc4);
        builder.AddZone("Z", [42]);

        Assert.That(builder.Lines[0], Does.EndWith(",#7,'Z',$,$,$);"));
        Assert.That(builder.Lines[1], Does.EndWith(",#7,'Z',$,(#42),$,#100);"));
    }

    [Test]
    public static void AZoneNeedsSpaces()
    {
        var builder = new IfcZoneBuilder(100, 7, IfcSchema.Ifc2x3);
        Assert.Throws<ArgumentException>(() => builder.AddZone("Empty", []));
    }

    private static string GuidOf(string line)
        => line.Split('\'')[1];
}
