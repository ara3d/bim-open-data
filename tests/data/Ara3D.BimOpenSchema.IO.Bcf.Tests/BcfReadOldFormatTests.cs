using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>A BCF 2.1 file read and written back: what its camera lacks stays absent until a
/// write needs it.</summary>
[TestFixture]
public sealed class BcfReadOldFormatTests
{
    private static IReadOnlyList<BcfTopicRecord> Read21()
    {
        var contents = BcfReader.Read(HandWrittenBcf.Container21());
        Assert.That(contents.Warnings, Is.Empty);
        return contents.Topics;
    }

    [Test]
    public void AMissingAspectRatioStaysNullInTheRecordAndTheTable()
    {
        var topics = Read21();
        var camera = topics.Single().Camera!.Value;
        var table = topics.ToTable();
        var column = table.Columns.ToList().FindIndex(c => c.Descriptor.Name == BcfColumnNames.AspectRatio);
        Assert.Multiple(() =>
        {
            Assert.That(camera.FieldOfView, Is.EqualTo(47.5));
            Assert.That(camera.AspectRatio, Is.Null);
            Assert.That(table[column, 0], Is.Null.Or.EqualTo(DBNull.Value));
        });
    }

    [Test]
    public void WritingTheTableBackFillsTheMissingAspectRatioFromTheOptionsOnly()
    {
        var issue = Read21().ToTable().ToBcfIssues().Single();
        Assert.That(issue.Camera!.Value.AspectRatio, Is.Null, "reading the table back does not invent one");

        var (archive, _) = BcfArchive.Write([issue], BcfArchive.Fixed with { FieldOfView = 50, AspectRatio = 2 });
        var camera = archive.Viewpoints.Single().Root!.Element("PerspectiveCamera")!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That((double)camera.Element("FieldOfView")!, Is.EqualTo(47.5), "the file's own value is kept");
            Assert.That((double)camera.Element("AspectRatio")!, Is.EqualTo(2), "the options' value fills the gap");
        });
    }
}
