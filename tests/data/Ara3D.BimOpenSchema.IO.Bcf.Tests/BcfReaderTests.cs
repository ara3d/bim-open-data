using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>What the writer writes, the reader reads back: titles, fields, and the GlobalIds of
/// each topic, from the viewpoints when there is geometry and from the description when not.</summary>
[TestFixture]
public sealed class BcfReaderTests
{
    private static readonly IReadOnlyList<BcfIssue> Issues =
    [
        new("Doors too narrow", ["1aj$VJZFn2TxepZUBcKp$i", "1s1jVhK8z0pgKYcr9jt781"], "Leaf under 850 mm", "Open", "High", "Issue"),
        new("Missing fire rating", ["2OBrcmyk58NupXoVOHUuXp"], Status: "Active"),
        new("Model-level note", []),
    ];

    private static IReadOnlyList<BcfTopicRecord> RoundTrip(ElementBounds? bounds = null)
    {
        using var stream = new MemoryStream();
        BcfWriter.Write(stream, Issues, BcfArchive.Fixed, bounds);
        stream.Position = 0;
        Assert.That(BcfReader.Version(stream), Is.EqualTo("3.0"));
        stream.Position = 0;
        return BcfReader.Read(stream);
    }

    [Test]
    public void WithoutGeometry_ReadsFieldsAndTheIdsFromTheDescription()
    {
        var topics = RoundTrip().ToDictionary(t => t.Title);
        Assert.That(topics.Keys, Is.EquivalentTo(Issues.Select(i => i.Title)));
        var narrow = topics["Doors too narrow"];
        Assert.Multiple(() =>
        {
            Assert.That(narrow.GlobalIds, Is.EqualTo(Issues[0].GlobalIds));
            Assert.That(narrow.HasViewpoint, Is.False);
            Assert.That(narrow.Description, Does.StartWith("Leaf under 850 mm"));
            Assert.That(narrow.Status, Is.EqualTo("Open"));
            Assert.That(narrow.Priority, Is.EqualTo("High"));
            Assert.That(narrow.Type, Is.EqualTo("Issue"));
            Assert.That(narrow.Author, Is.EqualTo(BcfArchive.Fixed.Author));
            Assert.That(narrow.CreationDate, Is.EqualTo(BcfArchive.Fixed.CreationDate));
            Assert.That(narrow.Guid, Is.EqualTo(BcfGuid.Topic("", "Doors too narrow")));
            Assert.That(topics["Missing fire rating"].Status, Is.EqualTo("Active"));
            Assert.That(topics["Missing fire rating"].Type, Is.EqualTo(BcfArchive.Fixed.DefaultTopicType));
            Assert.That(topics["Model-level note"].GlobalIds, Is.Empty);
        });
    }

    [Test]
    public void WithGeometry_ReadsTheIdsFromTheViewpoints()
    {
        var data = new FilePath(RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip();
        var topics = RoundTrip(ElementBounds.FromBimData(data)).ToDictionary(t => t.Title);
        Assert.Multiple(() =>
        {
            Assert.That(topics["Doors too narrow"].HasViewpoint, Is.True);
            Assert.That(topics["Doors too narrow"].GlobalIds, Is.EquivalentTo(Issues[0].GlobalIds));
            Assert.That(topics["Doors too narrow"].Description, Is.EqualTo("Leaf under 850 mm"), "no id list when a viewpoint holds them");
            Assert.That(topics["Model-level note"].HasViewpoint, Is.False);
        });
    }

    [Test]
    public void ToTable_OneRowPerTopicAndElement_WithTheIssueColumns()
    {
        var table = RoundTrip().ToTable();
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[]
        {
            IssueColumns.GlobalId, IssueColumns.Title, IssueColumns.Description, IssueColumns.Status,
            IssueColumns.Priority, IssueColumns.Type, "TopicGuid", "Author", "CreationDate",
        }));
        Assert.That(table.Rows, Has.Count.EqualTo(4), "two doors, one wall, one model-level row");
        var titles = Enumerable.Range(0, 4).Select(r => (string?)table[1, r]).ToList();
        Assert.That(titles.Count(t => t == "Doors too narrow"), Is.EqualTo(2));
        var note = Enumerable.Range(0, 4).Single(r => (string?)table[1, r] == "Model-level note");
        Assert.That(table[0, note], Is.EqualTo(""));
        Assert.That(table[8, 0], Is.EqualTo("2026-10-10T12:00:00Z"));
    }

    [Test]
    public void ReadFile_ReadsWhatWriteFileWrote()
    {
        var path = Path.Combine(Path.GetTempPath(), "bcf-reader-" + Guid.NewGuid().ToString("N"), "issues.bcf");
        try
        {
            BcfWriter.WriteFile(path, Issues, BcfArchive.Fixed);
            Assert.That(BcfReader.ReadFile(path).Select(t => t.Title), Is.EquivalentTo(Issues.Select(i => i.Title)));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
