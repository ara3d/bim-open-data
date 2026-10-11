using static Ara3D.Ids.Tests.IdsTestModel;
using static Ara3D.Ids.Tests.IdsXml;

namespace Ara3D.Ids.Tests;

/// <summary>Each IDS facet and cardinality over the in-memory model, one specification at a time.</summary>
[TestFixture]
public sealed class IdsEvaluatorTests
{
    private static readonly IdsModel Model = Build();

    private static IdsReport Run(string applicability, string requirements = "", string occurs = """minOccurs="1" maxOccurs="unbounded" """)
        => IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", applicability, requirements, occurs))), Model);

    private static Dictionary<string, IdsVerdict> ByGlobalId(IdsReport report)
        => report.Verdicts.ToDictionary(v => v.GlobalId);

    [Test]
    public void EntityFacet_MatchesTheClassExactly()
    {
        var report = Run(Entity("IFCDOOR"));
        Assert.That(ByGlobalId(report).Keys, Is.EquivalentTo(new[] { Door1, Door2 }));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.Pass), "no requirements: applicable means pass");
        Assert.That(report.Warnings, Is.Empty);
        Assert.That(report.Verdicts.Select(v => v.Specification), Has.All.EqualTo(0));
    }

    [Test]
    public void EntityFacet_EnumerationSelectsSeveralClasses()
        => Assert.That(ByGlobalId(Run(EntityEnum("IFCDOOR", "IFCWINDOW"))).Keys, Is.EquivalentTo(new[] { Door1, Door2, Window }));

    [Test]
    public void EntityFacet_PredefinedType_ReadsObjectTypeForUserDefined()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR", "DOOR"))).Keys, Is.EquivalentTo(new[] { Door1 }));
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR", "Hatch"))).Keys, Is.EquivalentTo(new[] { Door2 }));
    }

    [Test]
    public void PropertyFacet_AbsentPropertyFails()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating")));
        Assert.That(verdicts[Wall1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Wall2].Verdict, Is.EqualTo(Verdict.Fail), "an empty value is absent");
        Assert.That(verdicts[Wall2].Reason, Does.Contain("no Pset_WallCommon.FireRating"));
    }

    [Test]
    public void PropertyFacet_WarnsOnceThatTypePropertySetsAreNotSeen()
    {
        var report = Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating") + Property("Pset_WallCommon", "LoadBearing"));
        Assert.That(report.Warnings, Is.EqualTo(new[] { IdsFacets.TypePropertySetsWarning }));
        Assert.That(Run(Entity("IFCWALL")).Warnings, Is.Empty, "no property facet, no warning");
    }

    [Test]
    public void PropertyFacet_ValueEnumerationAndPattern()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", Enumeration("REI60", "REI90"))))[Wall1].Verdict,
            Is.EqualTo(Verdict.Pass));
        var wrong = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", Enumeration("REI90"))))[Wall1];
        Assert.That(wrong.Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(wrong.Reason, Does.Contain("REI60"));
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", Pattern("REI.*"))))[Wall1].Verdict,
            Is.EqualTo(Verdict.Pass));
    }

    [Test]
    public void PropertyFacet_NumericRangeOverAQuantity()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCWALL"), Property("Qto_WallBaseQuantities", "Length", AtLeast(4))));
        Assert.That(verdicts[Wall1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Wall2].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Qto_WallBaseQuantities", "Length", AtLeast(6))))[Wall1].Verdict,
            Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void PropertyFacet_DataTypeIsCheckedByKind()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", dataType: "IFCLABEL")))[Wall1].Verdict,
            Is.EqualTo(Verdict.Pass));
        var number = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", dataType: "IFCREAL")))[Wall1];
        Assert.That(number.Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(number.Reason, Does.Contain("text"));
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "LoadBearing", dataType: "IFCBOOLEAN")))[Wall2].Verdict,
            Is.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Qto_WallBaseQuantities", "Length", dataType: "IFCLENGTHMEASURE")))[Wall1].Verdict,
            Is.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", dataType: "IFCDESCRIPTIVEMEASURE")))[Wall1].Verdict,
            Is.EqualTo(Verdict.Pass), "a descriptive measure is text");
    }

    [Test]
    public void PropertyFacet_UnknownDataTypeWarnsAndIsNotChecked()
    {
        var report = Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", dataType: "IFCWIDGET"));
        Assert.That(ByGlobalId(report)[Wall1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(report.Warnings, Has.Exactly(1).Contains("IFCWIDGET"));
        Assert.That(report.Warnings, Has.Count.EqualTo(2), "plus the type property set warning");
    }

    [Test]
    public void AttributeFacet_NumericBound()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85))));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Door2].Reason, Does.Contain("0.8"));
    }

    [Test]
    public void AttributeFacet_EnumerationIsComparedWithoutItsDots()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Attribute("PredefinedType", Simple("NOTDEFINED"), cardinality: "prohibited")));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass), ".DOOR. is not NOTDEFINED");
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Attribute("PredefinedType", Simple("DOOR"))))[Door1].Verdict, Is.EqualTo(Verdict.Pass));
    }

    [Test]
    public void AttributeFacet_ASpacesNameIsItsRoomNumber_NotItsLongName()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCSPACE"), Attribute("Name", Pattern("[0-9]{3}"))))[Space].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCSPACE"), Attribute("Name", Simple("Kitchen"))))[Space].Verdict, Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void ModelNotInMetres_Warns()
    {
        var millimetres = new IdsModel(Model.Entities.Append(new IdsEntity(99, "P", "Project", "IFCPROJECT",
            [new IdsValue("IFCPROJECT", "Ifc:LengthUnitToMetre", BimOpenSchema.ParameterType.Number, "0.001")])).ToList());
        var report = IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85))))), millimetres);
        Assert.That(report.Warnings, Has.Exactly(1).Contains("0.001 m"));
        Assert.That(IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", Entity("IFCDOOR")))), Model).Warnings, Is.Empty, "the test model records no unit");
    }

    [Test]
    public void AttributeFacet_NameAndGlobalIdAreAttributes()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Attribute("Name", Pattern("Door .*")))).Values.Select(v => v.Verdict),
            Has.All.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Attribute("GlobalId", Simple(Door1))))[Door2].Verdict, Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void OptionalRequirement_PassesWhenAbsent_ChecksValueWhenPresent()
    {
        var absent = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", cardinality: "optional")));
        Assert.That(absent[Wall2].Verdict, Is.EqualTo(Verdict.Pass));
        var valued = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", Simple("REI90"), cardinality: "optional")));
        Assert.That(valued[Wall1].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(valued[Wall2].Verdict, Is.EqualTo(Verdict.Pass));
    }

    [Test]
    public void ProhibitedRequirement_FailsWhenPresent()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCWALL"), Property("Pset_WallCommon", "FireRating", cardinality: "prohibited")));
        Assert.That(verdicts[Wall1].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Wall1].Reason, Does.StartWith("prohibited"));
        Assert.That(verdicts[Wall2].Verdict, Is.EqualTo(Verdict.Pass));
    }

    [Test]
    public void SeveralRequirements_KeepTheWorstVerdict()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"),
            Attribute("OverallWidth", AtLeast(0.85)) + Property("Pset_DoorCommon", "IsExternal", dataType: "IFCBOOLEAN")));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Door2].Reason, Does.Contain("0.8").And.Contain("IsExternal"));
    }

    [Test]
    public void RequiredSpecification_WithNoApplicableEntity_FailsAtModelLevel()
    {
        var report = Run(Entity("IFCSLAB"), Property("Pset_SlabCommon", "FireRating"));
        Assert.That(report.Verdicts, Has.Count.EqualTo(1));
        var row = report.Verdicts[0];
        Assert.That(row.GlobalId, Is.Empty);
        Assert.That(row.EntityIndex, Is.EqualTo(-1));
        Assert.That(row.Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(row.Reason, Does.Contain("0 applicable"));
    }

    [Test]
    public void OptionalSpecification_WithNoApplicableEntity_ProducesNoRow()
        => Assert.That(Run(Entity("IFCSLAB"), Property("Pset_SlabCommon", "FireRating"), """minOccurs="0" maxOccurs="unbounded" """).Verdicts, Is.Empty);

    [Test]
    public void ProhibitedSpecification_FailsEveryApplicableEntity()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCWINDOW"), "", """maxOccurs="0" """));
        Assert.That(verdicts.Keys, Is.EquivalentTo(new[] { Window }));
        Assert.That(verdicts[Window].Verdict, Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void UnsupportedRequirementFacet_IsInfoNotAvailable_WithAWarning()
    {
        var report = Run(Entity("IFCWALL"), Material("concrete"));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(report.Warnings, Has.Exactly(1).Contains("material"));
    }

    [Test]
    public void UnsupportedApplicabilityFacet_IsOneModelLevelInfoNotAvailable()
    {
        var report = Run(Material("concrete"), Property("Pset_WallCommon", "FireRating"));
        Assert.That(report.Verdicts, Has.Count.EqualTo(1));
        Assert.That(report.Verdicts[0].Verdict, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(report.Verdicts[0].GlobalId, Is.Empty);
        Assert.That(report.Warnings, Is.Not.Empty);
    }

    [Test]
    public void MisspelledFacet_IsReportedAsAWarning_NotAPass()
    {
        // The loader skips an element it does not know and logs it; the report must carry that.
        var report = Run(Entity("IFCWALL"), "<proprety><propertySet><simpleValue>Pset_WallCommon</simpleValue></propertySet><baseName><simpleValue>FireRating</simpleValue></baseName></proprety>");
        Assert.That(report.Warnings, Has.Exactly(1).Contains("proprety").IgnoreCase);
        Assert.That(ByGlobalId(report)[Wall2].Verdict, Is.EqualTo(Verdict.Pass), "no requirement survived; the warning is the signal");
    }

    [Test]
    public void Rows_CarryTheSpecificationNameDescriptionAndFacets()
    {
        var ids = IdsFile.Parse(Document(Specification("DC-W1", Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85)),
            description: "Door leaf width at least 850 mm")));
        var row = IdsEvaluator.Evaluate(ids, Model).Verdicts[0];
        Assert.That(row.CheckId, Is.EqualTo("DC-W1"));
        Assert.That(row.CheckTitle, Is.EqualTo("Door leaf width at least 850 mm"));
        Assert.That(row.Citation, Does.Contain("OverallWidth"));
    }

    [Test]
    public void FromBimData_AndFromTables_AgreeOnAMinimalDocument()
    {
        // Strings: "" (absent), then the names the entities and descriptors point at.
        var strings = new[] { "", "IFCDOOR", "gid", "Door", "Ifc:OverallWidth", "Pset_DoorCommon", "FireRating", "EI30" };
        var data = new BimOpenSchema.BimData
        {
            Strings = strings,
            Numbers = [0.9f],
            Points = [],
            Documents = [new((BimOpenSchema.StringIndex)0, (BimOpenSchema.StringIndex)0)],
            Entities =
            [
                new(-1, (BimOpenSchema.StringIndex)0, (BimOpenSchema.DocumentIndex)0, (BimOpenSchema.StringIndex)1, (BimOpenSchema.EntityIndex)(-1), (BimOpenSchema.EntityIndex)(-1)),
                new(7, (BimOpenSchema.StringIndex)2, (BimOpenSchema.DocumentIndex)0, (BimOpenSchema.StringIndex)3, (BimOpenSchema.EntityIndex)0, (BimOpenSchema.EntityIndex)(-1)),
            ],
            Descriptors =
            [
                new((BimOpenSchema.StringIndex)4, (BimOpenSchema.StringIndex)0, (BimOpenSchema.StringIndex)1, BimOpenSchema.ParameterType.Number),
                new((BimOpenSchema.StringIndex)6, (BimOpenSchema.StringIndex)0, (BimOpenSchema.StringIndex)5, BimOpenSchema.ParameterType.String),
            ],
            Parameters =
            [
                new((BimOpenSchema.EntityIndex)1, (BimOpenSchema.DescriptorIndex)0, 0),
                new((BimOpenSchema.EntityIndex)1, (BimOpenSchema.DescriptorIndex)1, 7),
            ],
            Relations = [],
            Diagnostics = [],
            Geometry = new BimOpenSchema.BimGeometry(),
        };
        var model = IdsModel.FromBimData(data);
        Assert.That(model.Entities, Has.Count.EqualTo(1), "the class row has no category and is left out");
        var door = model.Entities[0];
        Assert.That(door.Category, Is.EqualTo("IFCDOOR"));
        Assert.That(door.GlobalId, Is.EqualTo("gid"));
        Assert.That(door.Attribute("OverallWidth")?.Candidate, Is.EqualTo(0.9).Within(1e-6));
        Assert.That(door.Properties.Single().Text, Is.EqualTo("EI30"));

        var ids = IdsFile.Parse(Document(Specification("S", Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85)))));
        Assert.That(IdsEvaluator.Evaluate(ids, model).Verdicts.Single().Verdict, Is.EqualTo(Verdict.Pass));
    }
}
