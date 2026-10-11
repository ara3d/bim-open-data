using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>The BOS session cache: one session per .bos path, reopened when the file changes,
/// bounded, and shared with the IFC sessions' conversions.</summary>
[TestFixture]
public sealed class BosSessionCacheTests
{
    private string _scratch = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _scratch = RepoPaths.Artifacts("bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void Get_ReturnsTheSameSessionForTheSameFile()
    {
        using var cache = new BosSessionCache();
        var path = RepoPaths.Samples("public", "duplex.bos");
        var session = cache.Get(path);

        Assert.That(cache.Get(path), Is.SameAs(session));
        Assert.That(session.Scene.EntityCount, Is.EqualTo(session.Data.Entities.Length));
    }

    [Test]
    public void Get_MissingFile_Throws()
    {
        using var cache = new BosSessionCache();
        Assert.Throws<FileNotFoundException>(() => cache.Get("C:/no-such-model.bos"));
    }

    [Test]
    public void Get_ReopensAFileThatChanged()
    {
        using var cache = new BosSessionCache();
        var path = Copy("duplex.bos", "changed.bos");
        var first = cache.Get(path);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));

        Assert.That(first.IsCurrent, Is.False);
        Assert.That(cache.Get(path), Is.Not.SameAs(first));
    }

    [Test]
    public void Capacity_EvictsTheLeastRecentlyUsed()
    {
        using var cache = new BosSessionCache(capacity: 1);
        var first = RepoPaths.Samples("public", "duplex.bos");
        var second = RepoPaths.Samples("public", "duplex-electrical.bos");

        cache.Get(first);
        cache.Get(second);

        Assert.That(cache.IsOpen(second), Is.True);
        Assert.That(cache.IsOpen(first), Is.False);
    }

    [Test]
    public void Close_DeletesTheDatabase()
    {
        using var cache = new BosSessionCache();
        var path = RepoPaths.Samples("public", "duplex.bos");
        var database = cache.Get(path).DatabasePath.FullPath;
        Assert.That(File.Exists(database), Is.True);

        Assert.That(cache.Close(path), Is.True);
        Assert.That(File.Exists(database), Is.False);
    }

    [Test]
    public void IfcSession_HoldsItsConversionInTheSharedCache_UntilItCloses()
    {
        using var cache = new IfcSessionCache();
        var ifc = TestModel.RequirePath(TestModel.FzkHaus);
        var session = cache.Get(ifc);
        var bos = session.Bos;

        Assert.That(cache.BosSessions.Get(bos.BosPath.FullPath), Is.SameAs(bos));
        Assert.That(cache.Close(ifc), Is.True);
        Assert.That(cache.BosSessions.IsOpen(bos.BosPath.FullPath), Is.False);
        Assert.That(File.Exists(bos.BosPath.FullPath), Is.False, "the conversion's temp folder goes with the IFC session");
    }

    private string Copy(string sample, string name)
    {
        var path = Path.Combine(_scratch, name);
        File.Copy(RepoPaths.Samples("public", sample), path, overwrite: true);
        return path;
    }
}
