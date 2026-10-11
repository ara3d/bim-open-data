using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BIMOpenSchema.Tests;

/// <summary>Reading a subset of a .bos archive's tables: the tables asked for equal a full
/// read, and the others come back empty.</summary>
public static class SelectiveReadTests
{
    private static FilePath Schependomlaan => new(RepoPaths.Samples("public", "schependomlaan.bos"));

    private static readonly BosTables GeometryAndEntities = BosTables.Geometry | BosTables.Entities;

    [Test]
    public static void GeometryAndEntitiesMatchAFullRead()
    {
        var full = Schependomlaan.ReadBimDataFromParquetZip();
        var partial = Schependomlaan.ReadBimDataFromParquetZip(GeometryAndEntities);

        Assert.That(full.Entities, Is.Not.Empty);
        Assert.That(full.Parameters, Is.Not.Empty, "the sample must have parameters for the empty check to mean anything");
        Assert.That(partial.Entities, Is.EqualTo(full.Entities));
        Assert.That(partial.Strings, Is.EqualTo(full.Strings));
        AssertSameGeometry(partial.Geometry, full.Geometry);
        Assert.That(partial.Parameters, Is.Empty);
    }

    [Test]
    public static void ParametersBringTheTablesTheyIndexInto()
    {
        var full = Schependomlaan.ReadBimDataFromParquetZip();
        var partial = Schependomlaan.ReadBimDataFromParquetZip(BosTables.Parameters);

        Assert.That(partial.Parameters, Is.EqualTo(full.Parameters));
        Assert.That(partial.Descriptors, Is.EqualTo(full.Descriptors));
        Assert.That(partial.Strings, Is.EqualTo(full.Strings));
        Assert.That(partial.Numbers, Is.EqualTo(full.Numbers));
        Assert.That(partial.Points, Is.EqualTo(full.Points));
        Assert.That(partial.Entities, Is.Empty);
        Assert.That(partial.Relations, Is.Empty);
        AssertSameGeometry(partial.Geometry, new BimGeometry());
    }

    [Test]
    public static void WithoutGeometryTheGeometryIsEmpty()
    {
        var full = Schependomlaan.ReadBimDataFromParquetZip();
        var partial = Schependomlaan.ReadBimDataFromParquetZip(BosTables.Entities);

        Assert.That(full.Geometry.InstanceEntityIndex, Is.Not.Empty);
        AssertSameGeometry(partial.Geometry, new BimGeometry());
        Assert.That(partial.Entities, Is.EqualTo(full.Entities));
    }

    [Test]
    public static void AllReadsEverythingLikeTheOriginalOverload()
    {
        var full = Schependomlaan.ReadBimDataFromParquetZip();
        var all = Schependomlaan.ReadBimDataFromParquetZip(BosTables.All);

        Assert.That(all.Entities, Is.EqualTo(full.Entities));
        Assert.That(all.Parameters, Is.EqualTo(full.Parameters));
        Assert.That(all.Numbers, Is.EqualTo(full.Numbers));
        Assert.That(all.Strings, Is.EqualTo(full.Strings));
        AssertSameGeometry(all.Geometry, full.Geometry);
    }

    [Test]
    public static void NoneReadsNothing()
    {
        var none = Schependomlaan.ReadBimDataFromParquetZip(BosTables.None);

        Assert.That(none.Entities, Is.Empty);
        Assert.That(none.Strings, Is.Empty);
        Assert.That(none.Numbers, Is.Empty);
        Assert.That(none.Parameters, Is.Empty);
        AssertSameGeometry(none.Geometry, new BimGeometry());
    }

    private static void AssertSameGeometry(BimGeometry actual, BimGeometry expected)
    {
        var arrays = typeof(BimGeometry).GetProperties().Where(p => p.PropertyType.IsArray).ToList();
        Assert.That(arrays, Is.Not.Empty);
        foreach (var p in arrays)
            Assert.That((System.Collections.IEnumerable)p.GetValue(actual)!,
                Is.EqualTo((System.Collections.IEnumerable)p.GetValue(expected)!), p.Name);
    }
}
