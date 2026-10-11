using Ara3D.Geometry;
using Ara3D.Ifc.Mesher;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>How a mapped item's transform composes with its representation map's origin and the
/// product's placement, including mirrors (operators with a negative determinant).</summary>
[TestFixture]
public sealed class MappedItemTransformTests
{
    [Test]
    public void InstanceMatrix_UsesPartTimesProductRowVectorOrder()
    {
        var part = Matrix4x4.CreateRotationZ((Angle)(MathF.PI / 4));
        var product = Matrix4x4.CreateTranslation(new Vector3(10, 20, 0))
            * Matrix4x4.CreateRotationZ((Angle)(MathF.PI / 3));
        var correct = part * product;
        var wrong = product * part;
        var productOrigin = Vector3.Zero.Transform(product);
        var correctWorld = Vector3.Zero.Transform(correct);
        var wrongWorld = Vector3.Zero.Transform(wrong);
        Assert.That((float)correctWorld.X, Is.EqualTo((float)productOrigin.X).Within(1e-3f));
        Assert.That((float)correctWorld.Y, Is.EqualTo((float)productOrigin.Y).Within(1e-3f));
        Assert.That((float)(wrongWorld - productOrigin).Length, Is.GreaterThan(1f),
            "product*part rotates the product placement away from oracle");
    }

    [Test]
    public void MappedItemTransform_IsTargetTimesOrigin()
    {
        using var model = MicroIfc.Parse(
            """
            #1=IFCCARTESIANPOINT((1000.,0.,0.));
            #2=IFCDIRECTION((1.,0.,0.));
            #3=IFCDIRECTION((0.,0.,1.));
            #4=IFCAXIS2PLACEMENT3D(#1,#3,#2);
            #5=IFCCARTESIANPOINT((0.,0.,0.));
            #6=IFCDIRECTION((1.,0.,0.));
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCAXIS2PLACEMENT3D(#5,#7,#6);
            #9=IFCSHAPEREPRESENTATION($,'Body','Tessellation',());
            #10=IFCREPRESENTATIONMAP(#4,#9);
            #11=IFCCARTESIANPOINT((0.,0.,0.));
            #12=IFCDIRECTION((1.,0.,0.));
            #13=IFCDIRECTION((0.,0.,1.));
            #14=IFCCARTESIANTRANSFORMATIONOPERATOR3D(#12,$,#11,$,#13);
            #15=IFCMAPPEDITEM(#10,#14);
            """,
            lengthScaleOverride: 0.001);

        var ctx = model.Context;
        var mapped = ctx.GetEntity(15);
        Assert.That(GeometryDispatcher.TryGetMappedItemTransform(ctx, mapped, out var transform), Is.True);

        var origin = Placements.ReadAxis2Placement3D(ctx, ctx.GetEntity(4));
        var target = Placements.ReadCartesianTransformationOperator3D(ctx, ctx.GetEntity(14));
        var expected = target * origin.Matrix;
        Assert.That(MatrixNorms.Frobenius(transform - expected), Is.LessThan(1e-3));
    }

    [Test]
    public void LocalPlacement_ChainComposesParentThenRelative()
    {
        using var model = MicroIfc.Parse(
            """
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCDIRECTION((1.,0.,0.));
            #3=IFCDIRECTION((0.,0.,1.));
            #4=IFCAXIS2PLACEMENT3D(#1,#3,#2);
            #5=IFCLOCALPLACEMENT($,#4);
            #6=IFCCARTESIANPOINT((0.,0.,3000.));
            #7=IFCAXIS2PLACEMENT3D(#6,#3,#2);
            #8=IFCLOCALPLACEMENT(#5,#7);
            #9=IFCCARTESIANPOINT((1000.,2000.,0.));
            #10=IFCAXIS2PLACEMENT3D(#9,#3,#2);
            #11=IFCLOCALPLACEMENT(#8,#10);
            """,
            lengthScaleOverride: 0.001);

        var frame = Placements.ReadLocalPlacement(model.Context, model.Entity(11));
        Assert.That((float)frame.Origin.X, Is.EqualTo(1f).Within(1e-3f));
        Assert.That((float)frame.Origin.Y, Is.EqualTo(2f).Within(1e-3f));
        Assert.That((float)frame.Origin.Z, Is.EqualTo(3f).Within(1e-3f));
    }

    [Test]
    public void MappedItemTransform_RotatedMappingOrigin_IsTargetTimesOrigin()
    {
        using var model = MicroIfc.Parse(
            """
            #1=IFCRECTANGLEPROFILEDEF(.AREA.,'BoxProfile',$,2.,2.);
            #2=IFCDIRECTION((0.,0.,1.));
            #3=IFCEXTRUDEDAREASOLID(#1,$,#2,4.);
            #4=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#3));
            #5=IFCCARTESIANPOINT((100.,0.,0.));
            #6=IFCDIRECTION((0.,0.,1.));
            #7=IFCDIRECTION((0.,1.,0.));
            #8=IFCAXIS2PLACEMENT3D(#5,#6,#7);
            #9=IFCREPRESENTATIONMAP(#8,#4);
            #10=IFCCARTESIANPOINT((0.,0.,0.));
            #11=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#10,1.,$);
            #12=IFCMAPPEDITEM(#9,#11);
            """,
            lengthScaleOverride: 0.001);

        var ctx = model.Context;
        var mapped = ctx.GetEntity(12);
        Assert.That(GeometryDispatcher.TryGetMappedItemTransform(ctx, mapped, out var transform), Is.True);

        var origin = Placements.ReadAxis2Placement3D(ctx, ctx.GetEntity(8));
        var target = Placements.ReadCartesianTransformationOperator3D(ctx, ctx.GetEntity(11));
        var expected = target * origin.Matrix;
        Assert.That(MatrixNorms.Frobenius(transform - expected), Is.LessThan(1e-3));
    }

    [Test]
    public void MirroredMappedItem3D_HonorsNegativeDeterminant_AndFlipsWinding()
    {
        // Box centered at local y=+1 m, mapped through an operator whose Axis2 = (0,-1,0):
        // a reflection across the XZ plane (determinant -1). It must land at y=-1 (mirrored, not
        // rotated) and keep its outward winding (signed volume sign preserved).
        using var model = MicroIfc.Parse(
            """
            #1=IFCCARTESIANPOINT((0.,1000.,0.));
            #2=IFCAXIS2PLACEMENT2D(#1,$);
            #3=IFCRECTANGLEPROFILEDEF(.AREA.,'P',#2,2000.,2000.);
            #4=IFCDIRECTION((0.,0.,1.));
            #5=IFCEXTRUDEDAREASOLID(#3,$,#4,4000.);
            #6=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#5));
            #7=IFCCARTESIANPOINT((0.,0.,0.));
            #8=IFCDIRECTION((0.,0.,1.));
            #9=IFCDIRECTION((1.,0.,0.));
            #10=IFCAXIS2PLACEMENT3D(#7,#8,#9);
            #11=IFCREPRESENTATIONMAP(#10,#6);
            #12=IFCDIRECTION((1.,0.,0.));
            #13=IFCDIRECTION((0.,-1.,0.));
            #14=IFCCARTESIANPOINT((0.,0.,0.));
            #15=IFCDIRECTION((0.,0.,1.));
            #16=IFCCARTESIANTRANSFORMATIONOPERATOR3D(#12,#13,#14,1.,#15);
            #17=IFCMAPPEDITEM(#11,#16);
            """,
            lengthScaleOverride: 0.001);

        var ctx = model.Context;
        var op = Placements.ReadCartesianTransformationOperator3D(ctx, ctx.GetEntity(16));
        Assert.That(MeshHelpers.LinearDeterminant(op), Is.LessThan(0),
            "mirror operator must keep negative determinant, not be forced right-handed");

        var reference = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(5));
        Assert.That(reference, Is.Not.Null);
        var refVol = MeshHelpers.SignedVolume(reference!.Value);
        var refCenter = MeshHelpers.GetBounds(reference.Value).Center;

        var mapped = GeometryDispatcher.TryBuild(ctx, ctx.GetEntity(17));
        Assert.That(mapped, Is.Not.Null);
        var mappedVol = MeshHelpers.SignedVolume(mapped!.Value);
        var mappedCenter = MeshHelpers.GetBounds(mapped.Value).Center;

        Assert.That((float)mappedCenter.Y, Is.EqualTo(-(float)refCenter.Y).Within(1e-3f),
            "mirrored box centroid Y should be negated");
        Assert.That((float)mappedCenter.X, Is.EqualTo((float)refCenter.X).Within(1e-3f));
        Assert.That(Math.Sign(mappedVol), Is.EqualTo(Math.Sign(refVol)),
            "winding flip must preserve signed-volume sign under a mirror");
        Assert.That(Math.Abs(mappedVol), Is.EqualTo(Math.Abs(refVol)).Within(1e-4),
            "mirror preserves volume magnitude");
    }

    [Test]
    public void MirroredProfileOperator2D_ReflectsRing()
    {
        // Derived profile via a 2D operator with Axis2 = (0,-1): reflect the parent ring across X.
        using var model = MicroIfc.Parse(
            """
            #1=IFCCARTESIANPOINT((0.,1000.));
            #2=IFCAXIS2PLACEMENT2D(#1,$);
            #3=IFCRECTANGLEPROFILEDEF(.AREA.,'P',#2,2000.,2000.);
            #4=IFCDIRECTION((1.,0.));
            #5=IFCDIRECTION((0.,-1.));
            #6=IFCCARTESIANPOINT((0.,0.));
            #7=IFCCARTESIANTRANSFORMATIONOPERATOR2D(#4,#5,#6,1.);
            #8=IFCDERIVEDPROFILEDEF(.AREA.,'D',#3,#7,$);
            """,
            lengthScaleOverride: 0.001);

        var ctx = model.Context;
        var op = Placements.ReadCartesianTransformationOperator2D(ctx, ctx.GetEntity(7));
        Assert.That(MeshHelpers.LinearDeterminant(op), Is.LessThan(0),
            "2D mirror operator must keep negative determinant");

        var parent = ProfileBuilder.Build(ctx, ctx.GetEntity(3));
        var derived = ProfileBuilder.Build(ctx, ctx.GetEntity(8));
        var parentY = parent.Outer.Average(p => (double)p.Y.Value);
        var derivedY = derived.Outer.Average(p => (double)p.Y.Value);
        Assert.That(parentY, Is.EqualTo(1.0).Within(1e-3), "parent ring centered at y=+1");
        Assert.That(derivedY, Is.EqualTo(-parentY).Within(1e-3), "mirrored ring centered at y=-1");
    }
}
