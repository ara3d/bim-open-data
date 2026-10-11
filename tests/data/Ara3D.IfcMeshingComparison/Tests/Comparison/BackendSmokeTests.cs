using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Meshers;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

[TestFixture]
public sealed class BackendSmokeTests
{
    [Test]
    public void Catalog_NamesFiveBackends()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MesherCatalog.All.Select(b => b.Name), Is.EquivalentTo(new[]
            {
                IfcMesher.Name, "WebIfcDll", MesherCatalog.WebIfc, MesherCatalog.IfcLite, MesherCatalog.IfcOpenShell,
            }));
            Assert.That(MesherCatalog.All, Has.All.Matches<IMeshingBackend>(b => !string.IsNullOrWhiteSpace(b.Description)));
            Assert.That(MesherCatalog.Dumped, Has.Count.EqualTo(3));
        });
    }
}
