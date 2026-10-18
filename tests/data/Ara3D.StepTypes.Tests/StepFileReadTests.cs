using Ara3D.IO.StepParser;

namespace Ara3D.StepTypes.Tests;

/// <summary>Reads the u-blox SAM antenna file (fixtures/README.md) through generated descriptors, simple and complex
/// instances alike.</summary>
public static class StepFileReadTests
{
    private static StepDocument _document = null!;
    private static StepModel _model = null!;

    [OneTimeSetUp]
    public static void Open()
    {
        _document = new StepDocument(System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", "SAM_AP214.STEP"));
        _model = new StepModel(_document);
    }

    [OneTimeTearDown]
    public static void Close()
        => _document.Dispose();

    [Test]
    public static void OpensWithoutDiagnostics()
    {
        Assert.That(_document.Diagnostics, Is.Empty);
        Assert.That(_model.Count, Is.EqualTo(4937));
    }

    [Test]
    public static void CartesianPointCoordinates()
    {
        var point = _model.Get(1);
        Assert.That(point.Is<ICartesianPoint>() && point.Is<IRepresentationItem>(), Is.True);
        Assert.That(point.Numbers(CartesianPoint.Instance.Coordinates), Is.EqualTo(new[] { 7.4, 1.95, -1.1 }).Within(1e-12));
    }

    [Test]
    public static void AdvancedFaceGeometryAndSense()
    {
        var face = _model.Get(3242);
        Assert.That(face.Is<IAdvancedFace>() && face.Is<IFace>(), Is.True);
        Assert.That(face.Ref(AdvancedFace.Instance.FaceGeometry)?.Id, Is.EqualTo(659));
        Assert.That(face.Boolean(AdvancedFace.Instance.SameSense), Is.True);

        var planar = _model.Get(127);
        Assert.That(planar.Ref(AdvancedFace.Instance.FaceGeometry)?.Is<IPlane>(), Is.True);
        Assert.That(planar.Boolean(AdvancedFace.Instance.SameSense), Is.False);
    }

    [Test]
    public static void SupertypeDescriptorReadsASubtype()
    {
        var bounds = _model.Get(127).Refs(Face.Instance.Bounds);
        Assert.That(bounds?.Select(b => b.Id), Is.EqualTo(new[] { 858 }));
        Assert.That(bounds![0].Is<IFaceOuterBound>() && bounds[0].Is<IFaceBound>(), Is.True);
    }

    [Test]
    public static void LengthUnitPrefixFromAComplexInstance()
    {
        var unit = _model.Get(81);
        Assert.That(unit.IsComplex, Is.True);
        Assert.That(unit.Is<ILengthUnit>() && unit.Is<INamedUnit>() && unit.Is<ISiUnit>(), Is.True);
        Assert.That(unit.Is<IPlaneAngleUnit>(), Is.False);
        Assert.That(unit.Enum(SiUnit.Instance.Prefix), Is.EqualTo(SiPrefix.Milli));
        Assert.That(unit.Enum(SiUnit.Instance.Name), Is.EqualTo(SiUnitName.Metre));
        Assert.That(unit.TryGet(SiUnit.Instance.Dimensions, out var dimensions) && dimensions.IsRedeclared, Is.True);
        Assert.That(unit.Value(SiUnit.Instance.Dimensions), Is.Null);
    }

    [Test]
    public static void UnsetPrefixReadsAsNull()
    {
        var radian = _model.Get(46);
        Assert.That(radian.Is<IPlaneAngleUnit>(), Is.True);
        Assert.That(radian.Enum(SiUnit.Instance.Prefix), Is.Null);
        Assert.That(radian.Enum(SiUnit.Instance.Name), Is.EqualTo(SiUnitName.Radian));
    }

    [Test]
    public static void RationalBSplineSurfaceFromAComplexInstance()
    {
        var surface = _model.Get(659);
        Assert.That(surface.Is<IRationalBSplineSurface>() && surface.Is<IBSplineSurfaceWithKnots>() && surface.Is<ISurface>(), Is.True);
        Assert.That(surface.Is<IPlane>(), Is.False);

        var weights = surface.Numbers(RationalBSplineSurface.Instance.WeightsData);
        Assert.That(weights, Has.Count.EqualTo(4));
        Assert.That(weights![0], Is.EqualTo(new[] { 1.0, 0.8047378541243651600, 0.8047378541243651600, 1.0 }).Within(1e-15));
        Assert.That(weights[1][1], Is.EqualTo(0.6476030138606861900).Within(1e-15));

        Assert.That(surface.Integer(BSplineSurface.Instance.UDegree), Is.EqualTo(3));
        Assert.That(surface.Enum(BSplineSurface.Instance.SurfaceForm), Is.EqualTo(BSplineSurfaceForm.Unspecified));
        Assert.That(surface.Numbers(BSplineSurfaceWithKnots.Instance.UKnots), Is.EqualTo(new[] { 0.0, 1.0 }));
        Assert.That(surface.Integers(BSplineSurfaceWithKnots.Instance.UMultiplicities), Is.EqualTo(new[] { 4L, 4L }));
        var controlPoints = surface.Refs(BSplineSurface.Instance.ControlPointsList);
        Assert.That(controlPoints?.Select(row => row.Count), Is.EqualTo(new[] { 4, 4, 4, 4 }));
        Assert.That(controlPoints![0][0].Id, Is.EqualTo(1404));
    }

    [Test]
    public static void EveryInstanceIsKnownToAp242()
    {
        var unknown = Enumerable.Range(0, _model.Count).Select(_model.EntityAt).Where(e => !e.IsKnown).Select(e => e.ToString()).ToList();
        Assert.That(unknown, Is.Empty);
        Assert.That(Enumerable.Range(0, _model.Count).Count(i => _model.EntityAt(i).Is<IAdvancedFace>()), Is.EqualTo(98));
    }
}
