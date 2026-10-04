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
        string[] Sources,
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

    public static IEnumerable<TestCaseData> Unions()
        => Samples().Where(c => ((Sample)c.Arguments[0]!).Sources.Length > 1);

    /// <summary>A union keeps every part whole: its entities and geometry instances are the
    /// parts' (each part is the single-source sample of the same name) in source order, and
    /// each instance still names the same source entity (document and STEP id) as in its part,
    /// so nothing was merged on a matching GlobalId and no instance points into another model.</summary>
    [TestCaseSource(nameof(Unions))]
    public void Union_IsItsPartsInOrder(Sample sample)
    {
        var union = new FilePath(RepoPaths.Samples("public", sample.Name + ".bos")).ReadBimDataFromParquetZip();
        var parts = sample.Sources.Select(s => new FilePath(RepoPaths.Samples("public", s + ".bos")).ReadBimDataFromParquetZip()).ToArray();
        Assert.That(union.Entities, Has.Length.EqualTo(parts.Sum(p => p.Entities.Length)), "entities");
        Assert.That(union.Geometry.InstanceEntityIndex, Has.Length.EqualTo(parts.Sum(p => p.Geometry.InstanceEntityIndex.Length)), "instances");

        var (entityOffset, instanceOffset) = (0, 0);
        for (var d = 0; d < parts.Length; d++)
        {
            var part = parts[d];
            for (var i = 0; i < part.Geometry.InstanceEntityIndex.Length; i++)
            {
                var expected = part.Geometry.InstanceEntityIndex[i];
                var actual = union.Geometry.InstanceEntityIndex[instanceOffset + i];
                if (expected < 0)
                {
                    Assert.That(actual, Is.EqualTo(expected));
                    continue;
                }
                Assert.That(actual, Is.EqualTo(expected + entityOffset), $"{sample.Sources[d]} instance {i}");
                Assert.That(union.Entities[actual].Document, Is.EqualTo((DocumentIndex)d));
                Assert.That(union.Entities[actual].LocalId, Is.EqualTo(part.Entities[expected].LocalId));
            }
            entityOffset += part.Entities.Length;
            instanceOffset += part.Geometry.InstanceEntityIndex.Length;
        }
    }
}
