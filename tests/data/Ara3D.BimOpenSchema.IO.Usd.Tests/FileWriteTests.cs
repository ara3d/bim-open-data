namespace Ara3D.BimOpenSchema.IO.Usd.Tests;

/// <summary>WriteUsda(path) replaces the file only when the whole stage was written: a write
/// that fails partway leaves the earlier file and no temporary file behind.</summary>
[TestFixture]
public sealed class FileWriteTests
{
    private string _folder = "";

    [SetUp]
    public void CreateFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bos-usd-tests", "file-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

    /// <summary>One triangle mesh. With <paramref name="broken"/> the Y column is shorter than
    /// X, so writing the mesh points throws after the header, materials, and the start of the
    /// prototypes are already written.</summary>
    private static BimData Triangle(bool broken) => new()
    {
        Geometry = new BimGeometry
        {
            VertexX = [0, 10_000, 0],
            VertexY = broken ? [0] : [0, 0, 10_000],
            VertexZ = [0, 0, 0],
            IndexBuffer = [0, 1, 2],
            MeshVertexOffset = [0],
            MeshIndexOffset = [0],
        },
    };

    private string[] TemporaryFiles() => Directory.GetFiles(_folder, "*.tmp");

    [Test]
    public void FailedWrite_LeavesTheEarlierFileAndNoTemporaryFile()
    {
        var path = Path.Combine(_folder, "model.usda");
        File.WriteAllText(path, "earlier");

        Assert.Throws<IndexOutOfRangeException>(() => Triangle(broken: true).WriteUsda(path));

        Assert.That(File.ReadAllText(path), Is.EqualTo("earlier"));
        Assert.That(TemporaryFiles(), Is.Empty);
    }

    [Test]
    public void FailedWrite_CreatesNoFileWhenNoneExisted()
    {
        var path = Path.Combine(_folder, "model.usda");

        Assert.Throws<IndexOutOfRangeException>(() => Triangle(broken: true).WriteUsda(path));

        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }

    [Test]
    public void SuccessfulWrite_ReplacesTheEarlierFile()
    {
        var path = Path.Combine(_folder, "model.usda");
        File.WriteAllText(path, "earlier");

        var summary = Triangle(broken: false).WriteUsda(path);

        Assert.That(summary.Prototypes, Is.EqualTo(1));
        Assert.That(File.ReadAllText(path), Does.StartWith("#usda 1.0\n"));
        Assert.That(TemporaryFiles(), Is.Empty);
    }
}
