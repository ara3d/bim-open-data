using System.Text;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>IfcToBosConverter reports, as a warning with its count, each vertex coordinate
/// BimGeometryBuilder clamped because it lies beyond the vertex range (about 214.7 km).</summary>
public static class ConverterGeometryDiagnosticsTests
{
    /// <summary>A wall whose body is a 1,000 km by 1 m box, so its mesh reaches 500 km from its origin.</summary>
    private static readonly string FarWallIfc = MiniIfc.Document("""
        #1=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
        #2=IFCUNITASSIGNMENT((#1));
        #3=IFCCARTESIANPOINT((0.,0.,0.));
        #4=IFCAXIS2PLACEMENT3D(#3,$,$);
        #5=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#4,$);
        #6=IFCPROJECT('project-gid',$,'P',$,$,$,$,(#5),#2);
        #10=IFCLOCALPLACEMENT($,#4);
        #11=IFCCARTESIANPOINT((0.,0.));
        #12=IFCAXIS2PLACEMENT2D(#11,$);
        #13=IFCRECTANGLEPROFILEDEF(.AREA.,$,#12,1000000.,1.);
        #14=IFCDIRECTION((0.,0.,1.));
        #15=IFCEXTRUDEDAREASOLID(#13,#4,#14,1.);
        #16=IFCSHAPEREPRESENTATION(#5,'Body','SweptSolid',(#15));
        #17=IFCPRODUCTDEFINITIONSHAPE($,$,(#16));
        #18=IFCWALL('wall-gid',$,'W',$,$,#10,#17,$,$);
        """, MiniIfc.Ifc4, "far-wall-test.ifc", "ViewDefinition");

    [Test]
    public static void ClampedCoordinates_BecomeAWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-far-wall-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, FarWallIfc, Encoding.ASCII);
        IfcToBosConverter? converter = null;
        try
        {
            converter = new IfcToBosConverter(path);
            var d = converter.BimDataBuilder.Build();
            var clamped = converter.BimGeometryBuilder.ClampedCoordinateCount;
            Assert.That(clamped, Is.GreaterThan(0), "the box reaches beyond the vertex range");
            Assert.That(d.Diagnostics.Where(x => x.Type == DiagnosticType.ExporterWarning).Select(x => d.Strings[(int)x.Message]),
                Has.Member($"{clamped} vertex coordinates clamped: they lie beyond about 214.7 km"));
        }
        finally
        {
            converter?.IfcFile.Dispose();
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
