using System.Text.Json;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using BimOpenData.TestSupport;
using DuckDB.NET.Data;

namespace Ara3D.BimOpenSchema.DuckDb.Tests;

/// <summary>Opens each committed sample in <c>samples/public/</c> (its .bos and its .duckdb) and
/// checks the counts that <c>samples/public/samples.json</c> records and the sample README
/// reports, so a regenerated sample that changes is noticed. The files are committed, so these
/// tests need no fetched data.</summary>
[TestFixture]
public sealed class PublicSampleTests
{
    public sealed record Sample(
        string Name,
        bool Geometry,
        Dictionary<string, long> Counts,
        Dictionary<string, long> Relations,
        long Entities);

    private sealed record Catalog(Sample[] Samples);

    public static IEnumerable<TestCaseData> Samples()
    {
        var json = File.ReadAllText(RepoPaths.Samples("public", "samples.json"));
        var catalog = JsonSerializer.Deserialize<Catalog>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return catalog.Samples.Select(s => new TestCaseData(s).SetName($"PublicSample({s.Name})"));
    }

    private static DuckDBConnection OpenReadOnly(Sample sample)
    {
        var conn = new DuckDBConnection($"DataSource={RepoPaths.Samples("public", sample.Name + ".duckdb")};ACCESS_MODE=READ_ONLY");
        conn.Open();
        return conn;
    }

    private static long Count(DuckDBConnection conn, string sql, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter(value));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [TestCaseSource(nameof(Samples))]
    public void DuckDb_HasTheRecordedCounts(Sample sample)
    {
        if (!File.Exists(RepoPaths.Samples("public", sample.Name + ".duckdb")))
            Assert.Ignore($"{sample.Name}.duckdb is generated, not committed; run samples/public/convert.mjs");
        using var conn = OpenReadOnly(sample);
        Assert.Multiple(() =>
        {
            Assert.That(conn.ScalarInt64("SELECT count(*) FROM Entities"), Is.EqualTo(sample.Entities), "entities");
            foreach (var (category, expected) in sample.Counts)
                Assert.That(Count(conn, "SELECT count(*) FROM EntityText WHERE Category = ?", category), Is.EqualTo(expected), category);
            foreach (var (relation, expected) in sample.Relations)
                Assert.That(Count(conn, "SELECT count(*) FROM RelationText WHERE RelationType = ?", relation), Is.EqualTo(expected), relation);
        });
    }

    [TestCaseSource(nameof(Samples))]
    public void Bos_OpensWithTheRecordedEntitiesAndGeometry(Sample sample)
    {
        var path = new FilePath(RepoPaths.Samples("public", sample.Name + ".bos"));
        var data = path.ReadBimDataFromParquetZip();
        Assert.That(data.Entities, Has.Length.EqualTo(sample.Entities));
        if (sample.Geometry)
            Assert.That(path.ReadBimGeometryFromParquetZip().InstanceEntityIndex.Length, Is.GreaterThan(0));
    }
}
