using Ara3D.Geometry;
using Ara3D.IO.StepParser;
using Ara3D.Models;

namespace Ara3D.Step.Mesher;

/// <summary>Where parsing stopped in a file the parser could not finish: the mesher read the
/// <see cref="DefinitionCount"/> instances before byte <see cref="StopOffset"/>, the last being
/// #<see cref="LastDefinitionId"/>.</summary>
public readonly record struct StepParsedPrefix(int DefinitionCount, int LastDefinitionId, long StopOffset);

/// <summary>
/// A STEP file as a placed, coloured <see cref="Model3D"/>, with what the mesher found on the way. The counts and
/// bounds (metres) describe <see cref="Model"/>. <see cref="Placements"/> lists every shape item at every place,
/// meshed or not; <see cref="GeometryPending"/> counts the items whose shape mesher is not written yet.
/// <see cref="ContextUnits"/> holds every geometric context that assigns units, and every one a representation
/// used, by id. <see cref="StyledColours"/> counts styled instances with a surface colour.
/// <see cref="ParseDiagnostics"/> are the parser's; when one is an error, <see cref="Prefix"/> says what was read.
/// <see cref="Success"/> is false only when meshing itself failed, with the reason in <see cref="Errors"/>.
/// </summary>
public sealed record StepMeshingResult(
    string MesherName,
    bool Success,
    Model3D? Model,
    int MeshCount,
    int InstanceCount,
    int TriangleCount,
    Bounds3D Bounds,
    IReadOnlyList<StepPlacedItem> Placements,
    IReadOnlyDictionary<int, StepUnits> ContextUnits,
    int AssemblyDepth,
    int StyledColours,
    int GeometryPending,
    IReadOnlyList<StepCycle> Cycles,
    StepMeshingDiagnostics Diagnostics,
    IReadOnlyList<StepDiagnostic> ParseDiagnostics,
    StepParsedPrefix? Prefix,
    IReadOnlyList<string> Errors)
{
    public static StepMeshingResult Failed(string mesherName, string error, IReadOnlyList<StepDiagnostic>? parse = null)
        => new(mesherName, false, null, 0, 0, 0, Bounds3D.Empty, [], new Dictionary<int, StepUnits>(), 0, 0, 0, [],
            new StepMeshingDiagnostics(), parse ?? [], null, [error]);

    /// <summary>The prefix read when the parser stopped at an error; null when it read the whole file.</summary>
    public static StepParsedPrefix? PrefixOf(StepDocument document)
    {
        foreach (var d in document.Diagnostics)
            if (d.Severity == StepSeverity.Error)
                return new(document.Definitions.Count, document.Definitions.Count > 0 ? document.Definitions[^1].Id : 0, d.Offset);
        return null;
    }
}
