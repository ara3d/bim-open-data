using System.Numerics;
using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>A camera a viewer captured, the comments a reviewer typed, and the model file the
/// topic refers to: written, validated against the BCF 3.0 schemas, read back, and carried
/// through an issues table.</summary>
[TestFixture]
public sealed class BcfCameraCommentTests
{
    private const string Wall = "2O2Fr$t4X7Zf8NOew3FLOH";
    private static readonly DateTimeOffset Noon = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly BcfCamera Camera = new(
        new Vector3(12.5f, -3.25f, 8f), Vector3.Normalize(new Vector3(-1, 2, -0.5f)), Vector3.Normalize(new Vector3(0.2f, 0.4f, 2)), 47.5, 1.6);

    private static readonly BcfOptions WithModel = BcfArchive.Fixed with
    {
        ModelFile = @"C:\models\duplex.ifc",
        ModelDate = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero),
    };

    private static readonly IReadOnlyList<BcfIssue> Issues =
    [
        new("Wall is too thin", [Wall], "Under 200 mm", Camera: Camera,
            Comments: [new("ann@example.com", Noon, "Please check the fire rating"), new("", null, "Checked: 150 mm")]),
    ];

    private static IReadOnlyList<BcfTopicRecord> ReadBack(BcfArchive archive)
    {
        using var stream = new MemoryStream(archive.Bytes);
        var contents = BcfReader.Read(stream);
        Assert.That(contents.Warnings, Is.Empty);
        return contents.Topics;
    }

    [Test]
    public void ACameraTwoCommentsAndAModelFileValidateAndComeBack()
    {
        var (archive, summary) = BcfArchive.Write(Issues, WithModel);
        Assert.That(archive.SchemaErrors(), Is.Empty);
        Assert.That(summary.Viewpoints, Is.EqualTo(1), "a given camera is a viewpoint even without geometry");

        var topic = ReadBack(archive).Single();
        Assert.Multiple(() =>
        {
            Assert.That(topic.HasViewpoint, Is.True);
            Assert.That(topic.GlobalIds, Is.EqualTo(new[] { Wall }));
            Assert.That(topic.Description, Is.EqualTo("Under 200 mm"), "ids are in the viewpoint, not the description");
            Assert.That(topic.ModelFile, Is.EqualTo("duplex.ifc"));
            Assert.That(topic.Comments, Has.Count.EqualTo(2));
            Assert.That(topic.Comments[0], Is.EqualTo(new BcfComment("ann@example.com", Noon, "Please check the fire rating")));
            Assert.That(topic.Comments[1].Text, Is.EqualTo("Checked: 150 mm"));
            Assert.That(topic.Comments[1].Author, Is.EqualTo(WithModel.Author), "an empty author is the options'");
            Assert.That(topic.Comments[1].Date, Is.EqualTo(WithModel.CreationDate), "no date is the options'");
            AssertSameCamera(topic.Camera, Camera);
        });
    }

    [Test]
    public void TheHeaderNamesTheFileDateAndReferenceAndEachCommentPointsAtTheViewpoint()
    {
        var (archive, _) = BcfArchive.Write(Issues, WithModel);
        var markup = archive.Markups.Single().Root!;
        var file = markup.Element("Header")!.Element("Files")!.Element("File")!;
        var topic = markup.Element("Topic")!;
        var viewpoint = (string)topic.Element("Viewpoints")!.Element("ViewPoint")!.Attribute("Guid")!;
        var comments = topic.Element("Comments")!.Elements("Comment").ToList();
        Assert.Multiple(() =>
        {
            Assert.That((string)file.Attribute("IsExternal")!, Is.EqualTo("true"));
            Assert.That((string)file.Element("Filename")!, Is.EqualTo("duplex.ifc"));
            Assert.That((string)file.Element("Date")!, Is.EqualTo("2026-09-01T08:30:00Z"));
            Assert.That((string)file.Element("Reference")!, Is.EqualTo(@"C:\models\duplex.ifc"));
            Assert.That(comments.Select(c => (string)c.Element("Viewpoint")!.Attribute("Guid")!), Is.All.EqualTo(viewpoint));
            Assert.That(comments.Select(c => (string)c.Attribute("Guid")!),
                Is.EqualTo(new[] { BcfGuid.Comment(BcfGuid.Topic("", "Wall is too thin"), 0), BcfGuid.Comment(BcfGuid.Topic("", "Wall is too thin"), 1) }));
        });
    }

    [Test]
    public void WithoutAModelFileThereIsNoHeader()
    {
        var (archive, _) = BcfArchive.Write(Issues);
        Assert.That(archive.Markups.Single().Root!.Element("Header"), Is.Null);
        Assert.That(ReadBack(archive).Single().ModelFile, Is.Empty);
    }

    [Test]
    public void ACommentOnATopicWithoutACameraHasNoViewpointReference()
    {
        var (archive, _) = BcfArchive.Write([new BcfIssue("Note", [], Comments: [new("", null, "Hello")])]);
        Assert.That(archive.SchemaErrors(), Is.Empty);
        var topic = ReadBack(archive).Single();
        Assert.Multiple(() =>
        {
            Assert.That(topic.HasViewpoint, Is.False);
            Assert.That(topic.Camera, Is.Null);
            Assert.That(topic.Comments.Select(c => c.Text), Is.EqualTo(new[] { "Hello" }));
        });
    }

    [Test]
    public void AGivenCameraWinsOverTheFramedOne()
    {
        var (archive, _) = BcfArchive.Write(Issues, bounds: ElementBounds.FromBimData(
            new Ara3D.Utils.FilePath(BimOpenData.TestSupport.RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip()));
        AssertSameCamera(ReadBack(archive).Single().Camera, Camera);
    }

    [Test]
    public void TheSameInputGivesTheSameBytes()
    {
        var first = BcfArchive.Write(Issues, WithModel).Archive.Bytes;
        var second = BcfArchive.Write(Issues, WithModel).Archive.Bytes;
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ATableWithCameraAndCommentColumnsRoundTripsThroughToBcfIssues()
    {
        var table = IssueTable(
            [Wall, Wall], ["Wall is too thin", "Wall is too thin"], ["Please check", "Please check"],
            new object?[] { 12.5, 12.5 }, comments: ["First", "Second"], authors: ["ann", "bob"],
            dates: ["2026-10-18T12:00:00Z", "2026-10-18T13:00:00Z"]);
        var issue = table.ToBcfIssues().Single();
        Assert.Multiple(() =>
        {
            Assert.That(issue.GlobalIds, Is.EqualTo(new[] { Wall }));
            AssertSameCamera(issue.Camera, new BcfCamera(new(12.5f, 2, 3), new(0, 1, 0), new(0, 0, 1), 47.5, 1.6));
            Assert.That(issue.Comments!.Select(c => (c.Author, c.Text)), Is.EqualTo(new[] { ("ann", "First"), ("bob", "Second") }));
            Assert.That(issue.Comments![1].Date, Is.EqualTo(Noon.AddHours(1)));
        });

        var (archive, _) = BcfArchive.Write([issue], WithModel);
        Assert.That(archive.SchemaErrors(), Is.Empty);
        var back = ReadBack(archive).Single();
        Assert.That(back.Comments.Select(c => c.Text), Is.EqualTo(new[] { "First", "Second" }));
    }

    [Test]
    public void RepeatedCommentTextOfOneTitleIsWrittenOnce()
    {
        var issue = IssueTable(
            [Wall, "1hOSvn6df7F8_7GcBWlRGQ"], ["Walls", "Walls"], ["", ""],
            new object?[] { 12.5, 12.5 }, comments: ["Same", "Same"], authors: ["ann", "ann"], dates: ["", ""]).ToBcfIssues().Single();
        Assert.That(issue.Comments, Has.Count.EqualTo(1));
    }

    [Test]
    public void TwoCamerasForOneTitleThrowAnArgumentExceptionNamingTheTitle()
    {
        var table = IssueTable(
            [Wall, Wall], ["Wall is too thin", "Wall is too thin"], ["", ""],
            new object?[] { 12.5, 13.5 }, comments: ["", ""], authors: ["", ""], dates: ["", ""]);
        Assert.That(() => table.ToBcfIssues(), Throws.ArgumentException.With.Message.Contains("Wall is too thin"));
    }

    [Test]
    public void ARowWithoutCameraCellsHasNoCamera()
    {
        var issue = IssueTable(
            [Wall], ["Walls"], [""], new object?[] { null }, comments: [""], authors: [""], dates: [""]).ToBcfIssues().Single();
        Assert.Multiple(() =>
        {
            Assert.That(issue.Camera, Is.Null);
            Assert.That(issue.Comments, Is.Null);
        });
    }

    [Test]
    public void ToTableCarriesTheCameraAndCommentCountAndCommentsTableListsEachComment()
    {
        var topics = ReadBack(BcfArchive.Write(Issues, WithModel).Archive);
        var table = topics.ToTable();
        var comments = topics.CommentsTable();
        Assert.Multiple(() =>
        {
            Assert.That(Number(table, "CameraX", 0), Is.EqualTo(12.5).Within(1e-5));
            Assert.That(Number(table, "FieldOfView", 0), Is.EqualTo(47.5).Within(1e-5));
            Assert.That(table[table.Columns.ToList().FindIndex(c => c.Descriptor.Name == "Comments"), 0], Is.EqualTo(2));
            Assert.That(comments.Columns.Select(c => c.Descriptor.Name),
                Is.EqualTo(new[] { "TopicGuid", "Title", "Author", "CreationDate", "Comment" }));
            Assert.That(comments.Rows, Has.Count.EqualTo(2));
            Assert.That(comments[4, 0], Is.EqualTo("Please check the fire rating"));
            Assert.That(comments[3, 0], Is.EqualTo("2026-10-18T12:00:00Z"));
        });
    }

    [Test]
    public void ToTableLeavesTheCameraNullWithoutAViewpointCamera()
    {
        var topics = ReadBack(BcfArchive.Write([new BcfIssue("Note", [Wall])]).Archive);
        var table = topics.ToTable();
        Assert.That(table[table.Columns.ToList().FindIndex(c => c.Descriptor.Name == "CameraX"), 0], Is.Null.Or.EqualTo(DBNull.Value));
    }

    private static double Number(IDataTable table, string name, int row)
        => Convert.ToDouble(table[table.Columns.ToList().FindIndex(c => c.Descriptor.Name == name), row]);

    private static void AssertSameCamera(BcfCamera? actual, BcfCamera expected)
    {
        Assert.That(actual, Is.Not.Null);
        var a = actual!.Value;
        Assert.Multiple(() =>
        {
            Assert.That(Vector3.Distance(a.ViewPoint, expected.ViewPoint), Is.LessThan(1e-5f), "position");
            Assert.That(Vector3.Distance(a.Direction, expected.Direction), Is.LessThan(1e-5f), "direction");
            Assert.That(Vector3.Distance(a.Up, expected.Up), Is.LessThan(1e-5f), "up");
            Assert.That(a.FieldOfView, Is.EqualTo(expected.FieldOfView).Within(1e-5));
            Assert.That(a.AspectRatio, Is.EqualTo(expected.AspectRatio).Within(1e-5));
        });
    }

    /// <summary>An issues table with the camera at (x, 2, 3) looking along +y, up +z, field of view
    /// 47.5 and aspect 1.6, where x comes from <paramref name="x"/> (null makes the row's camera empty).</summary>
    private static IDataTable IssueTable(
        string[] ids, string[] titles, string[] descriptions, object?[] x,
        string[] comments, string[] authors, string[] dates)
    {
        var builder = new DataTableBuilder("issues");
        builder.AddColumn(ids.Cast<object?>().ToArray(), "GlobalId", typeof(string));
        builder.AddColumn(titles.Cast<object?>().ToArray(), "Title", typeof(string));
        builder.AddColumn(descriptions.Cast<object?>().ToArray(), "Description", typeof(string));
        builder.AddColumn(comments.Cast<object?>().ToArray(), "Comment", typeof(string));
        builder.AddColumn(authors.Cast<object?>().ToArray(), "Author", typeof(string));
        builder.AddColumn(dates.Cast<object?>().ToArray(), "CreationDate", typeof(string));
        Number(builder, "CameraX", x);
        Number(builder, "CameraY", x.Select(v => v is null ? null : (object?)2.0).ToArray());
        Number(builder, "CameraZ", x.Select(v => v is null ? null : (object?)3.0).ToArray());
        Number(builder, "DirectionX", x.Select(v => v is null ? null : (object?)0.0).ToArray());
        Number(builder, "DirectionY", x.Select(v => v is null ? null : (object?)1.0).ToArray());
        Number(builder, "DirectionZ", x.Select(v => v is null ? null : (object?)0.0).ToArray());
        Number(builder, "UpX", x.Select(v => v is null ? null : (object?)0.0).ToArray());
        Number(builder, "UpY", x.Select(v => v is null ? null : (object?)0.0).ToArray());
        Number(builder, "UpZ", x.Select(v => v is null ? null : (object?)1.0).ToArray());
        Number(builder, "FieldOfView", x.Select(v => v is null ? null : (object?)47.5).ToArray());
        Number(builder, "AspectRatio", x.Select(v => v is null ? null : (object?)1.6).ToArray());
        return builder.Build();
    }

    private static void Number(DataTableBuilder builder, string name, object?[] values)
        => builder.AddColumn(values, name, typeof(double));
}
