using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Logging;
using Ara3D.Utils;

namespace Ara3D.BimOpenSchema.Federation;

/// <summary>One document to add to a union, and the title and path it is recorded under.</summary>
public sealed record UnionInput(IBimData Data, string Title, string Path);

/// <summary>One document's entity count and declared length unit, as recorded on its
/// IFCPROJECT entity by the converter (C2). LengthUnit is null when the document declares
/// none, which storey matching treats as unknown rather than guessing metres. Title and Path
/// are null when the document records none (string index -1, or "" in older files).</summary>
public sealed record DocumentSummary(string? Title, string? Path, int Entities, string? LengthUnit, double? LengthUnitToMetre);

/// <summary>Converts IFC files one at a time and unions the results, tables and geometry, into
/// one document, with no matching or federation logic: that lives in the match graph (C5, C6) and
/// the studio views (C8). Keeping the two apart lets the union stay mechanical and the rules
/// stay in SQL.</summary>
public static class BosUnion
{
    // Read as string literals rather than IfcLengthUnit's constants: C2 (Ara3D.Ifc.Bos) may not
    // have committed its parameter names yet, and Summarize only needs to match by name.
    private const string LengthUnitNameParameter = "Ifc:LengthUnit";
    private const string LengthUnitScaleParameter = "Ifc:LengthUnitToMetre";
    private const string ProjectCategory = "IFCPROJECT";

    /// <summary>Converts each file with IfcToBosConverter, disposing its IfcFile before the
    /// next so peak memory reflects one file, not all of them. Title is the file name without
    /// its extension; Path is the path as given, as IfcToBosConverter records it, so relative
    /// inputs keep machine-local folders out of the union.</summary>
    public static IReadOnlyList<UnionInput> ConvertIfc(IReadOnlyList<FilePath> ifcFiles, ILogger? logger = null)
    {
        var result = new List<UnionInput>(ifcFiles.Count);
        foreach (var file in ifcFiles)
        {
            var converter = new IfcToBosConverter(file, logger);
            try
            {
                result.Add(new UnionInput(converter.BimDataBuilder.Build(), file.GetFileNameWithoutExtension(), file.Value));
            }
            finally
            {
                converter.IfcFile.Dispose();
            }
        }
        return result;
    }

    /// <summary>One document per input, in input order, via AddBimData(bd, title, path), and
    /// the inputs' geometry concatenated by GeometryUnion so each instance still points at its
    /// own document's entity. Every entity of every input is kept: nothing is merged on a
    /// matching GlobalId, so an element two models share appears, and is drawn, once per model.
    /// Geometry is null only when no input has any; an input without geometry adds none.</summary>
    public static BimData Union(IReadOnlyList<UnionInput> inputs)
    {
        var builder = new BimDataBuilder();
        foreach (var input in inputs)
            builder.AddBimData(input.Data, input.Title, input.Path);
        builder.Geometry = inputs.Any(HasGeometry)
            ? GeometryUnion.Union(inputs.Select(i => new GeometryUnion.Part(i.Data.Geometry ?? new BimGeometry(), i.Data.Entities.Length)).ToArray())
            : null!;
        return builder.Build();
    }

    private static bool HasGeometry(UnionInput input)
        => input.Data.Geometry is { InstanceEntityIndex.Length: > 0 };

    /// <summary>Parquet zip of the tables, plus the geometry tables when the union has geometry.</summary>
    public static void WriteBos(IBimData union, FilePath output)
        => union.WriteToParquetZip(output);

    /// <summary>Deletes then writes a DuckDB file: BOS tables via BosDuckDb.LoadBimData, then
    /// BosDuckDbViews.CreateViews. This load path avoids the enum shift the DuckDb README warns
    /// about for parquet-derived databases.</summary>
    public static void WriteDuckDb(IBimData union, FilePath output)
    {
        if (File.Exists(output))
            File.Delete(output);
        using var conn = BosDuckDb.Open(output);
        conn.LoadBimData(union);
        conn.CreateViews();
    }

    /// <summary>One row per document, with its entity count and the length unit declared on
    /// its IFCPROJECT entity (null when the document declares none).</summary>
    public static IReadOnlyList<DocumentSummary> Summarize(IBimData union)
    {
        var entityCounts = new int[union.Documents.Length];
        foreach (var e in union.Entities)
            entityCounts[(int)e.Document]++;

        var result = new DocumentSummary[union.Documents.Length];
        for (var d = 0; d < union.Documents.Length; d++)
        {
            var doc = union.Documents[d];
            var (name, scale) = ReadLengthUnit(union, (DocumentIndex)d);
            result[d] = new DocumentSummary(union.Label(doc.Title), union.Label(doc.Path), entityCounts[d], name, scale);
        }
        return result;
    }

    private static (string? Name, double? Scale) ReadLengthUnit(IBimData union, DocumentIndex document)
    {
        string? name = null;
        double? scale = null;
        for (var e = 0; e < union.Entities.Length; e++)
        {
            var entity = union.Entities[e];
            if (entity.Document != document || !IsCategory(union, entity.Category, ProjectCategory))
                continue;
            foreach (var p in union.Parameters)
            {
                if ((int)p.Entity != e)
                    continue;
                var descriptor = union.Descriptors[(int)p.Descriptor];
                var descriptorName = union.Get(descriptor.Name);
                if (descriptorName == LengthUnitNameParameter)
                    name = union.Label((StringIndex)p.Value);
                else if (descriptorName == LengthUnitScaleParameter)
                    scale = union.Get((NumberIndex)p.Value);
            }
        }
        return (name, scale);
    }

    private static bool IsCategory(IBimData union, EntityIndex category, string name)
        => union.EntityName(category) == name;
}
