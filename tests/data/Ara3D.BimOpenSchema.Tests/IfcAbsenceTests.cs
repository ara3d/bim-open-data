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
        """, MiniIfc.Ifc4, "absence-test.ifc", "ViewDefinition");

    static BimData Convert()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-absence-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, AbsenceIfc, Encoding.ASCII);
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
