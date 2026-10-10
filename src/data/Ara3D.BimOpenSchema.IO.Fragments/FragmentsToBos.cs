using Ara3D.Utils;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>Reads a That Open Fragments 2 file (<c>.frag</c>) into BIM Open Schema data.</summary>
public static class FragmentsToBos
{
    public const string GeneratorApplication = "Ara3D Fragments to BIM Open Schema Reader";
    public const string GeneratorVersion = "0.1.0";

    /// <summary>Reads the file; the document is titled by the file name without its extension.</summary>
    public static BimData Read(FilePath input)
        => Read(File.ReadAllBytes(input), input.GetFileNameWithoutExtension(), input);

    /// <summary>Reads the bytes of a <c>.frag</c> file, deflated or raw. Throws
    /// <see cref="FragmentsFormatException"/> when they are not a Fragments 2 model.</summary>
    public static BimData Read(byte[] bytes, string title, string path)
    {
        var model = FragmentsFile.Open(bytes);
        var bdb = new BimDataBuilder();
        bdb.Manifest.GeneratorApplication = GeneratorApplication;
        bdb.Manifest.GeneratorVersion = GeneratorVersion;
        var doc = bdb.AddDocument(title, path);
        var items = new FragmentsItems(model);
        new FragmentsEntityReader(items, bdb, doc).Read();
        bdb.Geometry = new BimGeometry();
        return bdb.Build();
    }

    /// <summary>Reads <paramref name="input"/> and writes it as a <c>.bos</c> archive.</summary>
    public static void Convert(FilePath input, FilePath output)
        => Read(input).WriteToParquetZip(output);
}
