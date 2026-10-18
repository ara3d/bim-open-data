namespace Ara3D.Step.Mesher.Tests;

public static class SmokeTests
{
    [Test]
    public static void FixturesAreCopied()
        => Assert.That(File.Exists(Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", "unit-cube.stp")));
}
