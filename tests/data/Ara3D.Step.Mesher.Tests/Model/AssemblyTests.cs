using Ara3D.IO.StepParser;
using Ara3D.Memory;

namespace Ara3D.Step.Mesher.Tests.Model;

public static class AssemblyTests
{
    static StepShapeCollector Collect(StepDocument document)
    {
        var collector = new StepShapeCollector(new StepModel(document), new StepMeshingDiagnostics());
        collector.AddProducts();
        collector.AddUnreachedShapes();
        return collector;
    }

    [Test]
    public static void TwoCubesArePlacedByTheirOccurrences()
    {
        using var document = new StepDocument(StepText.Fixture("two-cubes-assembly.stp"));
        var placed = Collect(document).Placed;
        Assert.That(placed.Select(p => p.ItemId), Is.EqualTo(new[] { 126, 126 }));
        Assert.That(placed.Select(p => p.Depth), Is.EqualTo(new[] { 1, 1 }));
        Assert.That(placed.Select(p => p.ProductDefinitionId), Is.EqualTo(new[] { 139, 139 }));
        Assert.That(placed[0].Transform.Translation.Length(), Is.LessThan(1e-9f));
        Assert.That(placed[1].Transform.Translation.X, Is.EqualTo(0.002f).Within(1e-9f));
        Assert.That(placed[1].Transform.Translation.Y, Is.EqualTo(0f).Within(1e-9f));
    }

    [Test]
    public static void SwappedItemsArePlacedTheSame()
    {
        var text = File.ReadAllText(StepText.Fixture("two-cubes-assembly.stp"))
            .Replace("#158=ITEM_DEFINED_TRANSFORMATION('','',#127,#146);", "#158=ITEM_DEFINED_TRANSFORMATION('','',#146,#127);");
        using var document = new StepDocument(System.Text.Encoding.ASCII.GetBytes(text).Fix());
        Assert.That(Collect(document).Placed[1].Transform.Translation.X, Is.EqualTo(0.002f).Within(1e-9f));
    }

    [Test]
    public static void AssemblyCycleIsRecordedNotFollowed()
    {
        using var document = StepText.Parse("""
            #1=APPLICATION_CONTEXT('');
            #2=PRODUCT_CONTEXT('',#1,'mechanical');
            #3=PRODUCT_DEFINITION_CONTEXT('part definition',#1,'design');
            #10=PRODUCT('root','root','',(#2));
            #11=PRODUCT_DEFINITION_FORMATION('','',#10);
            #12=PRODUCT_DEFINITION('design','',#11,#3);
            #20=PRODUCT('a','a','',(#2));
            #21=PRODUCT_DEFINITION_FORMATION('','',#20);
            #22=PRODUCT_DEFINITION('design','',#21,#3);
            #30=PRODUCT('b','b','',(#2));
            #31=PRODUCT_DEFINITION_FORMATION('','',#30);
            #32=PRODUCT_DEFINITION('design','',#31,#3);
            #40=NEXT_ASSEMBLY_USAGE_OCCURRENCE('1','','',#12,#22,$);
            #41=NEXT_ASSEMBLY_USAGE_OCCURRENCE('2','','',#22,#32,$);
            #42=NEXT_ASSEMBLY_USAGE_OCCURRENCE('3','','',#32,#22,$);
            """);
        var collector = Collect(document);
        Assert.That(collector.Cycles, Has.Count.EqualTo(1));
        Assert.That(collector.Cycles[0].Link, Is.EqualTo("NEXT_ASSEMBLY_USAGE_OCCURRENCE"));
        Assert.That(collector.Cycles[0].Path, Is.EqualTo(new[] { 22, 32, 22 }));
    }

    [Test]
    public static void ACycleWithNoRootIsFound()
    {
        using var document = StepText.Parse("""
            #1=APPLICATION_CONTEXT('');
            #3=PRODUCT_DEFINITION_CONTEXT('part definition',#1,'design');
            #22=PRODUCT_DEFINITION('a','',$,#3);
            #32=PRODUCT_DEFINITION('b','',$,#3);
            #41=NEXT_ASSEMBLY_USAGE_OCCURRENCE('2','','',#22,#32,$);
            #42=NEXT_ASSEMBLY_USAGE_OCCURRENCE('3','','',#32,#22,$);
            """);
        Assert.That(Collect(document).Cycles.Single().Path, Is.EqualTo(new[] { 22, 32, 22 }));
    }
}
