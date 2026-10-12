using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Ifc.Conventions;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Tests;

/// <summary>The converter records the project's AREAUNIT and VOLUMEUNIT scales apart from its
/// length unit, since IFC declares them apart: a metric Revit export has a millimetre length unit
/// and square-metre areas, so an area scaled by the length unit squared is off by a million.</summary>
[TestFixture]
public static class ConverterAreaVolumeUnitTests
{
    /// <summary>A metric Revit export's unit assignment: millimetres, square metres, cubic metres.</summary>
    private static readonly string MetricRevitIfc = MiniIfc.Document("""
        #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #2=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);
        #3=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);
        #4=IFCUNITASSIGNMENT((#1,#2,#3));
        #5=IFCPROJECT('project-gid',$,'Project',$,$,$,$,(),#4);
        """, MiniIfc.Ifc4, "metric-revit-units.ifc", "ViewDefinition");

    /// <summary>An imperial export: feet, square feet and cubic feet as conversion-based units.</summary>
    private static readonly string ImperialIfc = MiniIfc.Document("""
        #1=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
        #2=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
        #3=IFCMEASUREWITHUNIT(IFCRATIOMEASURE(0.3048),#2);
        #4=IFCCONVERSIONBASEDUNIT(#1,.LENGTHUNIT.,'FOOT',#3);
        #5=IFCDIMENSIONALEXPONENTS(2,0,0,0,0,0,0);
        #6=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);
        #7=IFCMEASUREWITHUNIT(IFCRATIOMEASURE(0.09290304),#6);
        #8=IFCCONVERSIONBASEDUNIT(#5,.AREAUNIT.,'SQUARE FOOT',#7);
        #9=IFCDIMENSIONALEXPONENTS(3,0,0,0,0,0,0);
        #10=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);
        #11=IFCMEASUREWITHUNIT(IFCRATIOMEASURE(0.028316846592),#10);
        #12=IFCCONVERSIONBASEDUNIT(#9,.VOLUMEUNIT.,'CUBIC FOOT',#11);
        #13=IFCUNITASSIGNMENT((#4,#8,#12));
        #14=IFCPROJECT('project-gid',$,'Project',$,$,$,$,(),#13);
        """, MiniIfc.Ifc2x3, "imperial-units.ifc", "ViewDefinition");

    /// <summary>A prefixed area unit and no volume unit.</summary>
    private static readonly string SquareMillimetreIfc = MiniIfc.Document("""
        #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #2=IFCSIUNIT(*,.AREAUNIT.,.MILLI.,.SQUARE_METRE.);
        #3=IFCUNITASSIGNMENT((#1,#2));
        #4=IFCPROJECT('project-gid',$,'Project',$,$,$,$,(),#3);
        """, MiniIfc.Ifc4, "square-millimetre.ifc", "ViewDefinition");

    private static EntityModel Project(string content)
    {
        var path = Path.Combine(TestData.OutputFolder, $"area-volume-units-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, content, System.Text.Encoding.ASCII);
        IfcToBosConverter? converter = null;
        try
        {
            converter = new IfcToBosConverter(path);
            var bom = new BimObjectModel(converter.BimDataBuilder.Build(), true);
            return bom.Entities.Single(e => e.Category == "IFCPROJECT");
        }
        finally
        {
            converter?.IfcFile.Dispose();
        }
    }

    [Test]
    public static void MetricRevitUnits_RecordAreaAndVolumeApartFromTheLength()
    {
        var project = Project(MetricRevitIfc);
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.LengthUnitToMetre), Is.EqualTo(0.001).Within(1e-9));
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.AreaUnitToSquareMetre), Is.EqualTo(1.0).Within(1e-9));
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.VolumeUnitToCubicMetre), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public static void ConversionBasedUnits_RecordTheirFactors()
    {
        var project = Project(ImperialIfc);
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.LengthUnitToMetre), Is.EqualTo(0.3048).Within(1e-6));
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.AreaUnitToSquareMetre), Is.EqualTo(0.09290304).Within(1e-8));
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.VolumeUnitToCubicMetre), Is.EqualTo(0.028316846592).Within(1e-8));
    }

    [Test]
    public static void PrefixedAreaUnit_ScalesByThePrefixSquared_AndAMissingVolumeUnitIsNotWritten()
    {
        var project = Project(SquareMillimetreIfc);
        Assert.That(project.GetParameterAsNumber(IfcParameterNames.AreaUnitToSquareMetre), Is.EqualTo(1e-6).Within(1e-12));
        Assert.That(project.ParameterValues, Does.Not.ContainKey(IfcParameterNames.VolumeUnitToCubicMetre));
    }
}
