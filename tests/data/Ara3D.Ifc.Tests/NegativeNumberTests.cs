using System.Text;
using Ara3D.IfcLoader;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>Ara3D.IO.StepParser 1.6.1 dropped the minus sign of a number, so IFCDIRECTION((0.,0.,-1.)) read as
/// (0, 0, 1) and every negative coordinate or direction flipped. Fixed in 1.6.2 (TKT-307).</summary>
public static class NegativeNumberTests
{
    private static readonly string Ifc = MiniIfc.Document("""
        #1=IFCDIRECTION((0.,0.,-1.));
        #2=IFCCARTESIANPOINT((-2.5,1.E-3,-4.E2));
        """);

    private static double[] ReadNumbers(int id)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ara3d-negative-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, Ifc, Encoding.ASCII);
        try
        {
            using var file = new IfcFile(path, false);
            return file.EntityResolver.GetEntity(id).GetNumberList(0);
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    [Test]
    public static void DirectionKeepsItsSign()
        => Assert.That(ReadNumbers(1), Is.EqualTo(new[] { 0.0, 0.0, -1.0 }));

    [Test]
    public static void PointKeepsSignsAndExponents()
        => Assert.That(ReadNumbers(2), Is.EqualTo(new[] { -2.5, 1e-3, -400.0 }));
}
