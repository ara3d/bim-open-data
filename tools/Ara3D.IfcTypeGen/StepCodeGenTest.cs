using Ara3D.Utils;

namespace Ara3D.IfcTypeGen;

/// <summary>
/// Generates the Ara3D.StepTypes source from the AP242 long form, which is fetched, never committed (see
/// <see cref="StepSchemaFiles"/>). One AP242 layer reads AP203 and AP214 files too: the three share the Part 42
/// and Part 43 entities.
/// </summary>
public static class StepCodeGenTest
{
    public const string Source = "the AP242 edition 3 long form (data/express/ap242ed3)";

    public static readonly DirectoryPath OutputFolder
        = PathUtil.GetCallerSourceFolder().RelativeFolder("..", "..", "src", "data", "Ara3D.StepTypes");

    public static IReadOnlyDictionary<string, string> Emit()
        => new StepCodeEmitter(StepSchemaLayoutTests.LoadSchemaOrIgnore(StepSchemaFiles.Ap242), Source).Files();

    [Test, Explicit]
    public static void TestGenerate()
    {
        foreach (var (name, text) in Emit())
            OutputFolder.RelativeFile(name).WriteAllText(text);
    }

    [Test]
    public static void GeneratedFilesAreCurrent()
    {
        foreach (var (name, text) in Emit())
            Assert.That(OutputFolder.RelativeFile(name).ReadAllText().ReplaceLineEndings(), Is.EqualTo(text.ReplaceLineEndings()),
                $"{name} differs from the generator's output; run TestGenerate");
    }
}
