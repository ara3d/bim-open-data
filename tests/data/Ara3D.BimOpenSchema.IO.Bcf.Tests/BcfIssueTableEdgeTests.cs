using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>Cell values of an issues table that need a decision: dates without a zone, numbers
/// that are not numbers, a camera with some of its cells, and comments that repeat.</summary>
[TestFixture]
public sealed class BcfIssueTableEdgeTests
{
    private const string Wall = "2O2Fr$t4X7Zf8NOew3FLOH";

    private static IDataTable Table(params (string Name, Type Type, object?[] Values)[] columns)
    {
        var builder = new DataTableBuilder("issues");
        foreach (var (name, type, values) in columns)
            builder.AddColumn(values, name, type);
        return builder.Build();
    }

    private static (string, Type, object?[]) Text(string name, params string[] values)
        => (name, typeof(string), values.Cast<object?>().ToArray());

    [Test]
    public void AnUnspecifiedDateTimeCellIsUtcNotLocal()
    {
        var table = Table(
            Text("GlobalId", Wall), Text("Title", "Wall"), Text("Comment", "Hi"),
            ("CreationDate", typeof(DateTime), [new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Unspecified)]));
        var date = table.ToBcfIssues().Single().Comments!.Single().Date;
        Assert.That(date, Is.EqualTo(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));
    }

    [TestCase("n/a")]
    [TestCase("Infinity")]
    [TestCase("1e400")]
    [TestCase("NaN")]
    public void ACameraCellThatIsNotAFiniteNumberThrowsNamingRowTitleColumnAndText(string cell)
    {
        var table = Table(
            Text("GlobalId", Wall), Text("Title", "Walls"),
            ("CameraX", typeof(string), [cell]), ("CameraY", typeof(double), [2.0]), ("CameraZ", typeof(double), [3.0]),
            ("DirectionX", typeof(double), [0.0]), ("DirectionY", typeof(double), [1.0]), ("DirectionZ", typeof(double), [0.0]),
            ("UpX", typeof(double), [0.0]), ("UpY", typeof(double), [0.0]), ("UpZ", typeof(double), [1.0]));
        var e = Assert.Throws<ArgumentException>(() => table.ToBcfIssues())!;
        Assert.That(e.Message, Does.Contain("Row 0").And.Contain("Walls").And.Contain("CameraX").And.Contain($"'{cell}'"));
    }
}
