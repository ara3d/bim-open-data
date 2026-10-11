using DuckDB.NET.Data;

namespace Ara3D.BimOpenSchema.DuckDb.Tests;

/// <summary>The text views keep an entity whose GlobalId, name, category, or type is absent
/// (index -1) and show NULL for it, instead of dropping the row on a join miss; "" written by
/// files from before -1 meant absent reads as NULL too.</summary>
[TestFixture]
public sealed class BosDuckDbAbsenceTests
{
    private DuckDBConnection _conn = null!;

    /// <summary>Entity 0 has no GlobalId, name, category, or type; entity 1 stores "" for both, as
    /// older files did; entity 2 has an absent Number parameter value and a relation to entity 0.</summary>
    private static BimData BuildData()
    {
        var bdb = new BimDataBuilder();
        var doc = bdb.AddDocument("doc", null);
        var none = BimDataBuilder.InvalidEntityIndex;
        var bare = bdb.AddEntity(-1, null, doc, null, none, none);
        bdb.AddEntity(5, "", doc, "", bare, bare);
        var wall = bdb.AddEntity(6, "guid-wall", doc, "Wall", bare, none);
        bdb.AddParameter(wall, (int)BimDataBuilder.InvalidNumberIndex, bdb.AddDescriptor("Area", "m2", "Dimensions", ParameterType.Number));
        bdb.AddParameter(wall, "", "Mark", null, "Identity");
        bdb.AddParameter(wall, new Point(1.5f, 2f, -3f), "Where", "m", "Geometry");
        bdb.AddParameter(wall, (PointIndex)(-1), "Nowhere", "m", "Geometry");
        bdb.AddRelation(wall, bare, RelationType.ContainedIn);
        return bdb.Build();
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
        => _conn = BuildData().ToDuckDb();

    [OneTimeTearDown]
    public void OneTimeTearDown()
        => _conn.Dispose();

    [Test]
    public void EntityWithoutGlobalIdOrName_KeepsItsRowWithNulls()
    {
        var rows = _conn.Query("SELECT EntityIndex, GlobalId, Name, Category, Type FROM EntityText ORDER BY EntityIndex").Rows;
        Assert.That(rows, Has.Count.EqualTo(3), "no entity is dropped by a join on -1");
        Assert.Multiple(() =>
        {
            for (var i = 1; i <= 4; i++)
                Assert.That(rows[0][i], Is.Null, $"column {i} of the bare entity");
            Assert.That(rows[1][1], Is.Null, "a GlobalId stored as \"\" by an older file");
            Assert.That(rows[1][2], Is.Null, "a name stored as \"\" by an older file");
            Assert.That(rows[2][1], Is.EqualTo("guid-wall"));
            Assert.That(rows[2][3], Is.Null, "the category entity has no name");
        });
    }

    [Test]
    public void AbsentParameterValueIsNull_AndAnEmptyStringValueIsKept()
    {
        var rows = _conn.Query("SELECT Name, Units, Value FROM ParameterText WHERE Name IN ('Area', 'Mark') ORDER BY Name").Rows;
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0][0], Is.EqualTo("Area"));
            Assert.That(rows[0][2], Is.Null);
            Assert.That(rows[1][0], Is.EqualTo("Mark"));
            Assert.That(rows[1][1], Is.Null, "units passed as null are absent");
            Assert.That(rows[1][2], Is.EqualTo(""));
        });
    }

    [Test]
    public void PointParameter_ShowsItsCoordinates_AndAnAbsentPointIsNull()
    {
        var rows = _conn.Query("SELECT Name, Value FROM ParameterText WHERE ValueType = 'Point' ORDER BY Name").Rows;
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0][0], Is.EqualTo("Nowhere"));
            Assert.That(rows[0][1], Is.Null);
            Assert.That(rows[1][0], Is.EqualTo("Where"));
            Assert.That(rows[1][1], Is.EqualTo("Point { X = 1.5, Y = 2.0, Z = -3.0 }"));
        });
    }

    [Test]
    public void RelationToAnUnnamedEntity_KeepsItsRow()
    {
        var rows = _conn.Query("SELECT NameA, NameB, RelationType FROM RelationText").Rows;
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That((rows[0][0], rows[0][1], rows[0][2]), Is.EqualTo(("Wall", (object?)null, "ContainedIn")));
    }
}
