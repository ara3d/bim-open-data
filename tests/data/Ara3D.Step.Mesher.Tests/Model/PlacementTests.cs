using SN = System.Numerics;

namespace Ara3D.Step.Mesher.Tests.Model;

public static class PlacementTests
{
    const string Units = """
        #1=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
        #2=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT((#1))REPRESENTATION_CONTEXT('',''));
        #3=CARTESIAN_POINT('',(0.,0.,0.));
        #4=DIRECTION('',(0.,0.,1.));
        #5=DIRECTION('',(1.,0.,0.));
        #6=AXIS2_PLACEMENT_3D('',#3,#4,#5);
        #7=CARTESIAN_POINT('',(10.,0.,0.));
        #8=DIRECTION('',(0.,1.,0.));
        #9=AXIS2_PLACEMENT_3D('',#7,#4,#8);
        """;

    const string Part = """
        #10=CLOSED_SHELL('',());
        #11=MANIFOLD_SOLID_BREP('part',#10);
        #12=ADVANCED_BREP_SHAPE_REPRESENTATION('part',(#11,#6),#2);
        #13=REPRESENTATION_MAP(#6,#12);
        """;

    static (IReadOnlyList<StepPlacedItem> Placed, StepShapeCollector Collector) Collect(string data)
    {
        using var document = StepText.Parse(data);
        var collector = new StepShapeCollector(new StepModel(document), new StepMeshingDiagnostics());
        collector.AddUnreachedShapes();
        return (collector.Placed, collector);
    }

    static SN.Vector3 Apply(SN.Matrix4x4 m, float x, float y, float z)
        => SN.Vector3.Transform(new SN.Vector3(x, y, z), m);

    [Test]
    public static void Axis2PlacementFrameIsInMetres()
    {
        using var document = StepText.Parse(Units);
        var units = new StepUnits(0.001, null);
        var frame = document.Entity(9).Frame(units)!.Value;
        var p = Apply(frame, 1, 0, 0);
        Assert.That(p.X, Is.EqualTo(0.01f).Within(1e-7f));
        Assert.That(p.Y, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public static void MappedItemsPlaceOneItemTwice()
    {
        var (placed, _) = Collect(Units + Part + """
            #14=MAPPED_ITEM('',#13,#6);
            #15=MAPPED_ITEM('',#13,#9);
            #16=SHAPE_REPRESENTATION('two',(#14,#15),#2);
            """);
        Assert.That(placed.Select(p => p.ItemId), Is.EqualTo(new[] { 11, 11 }));
        var second = Apply(placed[1].Transform, 0.001f, 0, 0);
        Assert.That(second.X, Is.EqualTo(0.01f).Within(1e-7f));
        Assert.That(second.Y, Is.EqualTo(0.001f).Within(1e-7f));
    }

    [Test]
    public static void SameShapeRelationshipBringsInTheBrepRepresentation()
    {
        var (placed, _) = Collect(Units + """
            #10=CLOSED_SHELL('',());
            #11=MANIFOLD_SOLID_BREP('part',#10);
            #12=ADVANCED_BREP_SHAPE_REPRESENTATION('part',(#11,#6),#2);
            #13=SHAPE_REPRESENTATION('part',(#6),#2);
            #14=SHAPE_REPRESENTATION_RELATIONSHIP('','',#13,#12);
            """);
        Assert.That(placed.Select(p => p.ItemId), Is.EqualTo(new[] { 11 }));
    }

    [Test]
    public static void MappedItemCycleIsRecordedNotFollowed()
    {
        var (placed, collector) = Collect(Units + Part + """
            #20=SHAPE_REPRESENTATION('a',(#6,#23),#2);
            #21=REPRESENTATION_MAP(#6,#20);
            #22=MAPPED_ITEM('',#21,#6);
            #23=MAPPED_ITEM('',#24,#6);
            #24=REPRESENTATION_MAP(#6,#25);
            #25=SHAPE_REPRESENTATION('b',(#6,#22),#2);
            #26=SHAPE_REPRESENTATION('root',(#22),#2);
            """);
        Assert.That(collector.Cycles, Has.Count.EqualTo(1));
        Assert.That(collector.Cycles[0].Path, Is.EqualTo(new[] { 20, 25, 20 }));
    }
}
