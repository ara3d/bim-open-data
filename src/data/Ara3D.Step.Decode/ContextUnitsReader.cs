using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>What the decoder applies for one context: millimetres per length unit and radians per plane-angle
/// unit.</summary>
public readonly record struct ContextFactors(double MillimetresPerUnit, double RadiansPerUnit);

/// <summary>Reads one 3D geometric_representation_context as the Plato library's ResolveContexts specifies: the
/// length and plane-angle units it assigns (the policy's where it is silent) and its length uncertainty, the
/// largest of those it lists, in the context's own length unit. Each finding goes to <see cref="DecodeFindings"/>
/// at the context.</summary>
public static class ContextUnitsReader
{
    public static ContextUnits ReadContextUnits(this StepEntity context, DecodePolicy policy, DecodeFindings findings)
    {
        var units = context.Is<ST.IGlobalUnitAssignedContext>() ? context.Refs(ST.GlobalUnitAssignedContext.Instance.Units) ?? [] : [];
        var lengths = new List<double>();
        var angles = new List<double>();
        foreach (var u in units)
        {
            if (u.Is<ST.ILengthUnit>() && u.MetresPerUnit() is { } metres)
                lengths.Add(metres);
            else if (u.Is<ST.IPlaneAngleUnit>() && u.RadiansPerUnit() is { } radians)
                angles.Add(radians);
        }
        var metresPerUnit = lengths.Count > 0 ? lengths[0] : (double)policy.DefaultLength.ScaleToMetres();
        var length = lengths.Count > 0 ? metresPerUnit.ClassifyLengthUnit(policy.UnitMatch) : policy.DefaultLength;
        if (lengths.Count == 0)
            findings.At(StepDiagnosticCode.LengthUnitDefaulted(), context.Id, metresPerUnit);
        else
            findings.At(StepDiagnosticCode.LengthUnitFound(), context.Id, metresPerUnit);
        if (lengths.Count > 1)
            findings.At(StepDiagnosticCode.LengthUnitsAmbiguous(), context.Id, lengths.Count);
        return new ContextUnits(new StepInstanceId(context.Id), length, lengths.Count > 0,
            context.AngleFactor(angles, policy, findings), angles.Count > 0, context.Uncertainty(metresPerUnit, length, findings));
    }

    public static ContextFactors Factors(this ContextUnits units)
        => new(units.Length.MillimetresPerUnit(), units.RadiansPerAngleUnit);

    static double AngleFactor(this StepEntity context, List<double> angles, DecodePolicy policy, DecodeFindings findings)
    {
        if (angles.Count == 0)
        {
            findings.At(StepDiagnosticCode.AngleUnitDefaulted(), context.Id, policy.DefaultRadiansPerAngleUnit);
            return policy.DefaultRadiansPerAngleUnit;
        }
        double snapped = angles[0].SnapAngleFactor(policy.DegreeSnap);
        if (snapped != angles[0])
            findings.At(StepDiagnosticCode.DegreeFactorSnapped(), context.Id, angles[0]);
        findings.At(StepDiagnosticCode.AngleUnitFound(), context.Id, snapped);
        return snapped;
    }

    /// <summary>Each length uncertainty converted through its own unit into the context's by the ratio of the two
    /// unit sizes, which is exactly 1 when they are the same unit.</summary>
    static ContextUncertainty Uncertainty(this StepEntity context, double metresPerUnit, LengthUnit length, DecodeFindings findings)
    {
        var listed = context.Is<ST.IGlobalUncertaintyAssignedContext>() ? context.Refs(ST.GlobalUncertaintyAssignedContext.Instance.Uncertainty) ?? [] : [];
        var count = 0;
        var largest = 0.0;
        foreach (var u in listed)
        {
            if (u.Measure(ST.MeasureWithUnit.Instance.ValueComponent) is not { } value
                || u.Ref(ST.MeasureWithUnit.Instance.UnitComponent)?.MetresPerUnit() is not { } unitMetres)
                continue;
            var inContext = value * (unitMetres / metresPerUnit);
            largest = count == 0 ? inContext : Math.Max(largest, inContext);
            count++;
        }
        if (count == 0)
            return ContextUncertainty.NoUncertainty();
        findings.At(StepDiagnosticCode.UncertaintyFound(), context.Id, largest * length.MillimetresPerUnit());
        return ContextUncertainty.StatedUncertainty(largest, count);
    }
}
