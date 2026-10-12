using Ara3D.DataTable;
using static Ara3D.Ids.Tests.IdsTestModel;
using static Ara3D.Ids.Tests.IdsXml;

namespace Ara3D.Ids.Tests;

/// <summary>The material and part-of facets over the test model's relations, and the same facets
/// over a model built without them.</summary>
[TestFixture]
public sealed class IdsRelationFacetTests
{
    private static readonly IdsModel Model = BuildWithRelations();

    private const string Contained = "IFCRELCONTAINEDINSPATIALSTRUCTURE";

    private static IdsReport Run(string applicability, string requirements = "", IdsModel? model = null)
        => IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", applicability, requirements))), model ?? Model);

    private static Dictionary<string, IdsVerdict> ByGlobalId(IdsReport report)
        => report.Verdicts.ToDictionary(v => v.GlobalId);

    [Test]
    public void Material_EnumerationPassesWhenAnyLayerMaterialMatches()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCWALL"), MaterialOf(Enumeration("Insulation", "Steel"))));
        Assert.That(verdicts[Wall1].Verdict, Is.EqualTo(Verdict.Pass), "the wall's second layer is Insulation");
        Assert.That(verdicts[Wall2].Verdict, Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void Material_FailNamesTheMaterialsFound()
    {
        var wall = ByGlobalId(Run(Entity("IFCWALL"), Material("Wood")))[Wall1];
        Assert.That(wall.Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(wall.Reason, Does.Contain("'Concrete'").And.Contain("'Insulation'").And.Contain("'Wall Set'"));
    }

    [Test]
    public void Material_TheLayerSetNameMatchesToo()
        => Assert.That(ByGlobalId(Run(Entity("IFCWALL"), Material("Wall Set")))[Wall1].Verdict, Is.EqualTo(Verdict.Pass));

    [Test]
    public void Material_AbsentFailsWhenRequired_PassesWhenOptional()
    {
        var required = ByGlobalId(Run(Entity("IFCDOOR"), Material("Wood")));
        Assert.That(required[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(required[Door2].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(required[Door2].Reason, Does.Contain("no material"));
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Material("Wood", "optional")))[Door2].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Material("Steel", "optional")))[Door1].Verdict, Is.EqualTo(Verdict.Fail),
            "optional still checks a material that exists");
    }

    [Test]
    public void Material_Prohibited()
    {
        var verdicts = ByGlobalId(Run(Entity("IFCDOOR"), Material("Wood", "prohibited")));
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Pass), "no material at all");
    }

    [Test]
    public void Material_OfTheTypeOnly_IsTheOccurrencesWhenItHasNone()
    {
        var model = BuildWithTypeMaterial();
        var prohibited = ByGlobalId(Run(Entity("IFCDOOR"), Material("Concrete", "prohibited"), model));
        Assert.That(prohibited[Door2].Verdict, Is.EqualTo(Verdict.Fail), "door D2 inherits Concrete from its type");
        Assert.That(prohibited[Door1].Verdict, Is.EqualTo(Verdict.Pass), "door D1's own Wood hides the type's Concrete");
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), Material("Concrete"), model))[Door2].Verdict, Is.EqualTo(Verdict.Pass));
    }

    /// <summary>A door whose type is not in the entities table, and a wall of Steel so the model
    /// carries HasMaterial: the door's material cannot be told, so a prohibited Steel cannot pass.</summary>
    [Test]
    public void Material_WhenTheTypeCannotBeTold_IsInfoNotAvailable()
    {
        var entities = Table("entities", ("EntityIndex", typeof(long), [0L, 1L, 2L]), ("GlobalId", typeof(string), ["D", "W", null]),
            ("Name", typeof(string), ["Door", "Wall", "Steel"]), ("Category", typeof(string), ["IFCDOOR", "IFCWALL", "IFCMATERIAL"]),
            ("Type", typeof(string), ["Filtered Out", null, null]));
        var parameters = Table("parameters", ("EntityIndex", typeof(long), []), ("Name", typeof(string), []),
            ("ParameterGroup", typeof(string), []), ("ValueType", typeof(string), []), ("Value", typeof(string), []));
        var relations = Table("relations", ("EntityIndexA", typeof(long), [1L]), ("EntityIndexB", typeof(long), [2L]),
            ("RelationType", typeof(string), ["HasMaterial"]));
        var report = Run(Entity("IFCDOOR"), Material("Steel", "prohibited"), IdsModel.FromTables(entities, parameters, relations));
        var door = report.Verdicts.Single();
        Assert.That(door.Verdict, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(door.Reason, Does.Contain("no material of its own").And.Contain("not in the entities table"));
        Assert.That(report.Warnings, Is.EqualTo(new[] { IdsFacets.UnresolvedTypeWarning }));
    }

    [Test]
    public void Material_AsApplicability_SelectsTheWall()
        => Assert.That(ByGlobalId(Run(Material("Concrete"))).Keys, Is.EquivalentTo(new[] { Wall1 }));

    [Test]
    public void PartOf_ContainedInAStorey()
    {
        var report = Run(EntityEnum("IFCDOOR", "IFCWINDOW"), PartOf("IFCBUILDINGSTOREY", Contained));
        var verdicts = ByGlobalId(report);
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Door2].Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(verdicts[Window].Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(verdicts[Window].Reason, Does.Contain("no container"));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public void PartOf_ContainmentReachesTheBuildingThroughTheStorey()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), PartOf("IFCBUILDING", Contained))).Values.Select(v => v.Verdict),
            Has.All.EqualTo(Verdict.Pass));
        var wrong = ByGlobalId(Run(Entity("IFCDOOR"), PartOf("IFCSITE", Contained)))[Door1];
        Assert.That(wrong.Verdict, Is.EqualTo(Verdict.Fail));
        Assert.That(wrong.Reason, Does.Contain("IFCBUILDINGSTOREY 'Level 1'").And.Contain("IFCBUILDING 'Building'"));
    }

    [Test]
    public void PartOf_AggregationIsNotContainment()
    {
        Assert.That(ByGlobalId(Run(Entity("IFCBUILDINGSTOREY"), PartOf("IFCBUILDING", "IFCRELAGGREGATES"))).Values.Single().Verdict,
            Is.EqualTo(Verdict.Pass));
        Assert.That(ByGlobalId(Run(Entity("IFCDOOR"), PartOf("IFCBUILDINGSTOREY", "IFCRELAGGREGATES")))[Door1].Verdict,
            Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void PartOf_AsApplicability_SelectsTheContainedDoors()
        => Assert.That(ByGlobalId(Run(PartOf("IFCBUILDINGSTOREY", Contained))).Keys, Is.EquivalentTo(new[] { Door1, Door2 }));

    [TestCase("IFCRELNESTS", "ChildOf")]
    [TestCase("IFCRELVOIDSELEMENT IFCRELFILLSELEMENT", "Fills or Voids")]
    public void PartOf_ARelationTheModelLacks_IsInfoNotAvailable(string relation, string bosTypes)
    {
        var report = Run(Entity("IFCDOOR"), PartOf("IFCWALL", relation));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(report.Warnings, Has.Exactly(1).Contains(bosTypes).And.Contains(relation));
    }

    [Test]
    public void PartOf_ARelationIds10DoesNotName_IsInfoNotAvailable_NotAnyRelation()
    {
        // Xbim's loader turns an unknown relation into an empty one, which would read as "any
        // relation" and pass the doors through containment.
        const string relation = "IFCRELREFERENCEDINSPATIALSTRUCTURE";
        var report = Run(Entity("IFCDOOR"), PartOf("IFCBUILDINGSTOREY", relation));
        Assert.That(report.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(report.Warnings, Has.Some.Contains(relation).And.Contains("not an IDS 1.0 relation"));
        Assert.That(report.Warnings, Has.Some.Contains(relation).And.Contains("not carried in BOS"));
    }

    [Test]
    public void PartOf_NoRelationStated_PassesThroughAnyRelation_AndCannotTellAMiss()
    {
        var report = Run(EntityEnum("IFCDOOR", "IFCWINDOW"), PartOf("IFCBUILDINGSTOREY", null));
        var verdicts = ByGlobalId(report);
        Assert.That(verdicts[Door1].Verdict, Is.EqualTo(Verdict.Pass), "found through containment");
        Assert.That(verdicts[Window].Verdict, Is.EqualTo(Verdict.InfoNotAvailable), "the model carries no nests, voids or fills");
        Assert.That(report.Warnings, Is.Not.Empty);
    }

    [Test]
    public void WithoutRelations_BothFacetsAreInfoNotAvailable()
    {
        var model = Build();
        var material = Run(Entity("IFCWALL"), Material("Concrete"), model);
        Assert.That(material.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(material.Warnings, Has.Exactly(1).Contains("material facets need the relations"));
        var partOf = Run(Entity("IFCDOOR"), PartOf("IFCBUILDINGSTOREY", Contained), model);
        Assert.That(partOf.Verdicts.Select(v => v.Verdict), Has.All.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(partOf.Warnings, Has.Exactly(1).Contains("partof facets need the relations"));
    }

    [Test]
    public void Relations_ReadTheTypeAsANameOrANumber()
    {
        var b = new DataTableBuilder("relations");
        b.AddColumn(new object?[] { 0L, 1L, null }, "entityindexa", typeof(long));
        b.AddColumn(new object?[] { 13L, 13L, 13L }, "EntityIndexB", typeof(long));
        b.AddColumn(new object?[] { 2, 2, 2 }, "RelationType", typeof(int));
        var relations = IdsRelations.FromTable(b.Build());
        Assert.That(relations.Count, Is.EqualTo(2), "a row without a source is dropped");
        Assert.That(relations.Carries(BimOpenSchema.RelationType.ContainedIn), Is.True);
        Assert.That(relations.Targets(1, BimOpenSchema.RelationType.ContainedIn), Is.EqualTo(new[] { 13L }));
    }
}
