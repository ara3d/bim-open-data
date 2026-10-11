namespace Ara3D.Ifc.Conventions.Tests;

/// <summary>The naming rule both IFC readers share, fed attributes by name as either source gives them.</summary>
public class IfcNamesTests
{
    private static IfcEntityName NameOf(string ifcClass, params (string Name, string? Value)[] attributes)
        => IfcNames.NameOf(ifcClass, name => attributes.FirstOrDefault(a => a.Name == name).Value);

    [Test]
    public void NameAttributes_ArePinned()
        => Assert.That(IfcNames.NameAttributes, Is.EqualTo(new[] { "Name", "LayerSetName", "ProfileName", "ApplicationFullName" }));

    [Test]
    public void Space_IsNamedByItsLongName_AndItsNameIsTheRoomNumber()
        => Assert.That(NameOf("IFCSPACE", ("Name", "101"), ("LongName", "Kitchen")), Is.EqualTo(new IfcEntityName("Kitchen", "101")));

    [TestCase(null)]
    [TestCase("")]
    public void SpaceWithoutALongName_IsNamedByItsName_WhichIsStillTheRoomNumber(string? longName)
        => Assert.That(NameOf("IFCSPACE", ("Name", "101"), ("LongName", longName)), Is.EqualTo(new IfcEntityName("101", "101")));

    [Test]
    public void SpaceWithAnEmptyName_HasNoRoomNumber()
        => Assert.That(NameOf("IFCSPACE", ("Name", ""), ("LongName", "Kitchen")), Is.EqualTo(new IfcEntityName("Kitchen", null)));

    [Test]
    public void AnotherClassWithALongName_IsNamedByItsName_AndHasNoRoomNumber()
        => Assert.That(NameOf("IFCBUILDINGSTOREY", ("Name", "E00"), ("LongName", "Ground floor")), Is.EqualTo(new IfcEntityName("E00", null)));

    [TestCase("IFCMATERIALLAYERSET", "LayerSetName", "Wall 200")]
    [TestCase("IFCARBITRARYPROFILEDEF", "ProfileName", "L-shape")]
    [TestCase("IFCAPPLICATION", "ApplicationFullName", "Revit")]
    public void AClassWithoutName_IsNamedByItsOwnNameAttribute(string ifcClass, string attribute, string value)
        => Assert.That(NameOf(ifcClass, (attribute, value), ("Description", "not a name")), Is.EqualTo(new IfcEntityName(value, null)));

    [Test]
    public void NoNameAttribute_GivesNoName()
        => Assert.That(NameOf("IFCMATERIALLIST", ("Materials", null)), Is.EqualTo(new IfcEntityName(null, null)));

    [Test]
    public void AStoredEmptyName_IsAValue()
        => Assert.That(NameOf("IFCWALL", ("Name", "")).Name, Is.EqualTo(""));
}
