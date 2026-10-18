using Ara3D.IO.StepParser;
using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>Reads the attribute kinds <see cref="StepEntityReaders"/> leaves out: strings, typed measure values,
/// and the entity name used to key diagnostics.</summary>
public static class StepValues
{
    /// <summary>The string in a STEP string slot, as written (no \X2\ decoding), or null when absent.</summary>
    public static string? Text(this StepEntity e, StepAttribute<string> attribute)
        => e.Value(attribute) is { IsString: true } t ? t.AsString() : null;

    /// <summary>A measure_value slot: a typed value such as LENGTH_MEASURE(25.4), or a bare number.</summary>
    public static double? Measure(this StepEntity e, StepAttribute<IMeasureValue> attribute)
        => e.Value(attribute) is not { } t ? null
            : t.IsNumber ? t.AsNumber()
            : t.IsEntity && t.AsSimpleEntity(e.Model.Document).Item2 is { IsNumber: true } inner ? inner.AsNumber()
            : null;

    /// <summary>The declared name of a simple instance, or the part names of a complex one joined by '+'.</summary>
    public static string EntityName(this StepEntity e)
        => e.Type?.EntityName
            ?? (e.IsComplex ? string.Join("+", e.Parts.Select(p => p.Partial.NameToken.ToString())) : e.Definition.NameToken.ToString());
}
