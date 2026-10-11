using System.Reflection;
using System.Text.Json;

namespace Ara3D.BimOpenSchema.ObjectModel.Tests;

[TestFixture]
public sealed class ColumnKindsTests
{
    private static readonly string[] IssueNames = typeof(IssueColumns)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Test]
    public void Names_are_unique_ignoring_case()
        => Assert.That(ColumnKinds.All.GroupBy(k => k.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1),
            Is.Empty);

    [Test]
    public void Every_issue_column_is_registered([ValueSource(nameof(IssueNames))] string name)
        => Assert.That(ColumnKinds.Find(name)?.Name, Is.EqualTo(name));

    [Test]
    public void Every_kind_has_a_meaning_and_a_name()
    {
        foreach (var kind in ColumnKinds.All)
        {
            Assert.That(kind.Name, Is.Not.Empty);
            Assert.That(kind.Meaning, Is.Not.Empty, kind.Name);
        }
    }

    [Test]
    public void All_lists_every_static_member()
    {
        var members = typeof(ColumnKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ColumnKind))
            .Select(f => (ColumnKind)f.GetValue(null)!);
        Assert.That(ColumnKinds.All, Is.EquivalentTo(members));
    }

    [Test]
    public void Find_ignores_case_and_misses_unknown_names()
    {
        Assert.That(ColumnKinds.Find("globalid"), Is.SameAs(ColumnKinds.GlobalId));
        Assert.That(ColumnKinds.Find("NoSuchColumn"), Is.Null);
    }

    [Test]
    public void Json_has_every_kind_in_order_and_omits_absent_units()
    {
        using var doc = JsonDocument.Parse(ColumnKinds.ToJson());
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.That(items.Select(i => i.GetProperty("name").GetString()),
            Is.EqualTo(ColumnKinds.All.Select(k => k.Name)));

        var volume = items.Single(i => i.GetProperty("name").GetString() == "Volume");
        Assert.That(volume.GetProperty("type").GetString(), Is.EqualTo("Number"));
        Assert.That(volume.GetProperty("role").GetString(), Is.EqualTo("Measure"));
        Assert.That(volume.TryGetProperty("unit", out _), Is.False);
    }
}
