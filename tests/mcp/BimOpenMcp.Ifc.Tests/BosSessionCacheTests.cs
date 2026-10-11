using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>The BOS session cache: one session per .bos path, reopened when the file changes,
/// bounded, held open while leased, and shared with the IFC sessions' conversions; and the
/// files it derives, found again after a restart and swept when stale.</summary>
[TestFixture]
public sealed class BosSessionCacheTests
{
    private string _scratch = null!;
    private readonly List<string> _stores = [];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _scratch = RepoPaths.Artifacts("bimopenmcp-ifc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        foreach (var folder in _stores.Append(_scratch).Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
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
    public void Lease_KeepsASessionOpenThroughEviction()
    {
        using var cache = new BosSessionCache(capacity: 1, Files());
        var first = RepoPaths.Samples("public", "duplex.bos");
        var second = RepoPaths.Samples("public", "duplex-electrical.bos");
        var lease = cache.Lease(first);
        cache.Get(second);

        Assert.That(cache.IsOpen(first), Is.True, "a held session is not evicted");
        Assert.That(File.Exists(lease.Session.DatabasePath.FullPath), Is.True, "and still builds its database");
        lease.Dispose();
        Assert.That(cache.IsOpen(first), Is.False, "released, it is past capacity and goes");
    }

    [Test]
    public void Close_WhileLeased_DisposesWhenTheLeaseEnds()
    {
        using var cache = new BosSessionCache(files: Files());
        var path = RepoPaths.Samples("public", "duplex.bos");
        var lease = cache.Lease(path);

        Assert.That(cache.Close(path), Is.True);
        Assert.That(cache.IsOpen(path), Is.False);
        Assert.That(lease.Session.Scene.EntityCount, Is.GreaterThan(0), "a closed session stays usable while held");
        lease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Session.DatabasePath);
    }

    [Test]
    public void Dispose_BuildsNothingAfterwards()
    {
        var files = Files();
        var session = BosSession.Open(RepoPaths.Samples("public", "duplex.bos"), files);
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = session.DatabasePath);
        Assert.That(Directory.Exists(files.Root), Is.False, "a disposed session leaves no folder behind");
    }

    [Test]
    public void Database_IsFoundAgainByANewCache_AsAfterARestart()
    {
        var files = Files();
        var path = Copy("duplex.bos", "restart.bos");
        string database;
        using (var before = new BosSessionCache(files: files))
            database = before.Get(path).DatabasePath.FullPath;
        var written = File.GetLastWriteTimeUtc(database);

        using var after = new BosSessionCache(files: files);
        Assert.That(after.Get(path).DatabasePath.FullPath, Is.EqualTo(database));
        Assert.That(File.GetLastWriteTimeUtc(database), Is.EqualTo(written), "found, not built again");
    }

    [Test]
    public void Sweep_RemovesTheDatabaseOfAChangedFileOnly()
    {
        var files = Files();
        var kept = Copy("duplex.bos", "kept.bos");
        var changed = Copy("duplex.bos", "swept.bos");
        using var cache = new BosSessionCache(files: files);
        var keptDatabase = cache.Get(kept).DatabasePath.FullPath;
        var changedDatabase = cache.Get(changed).DatabasePath.FullPath;
        File.SetLastWriteTimeUtc(changed, File.GetLastWriteTimeUtc(changed).AddMinutes(1));

        Assert.That(files.Sweep(DerivedFiles.DefaultUnusedFor), Is.EqualTo(1));
        Assert.That(File.Exists(keptDatabase), Is.True);
        Assert.That(File.Exists(changedDatabase), Is.False);
        Assert.That(cache.Get(changed).DatabasePath.FullPath, Is.Not.EqualTo(changedDatabase), "the changed file gets a folder of its own");
    }

    [Test]
    public void IfcSession_HoldsItsConversionInTheSharedCache_UntilItCloses()
    {
        using var cache = new IfcSessionCache(files: Files());
        var ifc = TestModel.RequirePath(TestModel.FzkHaus);
        var session = cache.Get(ifc);
        var bos = session.Bos;

        Assert.That(cache.BosSessions.Get(bos.BosPath.FullPath), Is.SameAs(bos));
        Assert.That(cache.Close(ifc), Is.True);
        Assert.That(cache.BosSessions.IsOpen(bos.BosPath.FullPath), Is.False);
        Assert.That(File.Exists(bos.BosPath.FullPath), Is.True, "the conversion stays for the next session to reuse");
    }

    [Test]
    public void IfcSession_KeepsItsConversionOpenPastTheBosCapacity()
    {
        using var cache = new IfcSessionCache(bosCapacity: 1, files: Files());
        var ifc = TestModel.RequirePath(TestModel.FzkHaus);
        var bos = cache.Get(ifc).Bos;
        var database = bos.DatabasePath.FullPath;
        cache.BosSessions.Get(RepoPaths.Samples("public", "duplex.bos"));
        cache.BosSessions.Get(RepoPaths.Samples("public", "duplex-electrical.bos"));

        Assert.That(cache.BosSessions.IsOpen(bos.BosPath.FullPath), Is.True);
        Assert.That(cache.Get(ifc).Bos, Is.SameAs(bos));
        Assert.That(File.Exists(database), Is.True, "the databasePath ifc_to_bos returned is still there");
    }

    /// <summary>A store of derived files of its own, so a test sees only what it built. Under the
    /// temp folder, not the artifacts folder, to keep DuckDB's paths short.</summary>
    private DerivedFiles Files()
    {
        var files = new DerivedFiles(Path.Combine(Path.GetTempPath(), "bimopenmcp-tests", Guid.NewGuid().ToString("N")[..8]));
        _stores.Add(files.Root);
        return files;
    }

    private string Copy(string sample, string name)
    {
        var path = Path.Combine(_scratch, name);
        File.Copy(RepoPaths.Samples("public", sample), path, overwrite: true);
        return path;
    }
}
