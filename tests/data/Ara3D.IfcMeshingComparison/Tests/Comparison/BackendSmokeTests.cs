using Ara3D.Ifc.Mesher;
using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.IfcMeshingComparison.Meshers;
using Ara3D.IfcMeshingComparison.Reporting;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Tests.Comparison;

[TestFixture]
public sealed class BackendSmokeTests
{
    static readonly IMeshingBackend[] AllBackends =
    [
        new WebIfcBackend(),
        new IfcMesherBackend(),
    ];

    [Test]
    public void Backends_Instantiate_WithExpectedNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AllBackends, Has.Length.EqualTo(2));
            Assert.That(AllBackends.Select(b => b.Name), Is.EquivalentTo(new[] { "WebIfcDll", IfcMesher.Name }));
            Assert.That(AllBackends, Has.All.Matches<IMeshingBackend>(b => !string.IsNullOrWhiteSpace(b.Description)));
        });
    }
}
