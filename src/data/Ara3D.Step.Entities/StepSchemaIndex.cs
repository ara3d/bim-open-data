using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.StepTypes;
using Ara3D.Utils;

namespace Ara3D.Step.Entities;

/// <summary>
/// Finds the descriptor for an entity name as a file writes it, upper case, without allocating. A file may write
/// the long name (CARTESIAN_POINT) or the ISO 10303 short name (CRTPNT, StepShortNames.tsv); both give the same
/// descriptor, so every reader sees long names. A name that is both (LINE, PLANE) is the long name.
/// </summary>
public static class StepSchemaIndex
{
    private static readonly Dictionary<uint, StepEntityType> ByCode
        = StepSchema.Entities.ToDictionary(t => Encoding.ASCII.GetBytes(t.EntityName).Fnv1a32bit());

    private static readonly Dictionary<uint, (string Short, StepEntityType Type)> ByShortCode = ReadShortNames();

    public static StepEntityType? Find(StepToken name)
        => FindLong(name) ?? FindShort(name);

    /// <summary>True when the name is a short name that is not also a long name: the file used a short form.</summary>
    public static bool IsShortName(StepToken name)
        => FindLong(name) == null && FindShort(name) != null;

    private static StepEntityType? FindLong(StepToken name)
        => ByCode.TryGetValue(name.Span.Fnv1a32bit(), out var type) && name.Span.MatchesName(type.EntityName) ? type : null;

    private static StepEntityType? FindShort(StepToken name)
        => ByShortCode.TryGetValue(name.Span.Fnv1a32bit(), out var entry) && name.Span.MatchesName(entry.Short) ? entry.Type : null;

    /// <summary>The pairs whose long name AP242 declares; the rest (entities only AP203 or AP214 have) are skipped.</summary>
    private static Dictionary<uint, (string, StepEntityType)> ReadShortNames()
    {
        var byName = StepSchema.Entities.ToDictionary(t => t.EntityName);
        var result = new Dictionary<uint, (string, StepEntityType)>();
        using var stream = typeof(StepSchemaIndex).Assembly.GetManifestResourceStream("StepShortNames.tsv")!;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var cells = line.Split('\t');
            if (cells.Length == 2 && !line.StartsWith('#') && byName.TryGetValue(cells[1], out var type))
                result[Encoding.ASCII.GetBytes(cells[0]).Fnv1a32bit()] = (cells[0], type);
        }
        return result;
    }
}
