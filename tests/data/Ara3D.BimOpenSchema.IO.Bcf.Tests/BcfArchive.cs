using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>A written BCF container read back for assertions: its entry names in zip order, each
/// file's XML, and a check of every file against the buildingSMART BCF 3.0 schema for its kind
/// (the XSDs in Schemas/, see README.md).</summary>
internal sealed class BcfArchive
{
    public byte[] Bytes { get; }
    public IReadOnlyList<string> EntryNames { get; }
    private readonly Dictionary<string, byte[]> _files;

    private BcfArchive(byte[] bytes)
    {
        Bytes = bytes;
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        EntryNames = zip.Entries.Select(e => e.FullName).ToList();
        _files = zip.Entries.Where(e => !e.FullName.EndsWith('/')).ToDictionary(e => e.FullName, Read);
    }

    public static (BcfArchive Archive, BcfSummary Summary) Write(IReadOnlyList<BcfIssue> issues, BcfOptions? options = null, ElementBounds? bounds = null)
    {
        using var stream = new MemoryStream();
        var summary = BcfWriter.Write(stream, issues, options ?? Fixed, bounds);
        return (new BcfArchive(stream.ToArray()), summary);
    }

    /// <summary>Options with a pinned date, so output depends on the input only.</summary>
    public static readonly BcfOptions Fixed = new() { CreationDate = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero) };

    public IEnumerable<string> TopicFolders
        => EntryNames.Where(n => n.EndsWith('/')).Select(n => n.TrimEnd('/'));

    public IEnumerable<string> Files(string suffix)
        => _files.Keys.Where(k => k.EndsWith(suffix, StringComparison.Ordinal));

    public XDocument Xml(string name)
        => XDocument.Load(new MemoryStream(_files[name]));

    public IEnumerable<XDocument> Markups => Files(".bcf").Select(Xml);
    public IEnumerable<XDocument> Viewpoints => Files(".bcfv").Select(Xml);

    /// <summary>Every schema error and warning in every file, as "file: message"; empty when valid.</summary>
    public IReadOnlyList<string> SchemaErrors()
        => _files.Keys.SelectMany(name => Validate(name, _files[name], SchemaFor(name))).ToList();

    private static string SchemaFor(string name)
        => Path.GetExtension(name) switch
        {
            ".bcf" => "markup.xsd",
            ".bcfv" => "visinfo.xsd",
            ".version" => "version.xsd",
            ".xml" when name == BcfWriter.ExtensionsFile => "extensions.xsd",
            _ => throw new AssertionException($"No BCF schema covers '{name}'"),
        };

    /// <summary>The schema errors and warnings of one XML document against one BCF schema file.</summary>
    public static IReadOnlyList<string> Validate(string name, byte[] xml, string schema)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        // .NET resolves no xs:include unless given a resolver; every BCF schema includes shared-types.xsd.
        settings.Schemas.XmlResolver = new XmlUrlResolver();
        settings.Schemas.Add(null, Path.Combine(AppContext.BaseDirectory, "Schemas", schema));
        settings.ValidationEventHandler += (_, e) => errors.Add($"{name}: {e.Severity} {e.Message}");
        using var reader = XmlReader.Create(new MemoryStream(xml), settings);
        while (reader.Read()) { }
        return errors;
    }

    private static byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
