using Ara3D.BimOpenSchema;
using static Ara3D.Ids.Tests.IdsXml;

namespace Ara3D.Ids.Tests;

/// <summary>IDS values are in SI; a model in millimetres has its lengths, areas and volumes
/// converted before a constraint compares them, and a number whose unit cannot be told is
/// compared as stored with a warning that names it.</summary>
[TestFixture]
public sealed class IdsUnitsTests
{
    private const string Wall = "W1";

    /// <summary>A project in millimetres, doors 900 and 800 wide, and a wall with a 5000 length
    /// and an area of 2 square metres (2,000,000 square millimetres) in its quantity set.</summary>
    private static readonly IdsModel Millimetres = new(
    [
        new(10, "P", "Project", "IFCPROJECT", [new("IFCPROJECT", "Ifc:LengthUnitToMetre", ParameterType.Number, "0.001")]),
        Door(IdsTestModel.Door1, "900"),
        Door(IdsTestModel.Door2, "800"),
        new(13, Wall, "Wall", "IFCWALL",
        [
            new("Qto_WallBaseQuantities", "Length", ParameterType.Number, "5000"),
            new("Qto_WallBaseQuantities", "NetSideArea", ParameterType.Number, "2000000"),
        ]),
    ]);

    private static IdsEntity Door(string globalId, string width)
        => new(globalId == IdsTestModel.Door1 ? 11 : 12, globalId, globalId, "IFCDOOR",
            [new("IFCDOOR", "Ifc:OverallWidth", ParameterType.Number, width)]);

    private static IdsReport Run(IdsModel model, string applicability, string requirements)
        => IdsEvaluator.Evaluate(IdsFile.Parse(Document(Specification("S", applicability, requirements))), model);

    private static Verdict VerdictOf(IdsReport report, string globalId)
        => report.Verdicts.Single(v => v.GlobalId == globalId).Verdict;

    [Test]
    public void LengthAttribute_IsConvertedToMetres()
    {
        var report = Run(Millimetres, Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85)));
        Assert.That(VerdictOf(report, IdsTestModel.Door1), Is.EqualTo(Verdict.Pass), "900 mm is at least 0.85 m");
        Assert.That(VerdictOf(report, IdsTestModel.Door2), Is.EqualTo(Verdict.Fail), "800 mm is not");
        Assert.That(report.Verdicts.Single(v => v.GlobalId == IdsTestModel.Door2).Reason, Does.Contain("'800' (0.8 in SI)"));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public void UnclassifiedNumber_IsComparedAsStored_AndWarnsByName()
    {
        var report = Run(Millimetres, Entity("IFCWALL"), Property("Qto_WallBaseQuantities", "Length", AtLeast(6)));
        Assert.That(VerdictOf(report, Wall), Is.EqualTo(Verdict.Pass), "5000 as stored is at least 6");
        Assert.That(report.Warnings, Has.Exactly(1).Contains("Qto_WallBaseQuantities.Length").And.Contains("not converted").And.Contains("0.001 m"));
    }

    [Test]
    public void PropertyDataType_TellsALength()
    {
        var report = Run(Millimetres, Entity("IFCWALL"), Property("Qto_WallBaseQuantities", "Length", AtLeast(6), dataType: "IFCLENGTHMEASURE"));
        Assert.That(VerdictOf(report, Wall), Is.EqualTo(Verdict.Fail), "5000 mm is 5 m, less than 6");
        Assert.That(report.Warnings, Has.None.Contains("not converted"));
    }

    [Test]
    public void AreaMeasure_ScalesByTheSquare()
    {
        string Area(double atLeast) => Property("Qto_WallBaseQuantities", "NetSideArea", AtLeast(atLeast), dataType: "IFCAREAMEASURE");
        Assert.That(VerdictOf(Run(Millimetres, Entity("IFCWALL"), Area(1.5)), Wall), Is.EqualTo(Verdict.Pass));
        Assert.That(VerdictOf(Run(Millimetres, Entity("IFCWALL"), Area(2.5)), Wall), Is.EqualTo(Verdict.Fail));
    }

    [Test]
    public void StatedUnits_WinOverTheModelUnit()
    {
        var model = new IdsModel([new(1, "D", "D", "IFCDOOR", [new("Dimensions", "Width", ParameterType.Number, "850", "mm")])]);
        var report = Run(model, Entity("IFCDOOR"), Property("Dimensions", "Width", AtLeast(0.85)));
        Assert.That(VerdictOf(report, "D"), Is.EqualTo(Verdict.Pass));
        Assert.That(report.Warnings, Has.None.Contains("not converted"), "the model records no unit, but the value states one");
    }

    [Test]
    public void ModelWithNoLengthUnit_WarnsThatALengthWasNotConverted()
    {
        var report = Run(IdsTestModel.Build(), Entity("IFCDOOR"), Attribute("OverallWidth", AtLeast(0.85)));
        Assert.That(report.Warnings, Has.Exactly(1).Contains("attribute OverallWidth").And.Contains("records no length unit"));
    }

    [Test]
    public void UnitsColumn_IsReadFromTheParameterTable()
    {
        var wall = IdsTestModel.Build().Entities.Single(e => e.GlobalId == IdsTestModel.Wall1);
        Assert.That(wall.Properties.Single(v => v.Name == "Length").Units, Is.EqualTo("m"));
        Assert.That(wall.Properties.Single(v => v.Name == "FireRating").Units, Is.Empty, "a null cell is no unit");
    }
}
