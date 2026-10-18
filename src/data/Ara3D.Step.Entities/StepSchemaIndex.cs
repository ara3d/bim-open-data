using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.StepTypes;
using Ara3D.Utils;

namespace Ara3D.Step.Entities;

/// <summary>Finds the descriptor for an entity name as a file writes it, upper case, without allocating.</summary>
public static class StepSchemaIndex
{
    private static readonly Dictionary<uint, StepEntityType> ByCode
        = StepSchema.Entities.ToDictionary(t => Encoding.ASCII.GetBytes(t.EntityName).Fnv1a32bit());

    public static StepEntityType? Find(StepToken name)
        => ByCode.TryGetValue(name.Span.Fnv1a32bit(), out var type) && name.Span.MatchesName(type.EntityName) ? type : null;
}
