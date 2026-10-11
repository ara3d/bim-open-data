using Ara3D.BimOpenSchema;

namespace Ara3D.Ifc.Conventions.Tests;

/// <summary>Pins the tables both IFC readers share. A change here changes the BOS that
/// Ara3D.Ifc.Bos and the Fragments reader write, so it should be a deliberate edit to the
/// expected values below, not an accident.</summary>
public class ConventionTableTests
{
    [Test]
    public void HiddenClasses_AreSpatialContainersZonesGridsAndAnnotations()
        => Assert.That(IfcClasses.Hidden, Is.EquivalentTo(new[]
        {
            "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCSPACE", "IFCSPATIALZONE", "IFCZONE",
            "IFCGRID", "IFCGRIDAXIS", "IFCANNOTATION", "IFCVIRTUALGRIDINTERSECTION",
        }));

    [Test]
    public void NonElementClasses_ArePinned()
        => Assert.That(IfcClasses.NonElement, Is.EquivalentTo(new[]
        {
            "IFCINDEXEDPOLYGONALFACE", "IFCPOLYLOOP", "IFCFACE", "IFCFACEOUTERBOUND", "IFCCARTESIANPOINT",
            "IFCRELDEFINESBYPROPERTIES", "IFCPROPERTYSET", "IFCPROPERTYSINGLEVALUE", "IFCAXIS2PLACEMENT3D",
            "IFCSHAPEREPRESENTATION", "IFCQUANTITYLENGTH", "IFCSTYLEDITEM", "IFCLOCALPLACEMENT",
            "IFCQUANTITYAREA", "IFCPRODUCTDEFINITIONSHAPE", "IFCCARTESIANPOINTLIST3D", "IFCMAPPEDITEM",
            "IFCPOLYGONALFACESET", "IFCINDEXEDPOLYCURVE", "IFCQUANTITYVOLUME", "IFCELEMENTQUANTITY",
            "IFCCARTESIANPOINTLIST2D", "IFCEXTRUDEDAREASOLID", "IFCINDEXEDCOLOURMAP", "IFCCOLOURRGBLIST",
            "IFCARBITRARYCLOSEDPROFILEDEF", "IFCDIRECTION",
        }));

    [Test]
    public void Relations_MapInverseNamesToBosTypes()
    {
        Assert.That(IfcRelationNames.Relations.Select(r => (r.IfcClass, r.RelationType, r.RelatedInverse, r.RelatingInverse)),
            Is.EqualTo(new[]
            {
                ("IFCRELCONTAINEDINSPATIALSTRUCTURE", RelationType.ContainedIn, "ContainedInStructure", "ContainsElements"),
                ("IFCRELAGGREGATES", RelationType.MemberOf, "Decomposes", "IsDecomposedBy"),
                ("IFCRELNESTS", RelationType.ChildOf, "Nests", "IsNestedBy"),
                ("IFCRELASSOCIATESMATERIAL", RelationType.HasMaterial, "HasAssociations", "AssociatedTo"),
            }));
        Assert.That(IfcRelationNames.ByRelatedInverse["ContainedInStructure"], Is.EqualTo(RelationType.ContainedIn));
    }

    [Test]
    public void SkippedRelatingInverses_AreTheRelatingSidesPlusDefinesOccurrenceAndReferencesElements()
        => Assert.That(IfcRelationNames.SkippedRelatingInverses, Is.EquivalentTo(new[]
        {
            "ContainsElements", "IsDecomposedBy", "IsNestedBy", "AssociatedTo", "DefinesOccurrence", "ReferencesElements",
        }));

    [Test]
    public void ParameterNames_ArePinned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(IfcParameterNames.Attribute("OverallHeight"), Is.EqualTo("Ifc:OverallHeight"));
            Assert.That(IfcParameterNames.RoomNumber, Is.EqualTo("Ifc:Room:Number"));
            Assert.That(IfcParameterNames.AxisTag, Is.EqualTo("Ifc:AxisTag"));
            Assert.That(IfcParameterNames.LengthUnit, Is.EqualTo("Ifc:LengthUnit"));
            Assert.That(IfcParameterNames.LengthUnitToMetre, Is.EqualTo("Ifc:LengthUnitToMetre"));
            Assert.That(IfcParameterNames.LayerSet, Is.EqualTo("Ifc:LayerSet"));
            Assert.That(IfcParameterNames.LayerIndex, Is.EqualTo("Ifc:LayerIndex"));
            Assert.That(IfcParameterNames.ConstituentSet, Is.EqualTo("Ifc:ConstituentSet"));
            Assert.That(IfcParameterNames.ConstituentIndex, Is.EqualTo("Ifc:ConstituentIndex"));
            Assert.That(IfcParameterNames.NotParameters, Is.EquivalentTo(new[] { "GlobalId", "OwnerHistory", "Name" }));
        });
    }

    [Test]
    public void MaterialSets_NameTheirMemberListAndParameters()
        => Assert.That(IfcMaterialSets.All.Select(s => (s.SetClass, s.MembersAttribute, s.SetParameter, s.IndexParameter)),
            Is.EqualTo(new[]
            {
                ("IFCMATERIALLAYERSET", 0, "Ifc:LayerSet", "Ifc:LayerIndex"),
                ("IFCMATERIALCONSTITUENTSET", 2, "Ifc:ConstituentSet", "Ifc:ConstituentIndex"),
            }));

    [Test]
    public void PropertyValueAttributes_AndIntegerTypes_ArePinned()
    {
        Assert.That(IfcPropertyValues.ValueAttributes, Is.EqualTo(new[]
        {
            "NominalValue", "LengthValue", "AreaValue", "VolumeValue", "CountValue", "WeightValue",
            "TimeValue", "EnumerationValues", "ListValues", "LowerBoundValue", "UpperBoundValue",
        }));
        Assert.That(IfcPropertyValues.IntegerTypes, Is.EquivalentTo(new[]
        {
            "IFCINTEGER", "IFCPOSITIVEINTEGER", "IFCDIMENSIONCOUNT", "IFCYEARNUMBER", "IFCMONTHINYEARNUMBER",
            "IFCDAYINMONTHNUMBER", "IFCDAYINWEEKNUMBER", "IFCHOURINDAY", "IFCMINUTEINHOUR",
        }));
    }
}
