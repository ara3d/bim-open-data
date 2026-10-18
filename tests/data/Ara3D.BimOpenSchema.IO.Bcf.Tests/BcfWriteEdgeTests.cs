namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>Inputs at the edge of what the BCF 3.0 schemas accept: the written files must still
/// validate, and a read-then-write of what the reader keeps must not break them.</summary>
[TestFixture]
public sealed class BcfWriteEdgeTests
{
    [Test]
    public void ACommentWithoutTextWritesNoCommentElementAndStillValidates()
    {
        var issue = new BcfIssue("Note", [], Comments: [new BcfComment("", null, ""), new BcfComment("  ann ", null, "  ")]);
        var (archive, _) = BcfArchive.Write([issue]);
        var comments = archive.Markups.Single().Root!.Element("Topic")!.Element("Comments")!.Elements("Comment").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(comments, Has.Count.EqualTo(2));
            Assert.That(comments.Select(c => c.Element("Comment")), Is.All.Null);
            Assert.That((string)comments[1].Element("Author")!, Is.EqualTo("ann"), "the author is trimmed");
        });
    }

    [Test]
    public void ABlankCommentAuthorFallsBackToTheOptionsAuthor()
    {
        var (archive, _) = BcfArchive.Write([new BcfIssue("Note", [], Comments: [new BcfComment("   ", null, "Hi")])]);
        var comment = archive.Markups.Single().Root!.Element("Topic")!.Element("Comments")!.Element("Comment")!;
        Assert.That((string)comment.Element("Author")!, Is.EqualTo(BcfArchive.Fixed.Author));
    }

    [Test]
    public void AModelFileEndingInASeparatorWritesNoFilenameButStillValidates()
    {
        var (archive, _) = BcfArchive.Write([new BcfIssue("Note", [])], BcfArchive.Fixed with { ModelFile = @" C:\models\ " });
        var file = archive.Markups.Single().Root!.Element("Header")!.Element("Files")!.Element("File")!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(file.Element("Filename"), Is.Null);
            Assert.That((string)file.Element("Reference")!, Is.EqualTo(@"C:\models\"), "the reference is trimmed");
        });
    }

    [Test]
    public void ABlankModelFileWritesNoHeader()
    {
        var (archive, _) = BcfArchive.Write([new BcfIssue("Note", [])], BcfArchive.Fixed with { ModelFile = "  " });
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(archive.Markups.Single().Root!.Element("Header"), Is.Null);
        });
    }
}
