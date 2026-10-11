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
