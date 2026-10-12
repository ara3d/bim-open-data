using Ara3D.DataTable;
using static Ara3D.Ids.Tests.IdsTestModel;
using static Ara3D.Ids.Tests.IdsXml;
using Bos = Ara3D.BimOpenSchema;

namespace Ara3D.Ids.Tests;

/// <summary>Property values an occurrence inherits from its type object (TKT-237): over
/// <see cref="IdsTestModel"/>, both doors are of "Door Type A", whose Pset_DoorCommon.FireRating is
/// EI60; door D1 states its own EI30, door D2 states none.</summary>
[TestFixture]
public sealed class IdsTypePropertyTests
{
    private static readonly IdsModel Model = Build();

    private static IdsReport Run(string applicability, string requirements, IdsModel? model = null)
        => IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", applicability, requirements))), model ?? Model);

    private static Dictionary<string, IdsVerdict> ByGlobalId(IdsReport report)
        => report.Verdicts.Where(v => v.GlobalId.Length > 0).ToDictionary(v => v.GlobalId);

    [Test]
    public void FromTables_FindsTheTypeByName()
    {
        var door2 = Model.Entities.Single(e => e.GlobalId == Door2);
        Assert.That(door2.Type?.GlobalId, Is.EqualTo(DoorType));
        Assert.That(Model.Entities.Single(e => e.GlobalId == Wall1).Type, Is.Null);
    }

    [Test]
    public void RequiredProperty_PassesThroughTheTypeAndThroughTheOccurrence()
    {
        var report = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating"));
        var verdicts = ByGlobalId(report);
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Pass), "EI60 from the door type");
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public void OccurrenceValue_OverridesTheType()
    {
        var door1 = Model.Entities.Single(e => e.GlobalId == Door1);
        Assert.That(door1.Properties.Where(v => v.Name == "FireRating").Select(v => v.Text), Is.EqualTo(new[] { "EI30" }));
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI30"))));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Door2].Reason, Does.Contain("EI60"));
    }

    [Test]
    public void ProhibitedProperty_FailsBecauseOfTheTypesValue()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI60"), cardinality: "prohibited")));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass), "its own EI30 hides the type's EI60");
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void Attributes_AreNotInherited()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Attribute("OperationType")));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Fail), "OperationType is the type's attribute, not the door's");
    }

    [Test]
    public void SameNamedTypes_WithDifferentValues_CannotBeTold()
    {
        var report = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating"), TwoTypesNamedT("EI60", "EI90"));
        var verdict = report.Verdicts.Single();
        Assert.That(verdict.Verdict, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(verdict.Reason, Does.Contain("'T'").And.Contain("several entities"));
        Assert.That(report.Warnings, Is.EqualTo(new[] { IdsFacets.UnresolvedTypeWarning }));
    }

    [Test]
    public void SameNamedTypes_WithTheSameValues_AreOneType()
    {
        var report = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI60")), TwoTypesNamedT("EI60", "EI60"));
        Assert.That(report.Verdicts.Single().Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public void TypeIndexColumn_ResolvesWhatTheNameCannot()
    {
        var report = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI90")), TwoTypesNamedT("EI60", "EI90", typeIndex: 2));
        Assert.That(report.Verdicts.Single().Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public void MissingTypeEntity_CannotBeTold()
    {
        var entities = Table("entities", ("EntityIndex", typeof(long), [0L]), ("GlobalId", typeof(string), ["D"]),
            ("Name", typeof(string), ["Door"]), ("Category", typeof(string), ["IFCDOOR"]), ("Type", typeof(string), ["Filtered Out"]));
        var model = IdsModel.FromTables(entities, Parameters([]));
        var verdict = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating"), model).Verdicts.Single();
        Assert.That(verdict.Verdict, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(verdict.Reason, Does.Contain("not in the entities table"));
    }

    /// <summary>The door's own Pset_DoorCommon.FireRating is EI30, and its type is not in the
    /// entities table. A pattern on the set could match a FireRating in another set the type
    /// gives, which might not be EI30, so the door cannot pass; an exact set and name can.</summary>
    [Test]
    public void PatternSetName_WhenTheTypeCannotBeTold_IsInfoNotAvailable_EvenWithAMatchingOwnValue()
    {
        var entities = Table("entities", ("EntityIndex", typeof(long), [0L]), ("GlobalId", typeof(string), ["D"]),
            ("Name", typeof(string), ["Door"]), ("Category", typeof(string), ["IFCDOOR"]), ("Type", typeof(string), ["Filtered Out"]));
        var model = IdsModel.FromTables(entities, Parameters([(0, "EI30")]));
        var pattern = Run(Entity("IFCDOOR"), PropertyOf(Pattern("Pset_.*"), Simple("FireRating"), Simple("EI30")), model);
        Assert.That(pattern.Verdicts.Single().Verdict, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(pattern.Verdicts.Single().Reason, Does.Contain("not in the entities table"));
        Assert.That(pattern.Warnings, Is.EqualTo(new[] { IdsFacets.UnresolvedTypeWarning }));
        var exact = Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI30")), model);
        Assert.That(exact.Verdicts.Single().Verdict, Is.EqualTo(Verdict.Pass), "its own value hides whatever the type has under that name");
    }

    [Test]
    public void FromBimData_ReadsEntityType()
    {
        // Strings: "" (absent), then the names the entities and descriptors point at.
        var strings = new[] { "", "IFCDOOR", "IFCDOORTYPE", "d", "Door", "t", "Door Type", "Pset_DoorCommon", "FireRating", "EI60" };
        var data = new Bos.BimData
        {
            Strings = strings,
            Numbers = [],
            Points = [],
            Documents = [new((Bos.StringIndex)0, (Bos.StringIndex)0)],
            Entities =
            [
                Row(-1, 0, 1, -1, -1),
                Row(-1, 0, 2, -1, -1),
                Row(7, 3, 4, 0, 3),
                Row(8, 5, 6, 1, -1),
            ],
            Descriptors = [new((Bos.StringIndex)8, (Bos.StringIndex)0, (Bos.StringIndex)7, Bos.ParameterType.String)],
            Parameters = [new((Bos.EntityIndex)3, (Bos.DescriptorIndex)0, 9)],
            Relations = [],
            Diagnostics = [],
            Geometry = new Bos.BimGeometry(),
        };
        var model = IdsModel.FromBimData(data);
        var door = model.Entities.Single(e => e.Category == "IFCDOOR");
        Assert.That(door.TypeIndex, Is.EqualTo(3));
        Assert.That(door.TypeName, Is.EqualTo("Door Type"));
        Assert.That(door.OwnProperties, Is.Empty);
        Assert.That(door.Properties.Single().Text, Is.EqualTo("EI60"));
        Assert.That(Run(Entity("IFCDOOR"), Property("Pset_DoorCommon", "FireRating", Simple("EI60")), model).Verdicts.Single().Verdict,
            Is.EqualTo(Verdict.Pass));
    }

    private static Bos.Entity Row(long localId, int globalId, int name, int category, int type)
        => new(localId, (Bos.StringIndex)globalId, (Bos.DocumentIndex)0, (Bos.StringIndex)name, (Bos.EntityIndex)category, (Bos.EntityIndex)type);

    /// <summary>A door (entity 0) whose Type column names "T", and two IFCDOORTYPE entities named
    /// "T" (1 and 2) with these fire ratings; <paramref name="typeIndex"/> fills a TypeIndex column.</summary>
    private static IdsModel TwoTypesNamedT(string first, string second, long? typeIndex = null)
    {
        var columns = new List<(string, Type, object?[])>
        {
            ("EntityIndex", typeof(long), [0L, 1L, 2L]),
            ("GlobalId", typeof(string), ["D", "T1", "T2"]),
            ("Name", typeof(string), ["Door", "T", "T"]),
            ("Category", typeof(string), ["IFCDOOR", "IFCDOORTYPE", "IFCDOORTYPE"]),
            ("Type", typeof(string), ["T", null, null]),
        };
        if (typeIndex is { } index)
            columns.Add(("TypeIndex", typeof(long), [index, null, null]));
        return IdsModel.FromTables(Table("entities", [.. columns]), Parameters([(1, first), (2, second)]));
    }

    private static IDataTable Parameters(IReadOnlyList<(long Entity, string FireRating)> rows)
        => Table("parameters",
            ("EntityIndex", typeof(long), rows.Select(r => (object?)r.Entity).ToArray()),
            ("Name", typeof(string), rows.Select(_ => (object?)"FireRating").ToArray()),
            ("ParameterGroup", typeof(string), rows.Select(_ => (object?)"Pset_DoorCommon").ToArray()),
            ("ValueType", typeof(string), rows.Select(_ => (object?)"String").ToArray()),
            ("Value", typeof(string), rows.Select(r => (object?)r.FireRating).ToArray()));
}
