using System.Text;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Ifc.Conventions;
using Ara3D.IfcLoader;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>IfcToBosConverter and IfcEntity.GetEntityLabel name an entity by
/// Ara3D.Ifc.Conventions' IfcNames, the rule the Fragments reader applies to the same entity.</summary>
public static class EntityNameTests
{
    private static readonly string NamesIfc = MiniIfc.Document("""
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
        #2=IFCSPACE('space-gid',$,'101',$,$,$,$,'Kitchen',.ELEMENT.,.INTERNAL.,$);
        #3=IFCSPACE('space2-gid',$,'102',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        #4=IFCMATERIAL('Concrete',$,$);
        #5=IFCMATERIALLAYER(#4,0.2,$,'Core',$,$,$);
        #6=IFCMATERIALLAYERSET((#5),'Wall 200',$);
        #7=IFCGRIDAXIS('A',$,.T.);
        #8=IFCWALL('wall-gid',$,$,$,$,$,$,$,$);
        """, MiniIfc.Ifc4, "names-test.ifc", "ViewDefinition");

    private static string WriteTempIfc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-names-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, NamesIfc, Encoding.ASCII);
        return path;
    }

    private static T WithFile<T>(Func<string, T> read)
    {
        var path = WriteTempIfc();
        try { return read(path); }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private static BimData Convert()
        => WithFile(path =>
        {
            var converter = new IfcToBosConverter(path);
            try { return converter.BimDataBuilder.Build(); }
            finally { converter.IfcFile.Dispose(); }
        });

    private static string? NameOf(BimData d, long localId)
        => d.Entities.Single(e => e.LocalId == localId).Name is var n && (int)n >= 0 ? d.Strings[(int)n] : null;

    private static string? RoomNumberOf(BimData d, long localId)
    {
        var entity = Array.FindIndex(d.Entities, e => e.LocalId == localId);
        return d.Parameters
            .Where(p => (int)p.Entity == entity && d.Strings[(int)d.Descriptors[(int)p.Descriptor].Name] == IfcParameterNames.RoomNumber)
            .Select(p => d.Strings[p.Value])
            .SingleOrDefault();
    }

    [TestCase(1, "METRE", null)]
    [TestCase(2, "Kitchen", "101")]
    [TestCase(3, "102", "102")]
    [TestCase(5, "Core", null)]
    [TestCase(6, "Wall 200", null)]
    [TestCase(7, null, null)]
    [TestCase(8, null, null)]
    public static void Converter_NamesByTheSharedRule(int localId, string? name, string? roomNumber)
    {
        var d = Convert();
        Assert.That((NameOf(d, localId), RoomNumberOf(d, localId)), Is.EqualTo((name, roomNumber)));
    }

    /// <summary>The label is the name, or "#id" when there is none; IfcGridAxis has no name
    /// attribute (its attribute 2 is SameSense, .T.).</summary>
    [TestCase(1, "METRE")]
    [TestCase(2, "Kitchen")]
    [TestCase(6, "Wall 200")]
    [TestCase(7, "#7")]
    [TestCase(8, "#8")]
    public static void EntityLabel_IsTheNameOrTheId(int id, string label)
        => Assert.That(WithFile(path =>
        {
            using var file = new IfcFile(path, false);
            return file.EntityResolver.GetEntity(id).GetEntityLabel();
        }), Is.EqualTo(label));
}
