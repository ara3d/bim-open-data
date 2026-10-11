using System.Text;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>IfcToBosConverter states no units on its parameters, so every descriptor's units are
/// -1 (absent), not "".</summary>
public static class ConverterAbsentUnitsTests
{
    private static readonly string UnitsIfc = MiniIfc.Document("""
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
        #2=IFCUNITASSIGNMENT((#1));
        #3=IFCPROJECT('project-gid',$,'Project',$,$,$,$,(),#2);
        #4=IFCWALL('wall-gid',$,'W',$,$,$,$,'T1',$);
        #5=IFCPROPERTYSINGLEVALUE('Width',$,IFCLENGTHMEASURE(0.2),$);
        #6=IFCPROPERTYSET('ps-gid',$,'Pset_Test',$,(#5));
        #7=IFCRELDEFINESBYPROPERTIES('rd-gid',$,$,$,(#4),#6);
        """, MiniIfc.Ifc4, "units-test.ifc", "ViewDefinition");

    [Test]
    public static void EveryDescriptor_HasAbsentUnits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-units-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, UnitsIfc, Encoding.ASCII);
        IfcToBosConverter? converter = null;
        try
        {
            converter = new IfcToBosConverter(path);
            var d = converter.BimDataBuilder.Build();
            Assert.That(d.Descriptors, Is.Not.Empty);
            Assert.That(d.Descriptors.Select(x => x.Units), Is.All.EqualTo(BimDataBuilder.InvalidStringIndex));
        }
        finally
        {
            converter?.IfcFile.Dispose();
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
