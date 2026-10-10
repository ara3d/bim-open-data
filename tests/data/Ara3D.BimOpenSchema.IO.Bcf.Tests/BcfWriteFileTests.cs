namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>Writing to a path either replaces the file with a complete container or leaves the
/// folder as it was: no truncated target, no partial or temporary file.</summary>
[TestFixture]
public sealed class BcfWriteFileTests
{
    private static readonly byte[] Earlier = "an earlier export"u8.ToArray();
    private string _folder = null!;
    private string _target = null!;

    [SetUp]
    public void CreateFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bcf-writefile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _target = Path.Combine(_folder, "issues.bcf");
    }

    [TearDown]
    public void DeleteFolder()
        => Directory.Delete(_folder, recursive: true);

    private string[] FolderContents()
        => Directory.GetFiles(_folder).Select(Path.GetFileName).ToArray()!;

    [Test]
    public void InvalidIssuesLeaveAnExistingFileUntouched()
    {
        File.WriteAllBytes(_target, Earlier);
        Assert.That(() => BcfWriter.WriteFile(_target, [new BcfIssue("A", []), new BcfIssue("A", [])], BcfArchive.Fixed),
            Throws.ArgumentException);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(Earlier));
            Assert.That(FolderContents(), Is.EqualTo(new[] { "issues.bcf" }));
        });
    }

    [TestCase(true, TestName = "AFailurePartwayLeavesAnExistingFileUntouched")]
    [TestCase(false, TestName = "AFailurePartwayLeavesNoFile")]
    public void AFailurePartwayLeavesNoPartialFile(bool targetExists)
    {
        if (targetExists)
            File.WriteAllBytes(_target, Earlier);
        Assert.That(() => BcfWriter.ReplaceFile<int>(_target, stream =>
        {
            stream.Write(new byte[4096]);
            throw new IOException("disk full");
        }), Throws.TypeOf<IOException>());
        Assert.That(FolderContents(), Is.EqualTo(targetExists ? new[] { "issues.bcf" } : Array.Empty<string>()));
        if (targetExists)
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(Earlier));
    }

    [Test]
    public void ASuccessfulWriteReplacesTheFileWithTheSameBytesAsAStream()
    {
        File.WriteAllBytes(_target, Earlier);
        IReadOnlyList<BcfIssue> issues = [new BcfIssue("A", ["2O2Fr$t4X7Zf8NOew3FLOH"])];
        BcfWriter.WriteFile(_target, issues, BcfArchive.Fixed);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(BcfArchive.Write(issues).Archive.Bytes));
            Assert.That(FolderContents(), Is.EqualTo(new[] { "issues.bcf" }));
        });
    }
}
