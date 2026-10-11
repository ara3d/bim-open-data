using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>Advanced faces on curved surfaces.</summary>
[TestFixture]
public sealed class AdvancedFaceTests
{
    [Test]
    public void CylindricalAdvancedFace_DensifiesLongArcChords()
    {
        // Quarter-cylinder panel with only 4 boundary corners: densify should add arc samples.
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCDIRECTION((1.,0.,0.));
            #3=IFCDIRECTION((0.,0.,1.));
            #4=IFCAXIS2PLACEMENT3D(#1,#3,#2);
            #5=IFCCYLINDRICALSURFACE(#4,1.);
            #10=IFCCARTESIANPOINT((1.,0.,0.));
            #11=IFCCARTESIANPOINT((0.,1.,0.));
            #12=IFCCARTESIANPOINT((0.,1.,2.));
            #13=IFCCARTESIANPOINT((1.,0.,2.));
            #14=IFCPOLYLOOP((#10,#11,#12,#13));
            #15=IFCFACEOUTERBOUND(#14,.T.);
            #16=IFCADVANCEDFACE((#15),#5,.T.);
            #17=IFCCLOSEDSHELL((#16));
            #18=IFCADVANCEDBREP(#17);
            """);

        var mesh = Brep.BuildAdvancedBrep(model.Context, model.Entity(18));
        Assert.That(mesh.FaceIndices.Count, Is.GreaterThan(2),
            "cylindrical face should densify beyond a single flat quad");
        Assert.That(mesh.Points.Count, Is.GreaterThan(4));
    }
}
