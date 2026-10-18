using Ara3D.IO.StepParser;
using Ara3D.Step.Entities;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>Reads a SET OF trimming_select, whose items are references (cartesian_point) or typed values
/// (PARAMETER_VALUE(0.5)): the typed layer's readers take one or the other, not both. It reads through the
/// attribute's descriptor as they do; it belongs with them in Ara3D.Step.Entities (TKT-312 build log).</summary>
public static class StepSelectReaders
{
    /// <summary>The parameter_value of a trim, or null when the trim lists only points.</summary>
    public static double? TrimParameter(this StepEntity e, ST.StepAttribute<IReadOnlyList<ST.ITrimmingSelect>> attribute)
    {
        if (e.Value(attribute) is not { IsList: true } list)
            return null;
        foreach (var item in list.AsList(e.Model.Document))
        {
            if (item.IsNumber)
                return item.AsNumber();
            if (item.IsEntity && item.AsSimpleEntity(e.Model.Document).Item2 is { IsNumber: true } inner)
                return inner.AsNumber();
        }
        return null;
    }
}
