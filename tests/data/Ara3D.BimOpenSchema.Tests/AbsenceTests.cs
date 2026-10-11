using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;

namespace Ara3D.BIMOpenSchema.Tests;

/// <summary>
/// An index of -1 means absent (BimOpenSchema.cs): the builder stores a null GlobalId, name,
/// or parameter value as -1 or as no row, "" stays a value, and the accessors return null for
/// an absent or out-of-range index instead of 0, "", or an exception.
/// </summary>
public static class AbsenceTests
{
    private static (BimDataBuilder Builder, EntityIndex Unnamed, EntityIndex Named) BuilderWithUnnamedEntity()
    {
        var bdb = new BimDataBuilder();
        var doc = bdb.AddDocument("doc", null);
        var unnamed = bdb.AddEntity(-1, null, doc, null, BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex);
        var named = bdb.AddEntity(7, "", doc, "", unnamed, BimDataBuilder.InvalidEntityIndex);
        return (bdb, unnamed, named);
    }

    [Test]
    public static void NullGlobalIdAndNameAreStoredAsMinusOne()
    {
        var (bdb, unnamed, _) = BuilderWithUnnamedEntity();
        var data = bdb.Build();
        var e = data.Entities[(int)unnamed];
        Assert.That(e.GlobalId, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(e.Name, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(data.Documents[0].Path, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(data.Strings, Does.Not.Contain(null));
    }

    [Test]
    public static void EmptyStringIsAStoredValue()
    {
        var (bdb, _, named) = BuilderWithUnnamedEntity();
        var data = bdb.Build();
        var e = data.Entities[(int)named];
        Assert.That(e.GlobalId, Is.Not.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(data.Get(e.GlobalId), Is.EqualTo(""));
    }

    [Test]
    public static void UpdateEntityStoresNullAsMinusOne()
    {
        var bdb = new BimDataBuilder();
        var e = bdb.AddEntity();
        bdb.UpdateEntity(e, 3, null, BimDataBuilder.InvalidDocumentIndex, "Door", BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex);
        var data = bdb.Build();
        Assert.That(data.Entities[0].GlobalId, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(data.Name((EntityIndex)0), Is.EqualTo("Door"));
    }

    [Test]
    public static void NullStringParameterAddsNoRowAndEmptyStringAddsOne()
    {
        var (bdb, unnamed, _) = BuilderWithUnnamedEntity();
        bdb.AddParameter(unnamed, (string?)null, "Comment", null, "Text");
        bdb.AddParameter(unnamed, "", "Mark", null, "Text");
        var data = bdb.Build();
        Assert.That(data.Parameters, Has.Length.EqualTo(1));
        Assert.That(data.ParameterName(data.Parameters[0]), Is.EqualTo("Mark"));
        Assert.That(data.ParameterValue(data.Parameters[0]), Is.EqualTo(""));
    }

    [Test]
    public static void AccessorsReturnNullForMinusOneAndOutOfRange()
    {
        var data = BimDataBuilderTests.CreateSimpleData();
        foreach (var i in new[] { -1, 1_000_000 })
        {
            Assert.That(data.Get((StringIndex)i), Is.Null, $"string {i}");
            Assert.That(data.Get((NumberIndex)i), Is.Null, $"number {i}");
            Assert.That(data.Get((PointIndex)i), Is.Null, $"point {i}");
            Assert.That(data.Get((EntityIndex)i), Is.Null, $"entity {i}");
            Assert.That(data.Get((DocumentIndex)i), Is.Null, $"document {i}");
            Assert.That(data.Get((DescriptorIndex)i), Is.Null, $"descriptor {i}");
            Assert.That(data.Get((ParameterIndex)i), Is.Null, $"parameter {i}");
            Assert.That(data.Get((RelationIndex)i), Is.Null, $"relation {i}");
            Assert.That(data.EntityName((EntityIndex)i), Is.Null, $"entity name {i}");
        }
    }

    [Test]
    public static void NumberParameterPointingNowhereHasNoValue()
    {
        var bdb = new BimDataBuilder();
        var e = bdb.AddEntity(1, "g", BimDataBuilder.InvalidDocumentIndex, "e", BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex);
        var d = bdb.AddDescriptor("Area", "m2", "G", ParameterType.Number);
        bdb.AddParameter(e, (int)BimDataBuilder.InvalidNumberIndex, d);
        var data = bdb.Build();
        Assert.That(data.ParameterValue(data.Parameters[0]), Is.Null);
    }

    /// <summary>Files written before -1 meant absent store an unset name as "": names read it as absent.</summary>
    [Test]
    public static void NamesReadEmptyStringAsAbsent()
    {
        var (bdb, _, named) = BuilderWithUnnamedEntity();
        var data = bdb.Build();
        Assert.That(data.Name(named), Is.Null);
        Assert.That(data.Label(data.Entities[(int)named].GlobalId), Is.Null);
        Assert.That(data.GetEntityLabel(named), Is.EqualTo($"[{named}]"));
    }

    [Test]
    public static void MinusOneSurvivesAddBimData()
    {
        var (source, unnamed, _) = BuilderWithUnnamedEntity();
        var bdb = new BimDataBuilder();
        bdb.AddString("shifts every string index");
        bdb.AddBimData(source.Build());
        var e = bdb.Build().Entities[(int)unnamed];
        Assert.That(e.GlobalId, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
        Assert.That(e.Name, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
    }

    [Test]
    public static void MinusOneSurvivesParquetRoundTrip()
    {
        var (bdb, unnamed, _) = BuilderWithUnnamedEntity();
        var fp = ParquetParameterTypeTests.WriteToTempBosZip(bdb.Build());
        try
        {
            var data = fp.ReadBimDataFromParquetZip();
            Assert.That(data.Entities[(int)unnamed].GlobalId, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
            Assert.That(data.Entities[(int)unnamed].Name, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
            Assert.That(data.Name(unnamed), Is.Null);
        }
        finally
        {
            File.Delete(fp);
        }
    }
}
