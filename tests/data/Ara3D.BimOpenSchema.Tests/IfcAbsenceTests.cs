using System.Text;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using BimOpenData.TestSupport;

namespace Ara3D.BIMOpenSchema.Tests;

/// <summary>
/// IfcToBosConverter keeps what the IFC file leaves out absent (index -1, or no parameter row)
/// instead of writing "", "$", or a made-up "#id" name; an empty string the file states stays "".
/// </summary>
public static class IfcAbsenceTests
{
    static readonly string AbsenceIfc = MiniIfc.Document("""
        #10=IFCWALL('wall-gid',$,$,$,$,$,$);
        #11=IFCPROPERTYSINGLEVALUE('Comment',$,$,$);
        #14=IFCPROPERTYSINGLEVALUE('Mark',$,IFCLABEL(''),$);
        #12=IFCPROPERTYSET('ps-gid',$,'Pset_Test',$,(#11,#14));
        #13=IFCRELDEFINESBYPROPERTIES('rd-gid',$,$,$,(#10),#12);
        #20=IFCMATERIAL('Concrete',$,$);
        #21=IFCRELASSOCIATESMATERIAL('am-gid',$,$,$,(#10),#20);
        #30=IFCCARTESIANPOINT((0.,0.));
        #31=IFCAXIS2PLACEMENT2D(#30,$);
        #32=IFCRECTANGLEPROFILEDEF(.AREA.,$,#31,1.,2.);
        #33=IFCRECTANGLEPROFILEDEF(.AREA.,'200x400',#31,0.2,0.4);
        #34=IFCPRESENTATIONLAYERASSIGNMENT('A-WALL',$,(#10),$);
        #35=IFCCOLOURRGB($,0.5,0.5,0.5);
        #36=IFCWALL('wall2-gid',$,'#7',$,$,$,$);
        """, MiniIfc.Ifc4, "absence-test.ifc", "ViewDefinition");

    static BimData Convert() => Convert(AbsenceIfc);

    static BimData Convert(string ifc)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-absence-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc, Encoding.ASCII);
        IfcToBosConverter? converter = null;
        try
        {
            converter = new IfcToBosConverter(path);
            return converter.BimDataBuilder.Build();
        }
        finally
        {
            converter?.IfcFile.Dispose();
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    static Entity EntityWithLocalId(BimData d, long localId)
        => d.Entities.Single(e => e.LocalId == localId);

    [Test]
    public static void UnsetNameIsAbsent()
    {
        var wall = EntityWithLocalId(Convert(), 10);
        Assert.That(wall.Name, Is.EqualTo(BimDataBuilder.InvalidStringIndex), "an unset IfcRoot.Name is -1, not \"#10\"");
    }

    /// <summary>Attribute 2 is Name only on an IfcRoot: on IfcRectangleProfileDef it is Position, a
    /// reference whose text "#31" was stored as the name. A name now comes from the class's own name
    /// attribute (ProfileName, IfcPresentationLayerAssignment.Name) and only from a string.</summary>
    [Test]
    public static void NameComesFromTheClassNameAttributeAndOnlyFromAString()
    {
        var d = Convert();
        string? NameOf(long localId) => d.Get(EntityWithLocalId(d, localId).Name);
        Assert.Multiple(() =>
        {
            Assert.That(NameOf(32), Is.Null, "unset ProfileName; attribute 2 is the reference #31");
            Assert.That(NameOf(33), Is.EqualTo("200x400"));
            Assert.That(NameOf(34), Is.EqualTo("A-WALL"));
            Assert.That(NameOf(35), Is.Null, "unset IfcColourRgb.Name; attribute 2 is a number");
            Assert.That(NameOf(36), Is.EqualTo("#7"), "a name the file states is kept, whatever it looks like");
        });
    }

    [Test]
    public static void NoNameStartsWithHashUnlessTheFileSaysSo()
    {
        var d = Convert();
        var hashNames = d.Entities.Where(e => d.Get(e.Name)?.StartsWith('#') == true).Select(e => e.LocalId);
        Assert.That(hashNames, Is.EquivalentTo(new[] { 36L }));
    }

    [Test]
    public static void EntityThatIsNotAnIfcRootHasNoGlobalId()
    {
        var d = Convert();
        var material = EntityWithLocalId(d, 20);
        Assert.Multiple(() =>
        {
            Assert.That(material.GlobalId, Is.EqualTo(BimDataBuilder.InvalidStringIndex));
            Assert.That(d.Get(material.Name), Is.EqualTo("Concrete"));
            Assert.That(d.Get(EntityWithLocalId(d, 10).GlobalId), Is.EqualTo("wall-gid"));
        });
    }

    [Test]
    public static void CategoryEntitiesHaveNoGlobalId()
    {
        var d = Convert();
        var categories = d.Entities.Where(e => e.LocalId == -1).ToList();
        Assert.That(categories, Is.Not.Empty);
        Assert.That(categories.Select(e => e.GlobalId), Has.All.EqualTo(BimDataBuilder.InvalidStringIndex));
    }

    [Test]
    public static void PropertyWithoutValueAddsNoRow_AndAnEmptyValueIsKept()
    {
        var d = Convert();
        var wall = (EntityIndex)Array.FindIndex(d.Entities, e => e.LocalId == 10);
        var values = d.Parameters
            .Where(p => p.Entity == wall)
            .ToDictionary(p => d.ParameterName(p)!, p => d.ParameterValue(p));
        Assert.Multiple(() =>
        {
            Assert.That(values.Keys, Has.None.EqualTo("Comment"));
            Assert.That(values["Mark"], Is.EqualTo(""));
        });
    }
}
