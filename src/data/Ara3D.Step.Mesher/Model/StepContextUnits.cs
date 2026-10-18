using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>The units of each representation's context, read once per context. A context without a length unit,
/// or a representation without a context, gets <see cref="StepUnitsReader.DefaultMetresPerUnit"/>, recorded as
/// approximate.</summary>
public sealed class StepContextUnits
{
    readonly Dictionary<int, StepUnits> _byContext = new();
    readonly StepMeshingDiagnostics _diagnostics;

    public StepContextUnits(StepMeshingDiagnostics diagnostics)
        => _diagnostics = diagnostics;

    public static StepUnits Default
        => new(StepUnitsReader.DefaultMetresPerUnit, null);

    /// <summary>Every context read so far, by id.</summary>
    public IReadOnlyDictionary<int, StepUnits> ByContext => _byContext;

    public StepUnits Of(StepEntity representation)
        => representation.Ref(Representation.Instance.ContextOfItems) is { } context ? OfContext(context) : Assumed(representation);

    public StepUnits OfContext(StepEntity context)
    {
        if (_byContext.TryGetValue(context.Id, out var units))
            return units;
        return _byContext[context.Id] = context.ReadUnits() ?? Assumed(context);
    }

    StepUnits Assumed(StepEntity e)
    {
        _diagnostics.Record(e.EntityName(), StepSupport.Approximate, $"#{e.Id} has no length unit; millimetres assumed");
        return Default;
    }
}
