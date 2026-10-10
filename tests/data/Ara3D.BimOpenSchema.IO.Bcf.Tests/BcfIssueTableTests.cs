using Ara3D.DataTable;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>How rows of a table become issues, and how odd inputs are written.</summary>
[TestFixture]
public sealed class BcfIssueTableTests
{
    private const string WallA = "2O2Fr$t4X7Zf8NOew3FLOH";
    private const string WallB = "1hOSvn6df7F8_7GcBWlRGQ";

    private static IDataTable Table(params (string Name, object?[] Values)[] columns)
    {
        var builder = new DataTableBuilder("issues");
        foreach (var (name, values) in columns)
            builder.AddColumn(values, name, typeof(string));
        return builder.Build();
    }

    [Test]
    public void RowsGroupByTitleInFirstAppearanceOrder()
    {
        var issues = Table(
            ("GlobalId", [WallA, WallB, WallA, WallB]),
            ("Title", ["Walls", "Doors", "Walls", "Walls"]),
            ("Description", ["too thin", "", "too thin", "no fire rating"])).ToBcfIssues();

        Assert.That(issues, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Title, Is.EqualTo("Walls"));
            Assert.That(issues[0].GlobalIds, Is.EqualTo(new[] { WallA, WallB }), "each id once, in row order");
            Assert.That(issues[0].Description, Is.EqualTo("too thin\nno fire rating"));
            Assert.That(issues[1].Title, Is.EqualTo("Doors"));
            Assert.That(issues[1].Description, Is.Empty);
        });
    }

    [Test]
    public void TheVerdictsPresetReadsAVerdictTablesColumns()
    {
        var issue = Table(
            ("globalId", [WallA]),
            ("verdict", ["Fail"]),
            ("checkId", ["W1"]),
            ("checkTitle", ["Walls carry a fire rating"]),
            ("citation", ["Building code 3.1.4"])).ToBcfIssues(BcfColumns.Verdicts).Single();
        Assert.Multiple(() =>
        {
            Assert.That(issue.Title, Is.EqualTo("Walls carry a fire rating"));
            Assert.That(issue.Description, Is.EqualTo("Building code 3.1.4"));
            Assert.That(issue.GlobalIds, Is.EqualTo(new[] { WallA }));
        });
    }

    [Test]
    public void NullAndDbNullCellsAreEmpty()
    {
        var issue = Table(
            ("GlobalId", [null, DBNull.Value]),
            ("Title", ["Model has no project north", "Model has no project north"]),
            ("Priority", [DBNull.Value, null])).ToBcfIssues().Single();
        Assert.Multiple(() =>
        {
            Assert.That(issue.GlobalIds, Is.Empty);
            Assert.That(issue.Priority, Is.Empty);
        });
    }

    [Test]
    public void AModelLevelIssueIsATopicWithNoViewpoint()
    {
        var (archive, summary) = BcfArchive.Write([new BcfIssue("Model has no project north", [], "Set true north.")]);
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(summary, Is.EqualTo(new BcfSummary(1, 0, 0, 0)));
            Assert.That((string?)archive.Markups.Single().Descendants("Description").Single(), Is.EqualTo("Set true north."));
        });
    }

    [Test]
    public void StatusPriorityAndTypeAreWrittenAndListedInExtensions()
    {
        var (archive, _) = BcfArchive.Write([new BcfIssue("Walls", [WallA], Status: "Closed", Priority: "High", Type: "Clash")]);
        var topic = archive.Markups.Single().Root!.Element("Topic")!;
        var extensions = archive.Xml(BcfWriter.ExtensionsFile).Root!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That((string?)topic.Attribute("TopicStatus"), Is.EqualTo("Closed"));
            Assert.That((string?)topic.Attribute("TopicType"), Is.EqualTo("Clash"));
            Assert.That((string?)topic.Element("Priority"), Is.EqualTo("High"));
            Assert.That(extensions.Descendants().Where(e => !e.HasElements).Select(e => e.Value), Is.EqualTo(new[] { "Clash", "Closed", "High" }));
        });
    }

    [Test]
    public void BlankAndRepeatedIdsInARecordAreDropped()
    {
        var (archive, summary) = BcfArchive.Write([new BcfIssue("Walls", [WallA, " ", WallA + " "])]);
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(summary.Elements, Is.EqualTo(1));
        });
    }

    [Test]
    public void AnEmptyListWritesAValidContainerWithNoTopics()
    {
        var (archive, _) = BcfArchive.Write([]);
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(archive.EntryNames, Is.EqualTo(new[] { BcfWriter.VersionFile, BcfWriter.ExtensionsFile }));
        });
    }

    /// <summary>Why a topic without geometry gets no viewpoint: BCF 3.0 requires a camera in
    /// every viewpoint, so components alone do not validate. Also shows the validator rejects.</summary>
    [Test]
    public void Bcf3RejectsAViewpointWithoutACamera()
    {
        var xml = $"""
            <VisualizationInfo Guid="{BcfGuid.Viewpoint("t")}">
              <Components><Selection><Component IfcGuid="{WallA}"/></Selection></Components>
            </VisualizationInfo>
            """;
        Assert.That(BcfArchive.Validate("viewpoint.bcfv", System.Text.Encoding.UTF8.GetBytes(xml), "visinfo.xsd"), Is.Not.Empty);
    }

    [Test]
    public void MissingGlobalIdColumnNamesTheColumnsThereAre()
        => Assert.That(() => Table(("Id", [WallA]), ("Title", ["Walls"])).ToBcfIssues(),
            Throws.ArgumentException.With.Message.Contains("'GlobalId'").And.Message.Contains("Id, Title"));

    [Test]
    public void ABlankTitleIsAnError()
        => Assert.That(() => Table(("GlobalId", [WallA]), ("Title", ["  "])).ToBcfIssues(),
            Throws.ArgumentException.With.Message.Contains("Row 0"));

    [Test]
    public void TwoPrioritiesInOneTopicAreAnError()
        => Assert.That(() => Table(("GlobalId", [WallA, WallB]), ("Title", ["Walls", "Walls"]), ("Priority", ["High", "Low"])).ToBcfIssues(),
            Throws.ArgumentException.With.Message.Contains("'High' and 'Low'"));

    [Test]
    public void TwoIssuesWithOneTitleAreAnError()
        => Assert.That(() => BcfArchive.Write([new BcfIssue("Walls", [WallA]), new BcfIssue("Walls ", [WallB])]),
            Throws.ArgumentException.With.Message.Contains("'Walls'"));
}
