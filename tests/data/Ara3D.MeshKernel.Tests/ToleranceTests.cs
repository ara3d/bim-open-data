namespace Ara3D.MeshKernel.Tests;

public sealed class ToleranceTests
{
    [Test]
    public void Eps_IsOneMicrometre()
        => Assert.That(Tolerance.Eps, Is.EqualTo(1e-6f));
}
