using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>What the writer writes, the reader reads back: titles, fields, and the GlobalIds of
/// each topic, from the viewpoints when there is geometry and from the description when not.</summary>
[TestFixture]
public sealed class BcfReaderTests
{
    private static readonly IReadOnlyList<BcfIssue> Issues =
    [
        new("Doors too narrow", ["1aj$VJZFn2TxepZUBcKp$i", "1s1jVhK8z0pgKYcr9jt781"], "Leaf under 850 mm", "Open", "High", "Issue"),
        new("Missing fire rating", ["2OBrcmyk58NupXoVOHUuXp"], Status: "Active"),
        new("Model-level note", []),
    ];

    /// <summary>A Revit UniqueId: not IFC's 22-character form, so the writer emits it as an
    /// AuthoringToolId.</summary>
    private const string RevitId = "5a1b2c3d-0000-4000-8000-00000000abcd-0004a1f2";

    private static IReadOnlyList<BcfTopicRecord> RoundTrip(ElementBounds? bounds = null, IReadOnlyList<BcfIssue>? issues = null)
    {
        using var stream = new MemoryStream();
        BcfWriter.Write(stream, issues ?? Issues, BcfArchive.Fixed, bounds);
        stream.Position = 0;
        Assert.That(BcfReader.Version(stream), Is.EqualTo("3.0"));
        stream.Position = 0;
        var contents = BcfReader.Read(stream);
        Assert.That(contents.Warnings, Is.Empty);
        return contents.Topics;
    }

    private static ElementBounds Duplex()
        => ElementBounds.FromBimData(new FilePath(RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip());

    [Test]
    public void WithoutGeometry_ReadsFieldsAndTheIdsFromTheDescription()
    {
        var topics = RoundTrip().ToDictionary(t => t.Title);
        Assert.That(topics.Keys, Is.EquivalentTo(Issues.Select(i => i.Title)));
        var narrow = topics["Doors too narrow"];
        Assert.Multiple(() =>
        {
            Assert.That(narrow.GlobalIds, Is.EqualTo(Issues[0].GlobalIds));
            Assert.That(narrow.HasViewpoint, Is.False);
            Assert.That(narrow.Description, Is.EqualTo("Leaf under 850 mm"));
            Assert.That(narrow.Status, Is.EqualTo("Open"));
            Assert.That(narrow.Priority, Is.EqualTo("High"));
            Assert.That(narrow.Type, Is.EqualTo("Issue"));
            Assert.That(narrow.Author, Is.EqualTo(BcfArchive.Fixed.Author));
            Assert.That(narrow.CreationDate, Is.EqualTo(BcfArchive.Fixed.CreationDate));
            Assert.That(narrow.Guid, Is.EqualTo(BcfGuid.Topic("", "Doors too narrow")));
            Assert.That(topics["Missing fire rating"].Status, Is.EqualTo("Active"));
            Assert.That(topics["Missing fire rating"].Type, Is.EqualTo(BcfArchive.Fixed.DefaultTopicType));
            Assert.That(topics["Model-level note"].GlobalIds, Is.Empty);
        });
    }

    [Test]
    public void WithGeometry_ReadsTheIdsFromTheViewpoints()
    {
        var topics = RoundTrip(Duplex()).ToDictionary(t => t.Title);
        Assert.Multiple(() =>
        {
            Assert.That(topics["Doors too narrow"].HasViewpoint, Is.True);
            Assert.That(topics["Doors too narrow"].GlobalIds, Is.EquivalentTo(Issues[0].GlobalIds));
            Assert.That(topics["Doors too narrow"].Description, Is.EqualTo("Leaf under 850 mm"), "no id list when a viewpoint holds them");
            Assert.That(topics["Model-level note"].HasViewpoint, Is.False);
        });
    }

    [Test]
    public void ToTable_OneRowPerTopicAndElement_WithTheIssueColumns()
    {
        var table = RoundTrip().ToTable();
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[]
        {
            IssueColumns.GlobalId, IssueColumns.Title, IssueColumns.Description, IssueColumns.Status,
            IssueColumns.Priority, IssueColumns.Type, "TopicGuid", "Author", "CreationDate", BcfReader.AuthoringToolIdColumn,
        }));
        Assert.That(table.Rows, Has.Count.EqualTo(4), "two doors, one wall, one model-level row");
        var titles = Enumerable.Range(0, 4).Select(r => (string?)table[1, r]).ToList();
        Assert.That(titles.Count(t => t == "Doors too narrow"), Is.EqualTo(2));
        var note = Enumerable.Range(0, 4).Single(r => (string?)table[1, r] == "Model-level note");
        Assert.That(table[0, note], Is.EqualTo(""));
        Assert.That(table[9, note], Is.EqualTo(""));
        Assert.That(table[8, 0], Is.EqualTo("2026-10-10T12:00:00Z"));
    }

    [Test]
    public void WithoutGeometry_TheDescriptionComesBackWithoutTheIdList()
    {
        var topics = RoundTrip().ToDictionary(t => t.Title);
        Assert.That(topics["Doors too narrow"].Description, Is.EqualTo("Leaf under 850 mm"));
        Assert.That(topics["Missing fire rating"].Description, Is.EqualTo(""));
    }

    /// <summary>A hand-written BCF 2.1 container, the shape Solibri and Revit write: the
    /// viewpoint list sits beside the topic, not inside it.</summary>
    [Test]
    public void Bcf21_ViewpointsBesideTheTopic_AreRead()
    {
        const string markup = """
            <?xml version="1.0" encoding="utf-8"?>
            <Markup>
              <Topic Guid="4f7d2c1e-0000-4000-8000-000000000021" TopicType="Issue" TopicStatus="Open">
                <Title>Clash at level 2</Title>
                <CreationDate>2026-10-01T09:30:00Z</CreationDate>
                <CreationAuthor>solibri@example.com</CreationAuthor>
              </Topic>
              <Viewpoints Guid="4f7d2c1e-0000-4000-8000-000000000022">
                <Viewpoint>viewpoint.bcfv</Viewpoint>
                <Snapshot>snapshot.png</Snapshot>
              </Viewpoints>
            </Markup>
            """;
        const string viewpoint = """
            <?xml version="1.0" encoding="utf-8"?>
            <VisualizationInfo Guid="4f7d2c1e-0000-4000-8000-000000000022">
              <Components>
                <Selection>
                  <Component IfcGuid="1aj$VJZFn2TxepZUBcKp$i" />
                  <Component IfcGuid="1s1jVhK8z0pgKYcr9jt781" />
                </Selection>
              </Components>
            </VisualizationInfo>
            """;
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "bcf.version", "<?xml version=\"1.0\"?><Version VersionId=\"2.1\"><DetailedVersion>2.1</DetailedVersion></Version>");
            Add(zip, "4f7d2c1e-0000-4000-8000-000000000021\\markup.bcf", markup);
            Add(zip, "4f7d2c1e-0000-4000-8000-000000000021\\viewpoint.bcfv", viewpoint);
        }
        stream.Position = 0;
        Assert.That(BcfReader.Version(stream), Is.EqualTo("2.1"));
        stream.Position = 0;
        var topic = BcfReader.Read(stream).Topics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(topic.Title, Is.EqualTo("Clash at level 2"));
            Assert.That(topic.GlobalIds, Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i", "1s1jVhK8z0pgKYcr9jt781" }));
            Assert.That(topic.HasViewpoint, Is.True);
            Assert.That(topic.Author, Is.EqualTo("solibri@example.com"));
        });
    }

    /// <summary>The writer emits a 22-character id as IfcGuid and any other as AuthoringToolId;
    /// the reader puts each back in its own list, from a viewpoint or from the description.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void AnAuthoringToolId_ComesBackApartFromTheGlobalIds(bool withGeometry)
    {
        IReadOnlyList<BcfIssue> issues = [new("Revit door", ["1aj$VJZFn2TxepZUBcKp$i", RevitId])];
        var topic = RoundTrip(withGeometry ? Duplex() : null, issues).Single();
        var table = new[] { topic }.ToTable();
        Assert.Multiple(() =>
        {
            Assert.That(topic.HasViewpoint, Is.EqualTo(withGeometry));
            Assert.That(topic.GlobalIds, Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i" }));
            Assert.That(topic.AuthoringToolIds, Is.EqualTo(new[] { RevitId }));
            Assert.That(topic.Description, Is.EqualTo(""));
            Assert.That(Column(table, IssueColumns.GlobalId), Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i", "" }));
            Assert.That(Column(table, BcfReader.AuthoringToolIdColumn), Is.EqualTo(new[] { "", RevitId }));
        });
    }

    /// <summary>A read table written again: <see cref="BcfIssueTable.ToBcfIssues"/> takes a row's
    /// AuthoringToolId when its GlobalId is empty, so neither kind of id is lost on the way round.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void ReadTable_WrittenAgain_KeepsBothKindsOfId(bool withGeometry)
    {
        IReadOnlyList<BcfIssue> issues = [new("Revit door", ["1aj$VJZFn2TxepZUBcKp$i", RevitId])];
        var bounds = withGeometry ? Duplex() : null;
        var table = RoundTrip(bounds, issues).ToTable();
        var again = table.ToBcfIssues();
        Assert.That(again.Single().GlobalIds, Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i", RevitId }));
        var topic = RoundTrip(bounds, again).Single();
        Assert.Multiple(() =>
        {
            Assert.That(topic.GlobalIds, Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i" }));
            Assert.That(topic.AuthoringToolIds, Is.EqualTo(new[] { RevitId }));
        });
    }

    /// <summary>A container with one good topic, one whose component has only an AuthoringToolId,
    /// one whose markup lists a viewpoint the zip lacks, and one whose viewpoint is not XML. The
    /// bad parts are skipped with a warning each; every topic still reads.</summary>
    [Test]
    public void BadViewpoints_AreWarnings_AndTheOtherTopicsStillRead()
    {
        const string good = "00000000-0000-4000-8000-000000000001", authoring = "00000000-0000-4000-8000-000000000002",
            missing = "00000000-0000-4000-8000-000000000003", broken = "00000000-0000-4000-8000-000000000004";
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var guid in new[] { good, authoring, missing, broken })
                Add(zip, $"{guid}/markup.bcf", Markup(guid));
            Add(zip, $"{good}/viewpoint.bcfv", Viewpoint("""<Component IfcGuid="1aj$VJZFn2TxepZUBcKp$i" />"""));
            Add(zip, $"{authoring}/viewpoint.bcfv", Viewpoint($"<Component><AuthoringToolId>{RevitId}</AuthoringToolId></Component>"));
            Add(zip, $"{broken}/viewpoint.bcfv", "this is not <xml");
        }
        stream.Position = 0;
        var contents = BcfReader.Read(stream);
        var topics = contents.Topics.ToDictionary(t => t.Guid);
        var table = contents.Topics.ToTable();
        var authoringRow = Column(table, "TopicGuid").IndexOf(authoring);
        Assert.Multiple(() =>
        {
            Assert.That(topics.Keys, Is.EquivalentTo(new[] { good, authoring, missing, broken }));
            Assert.That(topics[good].GlobalIds, Is.EqualTo(new[] { "1aj$VJZFn2TxepZUBcKp$i" }));
            Assert.That(topics[good].AuthoringToolIds, Is.Empty);
            Assert.That(topics[authoring].GlobalIds, Is.Empty);
            Assert.That(topics[authoring].AuthoringToolIds, Is.EqualTo(new[] { RevitId }));
            Assert.That(topics[missing].HasViewpoint, Is.False);
            Assert.That(topics[broken].HasViewpoint, Is.False);
            Assert.That(contents.Warnings, Has.Count.EqualTo(2));
            Assert.That(contents.Warnings, Has.Exactly(1).Contains($"{missing}/viewpoint.bcfv").And.Contains("not in the container"));
            Assert.That(contents.Warnings, Has.Exactly(1).Contains($"{broken}/viewpoint.bcfv").And.Contains("not XML"));
            Assert.That(Column(table, IssueColumns.GlobalId)[authoringRow], Is.EqualTo(""));
            Assert.That(Column(table, BcfReader.AuthoringToolIdColumn)[authoringRow], Is.EqualTo(RevitId));
        });

        static string Markup(string guid) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Markup>
              <Topic Guid="{guid}" TopicType="Issue" TopicStatus="Open">
                <Title>Topic {guid[^1]}</Title>
                <Viewpoints><ViewPoint Guid="{guid}"><Viewpoint>viewpoint.bcfv</Viewpoint></ViewPoint></Viewpoints>
              </Topic>
            </Markup>
            """;

        static string Viewpoint(string component) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <VisualizationInfo><Components><Selection>{component}</Selection></Components></VisualizationInfo>
            """;
    }

    private static List<string?> Column(Ara3D.DataTable.IDataTable table, string name)
    {
        var column = table.Columns.ToList().FindIndex(c => c.Descriptor.Name == name);
        return Enumerable.Range(0, table.Rows.Count).Select(r => (string?)table[column, r]).ToList();
    }

    private static void Add(System.IO.Compression.ZipArchive zip, string name, string text)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(System.Text.Encoding.UTF8.GetBytes(text.Trim()));
    }

    [Test]
    public void ReadFile_ReadsWhatWriteFileWrote()
    {
        var path = Path.Combine(Path.GetTempPath(), "bcf-reader-" + Guid.NewGuid().ToString("N"), "issues.bcf");
        try
        {
            BcfWriter.WriteFile(path, Issues, BcfArchive.Fixed);
            Assert.That(BcfReader.ReadFile(path).Topics.Select(t => t.Title), Is.EquivalentTo(Issues.Select(i => i.Title)));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
