namespace Ara3D.Ifc.Conventions.Tests;

/// <summary>The predicates and the flag rule built on the tables.</summary>
public class ConventionRuleTests
{
    [TestCase("IFCSPACE", (byte)0, (byte)1)]
    [TestCase("IFCSPACE", (byte)6, (byte)1)]
    [TestCase("IFCWALL", (byte)0, (byte)0)]
    [TestCase("IFCWALL", (byte)6, (byte)6)]
    [TestCase(null, (byte)4, (byte)4)]
    public void InstanceFlags_AreExactlyHiddenForAHiddenClass_ElseTheSourceFlags(string? ifcClass, byte source, byte expected)
        => Assert.That(IfcClasses.InstanceFlags(ifcClass, source), Is.EqualTo(expected));

    [TestCase("IFCWALL", true)]
    [TestCase("IFCMATERIALLAYER", true)]
    [TestCase("IFCRELAGGREGATES", false)]
    [TestCase("IFCRELCONNECTSPATHELEMENTS", false)]
    [TestCase("IFCCARTESIANPOINT", false)]
    public void IsMaybeElement_LeavesOutRelationsAndGeometry(string ifcClass, bool expected)
        => Assert.That(IfcClasses.IsMaybeElement(ifcClass), Is.EqualTo(expected));

    [TestCase("IFCMATERIAL", true)]
    [TestCase("IFCMATERIALLAYERSETUSAGE", true)]
    [TestCase("IFCWALL", false)]
    public void IsMaterialResource_IsTheIfcMaterialFamily(string ifcClass, bool expected)
        => Assert.That(IfcClasses.IsMaterialResource(ifcClass), Is.EqualTo(expected));

    [Test]
    public void MaterialSetOf_FindsSetsOnly()
    {
        Assert.That(IfcMaterialSets.Of("IFCMATERIALLAYERSET")?.SetParameter, Is.EqualTo(IfcParameterNames.LayerSet));
        Assert.That(IfcMaterialSets.Of("IFCMATERIALLAYER"), Is.Null);
    }

    [TestCase("IFCINTEGER", true)]
    [TestCase("IFCLENGTHMEASURE", false)]
    [TestCase(null, false)]
    public void IsIntegerType(string? ifcType, bool expected)
        => Assert.That(IfcPropertyValues.IsIntegerType(ifcType), Is.EqualTo(expected));

    [Test]
    public void IsTypeObject_WhenAttributeFiveIsHasPropertySets()
    {
        string[] typeObject = ["GlobalId", "OwnerHistory", "Name", "Description", "ApplicableOccurrence", "HasPropertySets", "RepresentationMaps"];
        string[] wall = ["GlobalId", "OwnerHistory", "Name", "Description", "ObjectType", "ObjectPlacement", "Representation", "Tag"];
        Assert.Multiple(() =>
        {
            Assert.That(IfcTypeObjects.HasPropertySetsAttribute, Is.EqualTo(5));
            Assert.That(IfcTypeObjects.IsTypeObject(typeObject), Is.True);
            Assert.That(IfcTypeObjects.IsTypeObject(wall), Is.False);
            Assert.That(IfcTypeObjects.IsTypeObject(typeObject[..5]), Is.False);
        });
    }
}
