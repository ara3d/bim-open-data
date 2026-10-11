using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;
using DuckDB.NET.Data;

namespace BimOpenMcp.Ifc;

/// <summary>The MetricCatalog table: the analytics metric dictionary a model names in its
/// provenance set, so an agent can resolve a metric to its property set, property, unit, and
/// rollup rule without guessing from names. The dictionary is a CSV (for the NRC sample,
/// samples/nrc/nrc-metrics.csv) located by <c>Pset_NRCAnalyticsProvenance.MetricDictionaryURI</c>.
/// A relative URI is resolved against the folder of the source file the model records for the
/// entity that names it (the Documents table's Path: for an IFC conversion, the IFC file), then
/// against the .bos file's folder. A model that names none, or names a file that is not there,
/// gets an empty table with the dictionary's columns, so a query on MetricCatalog never fails for
/// want of the table.</summary>
public static class IfcMetricCatalog
{
    public const string Table = "MetricCatalog";
    public const string ProvenanceSet = "Pset_NRCAnalyticsProvenance";
    public const string UriProperty = "MetricDictionaryURI";

    /// <summary>The columns of the empty table, in the dictionary's order.</summary>
    public const string EmptyColumns =
        "MetricId VARCHAR, Level VARCHAR, PropertySet VARCHAR, PropertyName VARCHAR, ValueType VARCHAR, "
        + "Unit VARCHAR, LifecycleStage VARCHAR, Rollup VARCHAR, Description VARCHAR, Decimals BIGINT";

    /// <summary>Creates MetricCatalog in the database built from <paramref name="bos"/> and returns
    /// the dictionary it was read from, or null when it is empty.</summary>
    public static FilePath? Create(FilePath database, FilePath bos)
    {
        using var conn = BosDuckDb.Open(database);
        var dictionary = Locate(conn, bos);
        conn.Execute(dictionary is { } file
            ? $"CREATE OR REPLACE TABLE {Table} AS SELECT * FROM read_csv('{file.FullPath.Replace('\\', '/').Replace("'", "''")}')"
            : $"CREATE OR REPLACE TABLE {Table} ({EmptyColumns})");
        return dictionary;
    }

    /// <summary>The existing file the model's MetricDictionaryURI names, or null.</summary>
    private static FilePath? Locate(DuckDBConnection conn, FilePath bos)
    {
        // default, not null: FilePath converts implicitly from string, so a bare null would become
        // a FilePath with an empty path.
        if (Uri(conn) is not (string uri, var document))
            return default;
        var path = new[] { document, bos.FullPath }
            .Where(file => !string.IsNullOrEmpty(file))
            .Select(file => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file!)) ?? "", uri)))
            .FirstOrDefault(File.Exists);
        return path == null ? default(FilePath?) : new FilePath(path);
    }

    /// <summary>The first MetricDictionaryURI in entity order, and the path of the document its
    /// entity came from (null when the model records none).</summary>
    private static (string Uri, string? Document)? Uri(DuckDBConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT p.Value, s.Strings FROM ParameterText p
            JOIN Entities e ON e.rowid = p.EntityIndex
            LEFT JOIN Documents d ON d.rowid = e.Document
            LEFT JOIN Strings s ON s.rowid = d.Path
            WHERE p.ParameterGroup = '{ProvenanceSet}' AND p.Name = '{UriProperty}' AND p.Value <> ''
            ORDER BY p.EntityIndex LIMIT 1
            """;
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)) : null;
    }
}
