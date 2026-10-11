using Ara3D.BimOpenSchema;
using static Ara3D.Ids.Tests.IdsXml;

namespace Ara3D.Ids.Tests;

/// <summary>The verdict and specification tables carry the IssueColumns names.</summary>
[TestFixture]
public sealed class IdsTablesTests
{
    private static readonly string Ids = Document(
        Specification("DC-W1", Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85)), description: "Door leaf width at least 850 mm"),
        Specification("MAT", Entity("IFCWALL"), Material("concrete")));

    [Test]
    public void VerdictTable_HasTheIssueColumns()
    {
        var table = IdsEvaluator.Evaluate(IdsFile.Parse(Ids), IdsTestModel.Build()).ToTable();
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[]
        {
            IssueColumns.GlobalId, IssueColumns.EntityIndex, IssueColumns.CheckId, IssueColumns.CheckTitle,
            IssueColumns.Citation, IssueColumns.Verdict, IssueColumns.Reason,
        }));
        Assert.That(table.Rows, Has.Count.EqualTo(4), "two doors and two walls");
        Assert.That(table[5, 0], Is.EqualTo("Pass"));
        Assert.That(table[1, 0], Is.EqualTo(0L));
    }

    [Test]
    public void SummaryTable_OneRowPerSpecification_EvenWithNoRows_EvenWithOneName()
    {
        var ids = IdsFile.Parse(Document(
            Specification("S", Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85))),
            Specification("S", Entity("IFCSLAB"), "", occurs: """maxOccurs="0" """),
            Specification("S", Entity("IFCWALL"), Material("concrete"))));
        var table = IdsTables.Summary(ids, IdsEvaluator.Evaluate(ids, IdsTestModel.Build()));
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[] { "CheckId", "CheckTitle", "Pass", "Fail", "InfoNotAvailable", "Rows" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3), "three specifications, one name");
        Assert.That(Enumerable.Range(0, 3).Select(r => table[2, r]), Is.EqualTo(new object?[] { 1L, 0L, 0L }), "Pass");
        Assert.That(Enumerable.Range(0, 3).Select(r => table[3, r]), Is.EqualTo(new object?[] { 1L, 0L, 0L }), "Fail");
        Assert.That(Enumerable.Range(0, 3).Select(r => table[4, r]), Is.EqualTo(new object?[] { 0L, 0L, 2L }), "InfoNotAvailable");
        Assert.That(table[5, 1], Is.EqualTo(0L), "a prohibited specification nothing matched has no rows");
    }

    [Test]
    public void SpecificationTable_ListsEachSpecificationAndWhetherItIsSupported()
    {
        var table = IdsTables.Specifications(IdsFile.Parse(Ids));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[]
        {
            IssueColumns.CheckId, IssueColumns.CheckTitle, "IfcVersion", "Cardinality", "Applicability", "Requirements", "Supported",
        }));
        Assert.That(table[0, 0], Is.EqualTo("DC-W1"));
        Assert.That(table[1, 0], Is.EqualTo("Door leaf width at least 850 mm"));
        Assert.That((string?)table[4, 0], Does.Contain("IFCDOOR"));
        Assert.That((string?)table[5, 0], Does.Contain("OverallWidth"));
        Assert.That(table[6, 0], Is.True);
        Assert.That(table[6, 1], Is.False);
    }
}
