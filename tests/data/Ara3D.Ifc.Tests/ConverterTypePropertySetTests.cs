using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>TKT-237: a type object's own property sets (HasPropertySets) become parameters on
/// the type entity, and are not copied onto its occurrences (see Ara3D.Ifc.Conventions'
/// IfcTypeObjects). Before, only sets attached by IfcRelDefinesByProperties reached BOS.</summary>
public static class ConverterTypePropertySetTests
{
    /// <summary>An IFC4 door type whose Pset_DoorCommon is in HasPropertySets only, a door of that
    /// type with its own Pset_Test, and a second door type with HasPropertySets unset.</summary>
    private static readonly string Ifc4DoorType = MiniIfc.Document("""
        #1=IFCPROPERTYSINGLEVALUE('FireRating',$,IFCLABEL('EI30'),$);
        #2=IFCPROPERTYSET('pset-type-gid',$,'Pset_DoorCommon',$,(#1));
        #3=IFCDOORTYPE('door-type-gid',$,'D1',$,$,(#2),$,$,$,.DOOR.,.SINGLE_SWING_LEFT.,$,$);
        #4=IFCDOOR('door-gid',$,'Door 1',$,$,$,$,$,2.1,0.9,.DOOR.,.SINGLE_SWING_LEFT.,$);
        #5=IFCRELDEFINESBYTYPE('rdt-gid',$,$,$,(#4),#3);
        #6=IFCPROPERTYSINGLEVALUE('Mark',$,IFCLABEL('M-1'),$);
        #7=IFCPROPERTYSET('pset-door-gid',$,'Pset_Test',$,(#6));
        #8=IFCRELDEFINESBYPROPERTIES('rdp-gid',$,$,$,(#4),#7);
        #9=IFCDOORTYPE('bare-type-gid',$,'D2',$,$,$,$,$,$,.DOOR.,.SINGLE_SWING_LEFT.,$,$);
        #10=IFCDOOR('door2-gid',$,'Door 2',$,$,$,$,$,2.1,0.9,.DOOR.,.SINGLE_SWING_LEFT.,$);
        #11=IFCRELDEFINESBYTYPE('rdt2-gid',$,$,$,(#10),#9);
        """, MiniIfc.Ifc4, "type-psets-ifc4.ifc", "ViewDefinition");

    /// <summary>An IFC2x3 door style that, like the Duplex's, lists an IfcDoorLiningProperties
    /// (not an IfcPropertySet, so it adds nothing) beside a Pset_DoorCommon.</summary>
    private static readonly string Ifc2x3DoorStyle = MiniIfc.Document("""
        #1=IFCPROPERTYSINGLEVALUE('FireRating',$,IFCLABEL('EI60'),$);
        #2=IFCPROPERTYSET('pset-type-gid',$,'Pset_DoorCommon',$,(#1));
        #3=IFCDOORLININGPROPERTIES('lining-gid',$,'Lining',$,$,$,$,$,$,$,$,$,$,$,$);
        #4=IFCDOORSTYLE('door-style-gid',$,'0864 x 2032mm',$,$,(#3,#2),$,'150378',.SINGLE_SWING_RIGHT.,.NOTDEFINED.,.F.,.F.);
        #5=IFCDOOR('door-gid',$,'Door 1',$,$,$,$,$,2.032,0.864);
        #6=IFCRELDEFINESBYTYPE('rdt-gid',$,$,$,(#5),#4);
        """, MiniIfc.Ifc2x3, "type-psets-ifc2x3.ifc", "ViewDefinition");

    [Test]
    public static void Ifc4DoorType_CarriesItsPsetDoorCommon_AndItsDoorDoesNot()
    {
        var bom = ConvertText("type-psets-ifc4", Ifc4DoorType);
        var type = ByGlobalId(bom, "door-type-gid");
        var door = ByGlobalId(bom, "door-gid");

        Assert.Multiple(() =>
        {
            Assert.That(Values(type, "Pset_DoorCommon", "FireRating"), Is.EqualTo(new[] { "EI30" }));
            Assert.That(door.Type?.Index, Is.EqualTo(type.Index), "the door reaches the value through Entity.Type");
            Assert.That(Values(door, "Pset_DoorCommon", "FireRating"), Is.Empty, "type values are not copied to occurrences");
            Assert.That(Values(door, "Pset_Test", "Mark"), Is.EqualTo(new[] { "M-1" }));
            Assert.That(ByGlobalId(bom, "bare-type-gid").Parameters.Where(p => p.Descriptor.Group == "Pset_DoorCommon"), Is.Empty);
        });
    }

    [Test]
    public static void Ifc2x3DoorStyle_CarriesItsPropertySet_AndSkipsTheLiningProperties()
    {
        var bom = ConvertText("type-psets-ifc2x3", Ifc2x3DoorStyle);
        var style = ByGlobalId(bom, "door-style-gid");

        Assert.Multiple(() =>
        {
            Assert.That(Values(style, "Pset_DoorCommon", "FireRating"), Is.EqualTo(new[] { "EI60" }));
            Assert.That(style.Parameters.Where(p => p.Descriptor.Group == "Lining"), Is.Empty);
        });
    }

    /// <summary>model_0.ifc (IFC4, Revit) gives every IfcBeamType a Pset_BeamCommon through
    /// HasPropertySets alone; no IfcRelDefinesByProperties points at it.</summary>
    [Test]
    [Category("RequiresTestData")]
    public static void Model0BeamTypes_CarryPsetBeamCommon()
    {
        var path = Path.Combine(TestData.Folder, "model_0.ifc");
        if (!File.Exists(path))
            Assert.Ignore($"{path} not found");
        var bom = Convert(path);
        var beamTypes = bom.Entities.Where(e => e.Category == "IFCBEAMTYPE").ToList();

        Assert.That(beamTypes, Is.Not.Empty);
        Assert.That(beamTypes.Select(t => Values(t, "Pset_BeamCommon", "LoadBearing").FirstOrDefault()), Is.All.EqualTo(".T."));
    }

    private static BimObjectModel ConvertText(string prefix, string ifc)
    {
        var path = Path.Combine(TestData.OutputFolder, $"{prefix}-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc, System.Text.Encoding.ASCII);
        try
        {
            return Convert(path);
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private static BimObjectModel Convert(string path)
    {
        IfcToBosConverter? converter = null;
        try
        {
            converter = new IfcToBosConverter(path);
            return new BimObjectModel(converter.BimDataBuilder.Build(), true);
        }
        finally
        {
            converter?.IfcFile.Dispose();
        }
    }

    private static EntityModel ByGlobalId(BimObjectModel bom, string globalId)
        => bom.Entities.Single(e => e.GlobalId == globalId);

    private static string?[] Values(EntityModel entity, string group, string name)
        => entity.Parameters
            .Where(p => p.Descriptor.Group == group && p.Descriptor.Name == name)
            .Select(p => p.Value?.ToString())
            .ToArray();
}
