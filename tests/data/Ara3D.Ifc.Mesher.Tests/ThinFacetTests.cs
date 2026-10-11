using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>Sub-millimetre shell facets, as on the helix of the Institute models: ear clipping used to
/// skip them; they must mesh as single triangles.</summary>
[TestFixture]
public sealed class ThinFacetTests
{
    [Test]
    public void MeshesThinHelixTriangleShellFace()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((1.5075,0.348334,0.372727));
            #2=IFCCARTESIANPOINT((1.5075,0.,0.236364));
            #3=IFCCARTESIANPOINT((1.5074,0.348334,0.372727));
            #10=IFCPOLYLOOP((#1,#2,#3));
            #11=IFCFACEOUTERBOUND(#10,.T.);
            #12=IFCFACE((#11));
            #13=IFCOPENSHELL((#12));
            #14=IFCSHELLBASEDSURFACEMODEL((#13));
            """);

        var mesh = MeshRequired(model, 14);
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(1));
    }

    [Test]
    public void MeshesHelixOpenShellWithMultipleThinFacets()
    {
        using var model = MicroIfc.Parse("""
            #1=IFCCARTESIANPOINT((1.5075,0.348334,0.372727));
            #2=IFCCARTESIANPOINT((1.5075,0.,0.236364));
            #3=IFCCARTESIANPOINT((1.5074,0.348334,0.372727));
            #4=IFCCARTESIANPOINT((1.5074,0.,0.236364));
            #5=IFCCARTESIANPOINT((1.5075,0.348334,0.372827));
            #6=IFCCARTESIANPOINT((1.5075,0.,0.236464));
            #10=IFCPOLYLOOP((#1,#2,#3));
            #11=IFCFACEOUTERBOUND(#10,.T.);
            #12=IFCFACE((#11));
            #20=IFCPOLYLOOP((#2,#1,#5,#6));
            #21=IFCFACEOUTERBOUND(#20,.T.);
            #22=IFCFACE((#21));
            #30=IFCPOLYLOOP((#2,#4,#3));
            #31=IFCFACEOUTERBOUND(#30,.T.);
            #32=IFCFACE((#31));
            #40=IFCOPENSHELL((#12,#22,#32));
            #41=IFCSHELLBASEDSURFACEMODEL((#40));
            """);

        var mesh = MeshRequired(model, 41);
        Assert.That(mesh.FaceIndices, Has.Count.EqualTo(4));
    }

    static TriangleMesh3D MeshRequired(MicroIfcModel model, int entityId)
    {
        var mesh = GeometryDispatcher.TryBuild(model.Context, model.Entity(entityId));
        if (mesh is null)
        {
            var notes = model.Context.Diagnostics.Messages.TakeLast(3);
            var detail = notes.Any() ? string.Join("; ", notes) : "no diagnostics";
            throw new InvalidOperationException($"Could not mesh entity #{entityId}: {detail}");
        }
        return mesh.Value;
    }
}
