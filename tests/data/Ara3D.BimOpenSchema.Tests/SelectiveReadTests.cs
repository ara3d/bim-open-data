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
        Assert.That(partial.Entities, Is.EqualTo(full.Entities), "Parameter.Entity indexes Entities");
        Assert.That(partial.Documents, Is.EqualTo(full.Documents));
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

    [TestCase(BosTables.Entities)]
    [TestCase(BosTables.Documents)]
    [TestCase(BosTables.Descriptors)]
    [TestCase(BosTables.Parameters)]
    [TestCase(BosTables.Relations)]
    [TestCase(BosTables.Diagnostics)]
    [TestCase(BosTables.Geometry)]
    public static void EveryIndexInTheSelectedRowsResolvesToARowThatWasRead(BosTables selected)
    {
        var d = Schependomlaan.ReadBimDataFromParquetZip(selected);
        var full = Schependomlaan.ReadBimDataFromParquetZip();
        var ownRows = selected switch
        {
            BosTables.Entities => d.Entities.Length,
            BosTables.Documents => d.Documents.Length,
            BosTables.Descriptors => d.Descriptors.Length,
            BosTables.Parameters => d.Parameters.Length,
            BosTables.Relations => d.Relations.Length,
            BosTables.Diagnostics => d.Diagnostics.Length,
            _ => d.Geometry.InstanceEntityIndex.Length,
        };
        if (selected != BosTables.Diagnostics)
            Assert.That(ownRows, Is.GreaterThan(0), $"{selected}: the sample must have rows for the check to mean anything");

        void Resolves(int index, int rows, string what)
        {
            if (index >= 0) Assert.That(index, Is.LessThan(rows), $"{selected}: {what} {index}");
        }

        foreach (var e in d.Entities)
        {
            Resolves((int)e.GlobalId, d.Strings.Length, "Entity.GlobalId");
            Resolves((int)e.Name, d.Strings.Length, "Entity.Name");
            Resolves((int)e.Document, d.Documents.Length, "Entity.Document");
            Resolves((int)e.Category, d.Entities.Length, "Entity.Category");
            Resolves((int)e.Type, d.Entities.Length, "Entity.Type");
        }
        foreach (var x in d.Documents)
        {
            Resolves((int)x.Title, d.Strings.Length, "Document.Title");
            Resolves((int)x.Path, d.Strings.Length, "Document.Path");
        }
        foreach (var x in d.Descriptors)
        {
            Resolves((int)x.Name, d.Strings.Length, "Descriptor.Name");
            Resolves((int)x.Units, d.Strings.Length, "Descriptor.Units");
            Resolves((int)x.Group, d.Strings.Length, "Descriptor.Group");
        }
        foreach (var x in d.Parameters)
        {
            Resolves((int)x.Entity, d.Entities.Length, "Parameter.Entity");
            Resolves((int)x.Descriptor, d.Descriptors.Length, "Parameter.Descriptor");
            var rows = d.Descriptors[(int)x.Descriptor].Type switch
            {
                ParameterType.Entity => d.Entities.Length,
                ParameterType.String => d.Strings.Length,
                ParameterType.Number => d.Numbers.Length,
                ParameterType.Point => d.Points.Length,
                _ => int.MaxValue,
            };
            Resolves(x.Value, rows, "Parameter.Value");
        }
        foreach (var x in d.Relations)
        {
            Resolves((int)x.EntityA, d.Entities.Length, "Relation.EntityA");
            Resolves((int)x.EntityB, d.Entities.Length, "Relation.EntityB");
        }
        foreach (var x in d.Diagnostics)
        {
            Resolves((int)x.Document, d.Documents.Length, "Diagnostic.Document");
            Resolves((int)x.Entity, d.Entities.Length, "Diagnostic.Entity");
            Resolves((int)x.Message, d.Strings.Length, "Diagnostic.Message");
        }
        foreach (var i in d.Geometry.InstanceEntityIndex)
            Resolves(i, d.Entities.Length, "Instance entity");

        // The tables that the closure pulled in are whole, not truncated.
        if (d.Entities.Length > 0) Assert.That(d.Entities, Is.EqualTo(full.Entities));
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
