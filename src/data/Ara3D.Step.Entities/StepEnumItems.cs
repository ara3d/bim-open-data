using Ara3D.IO.StepParser;

namespace Ara3D.Step.Entities;

/// <summary>
/// Maps a file's enumeration item (.B_SPLINE.) to a generated enum member (BSpline). The generator writes each item
/// in Pascal case and refuses an enumeration whose items differ only in underscores or case, so the item without
/// dots and underscores, upper-cased, names one member.
/// </summary>
internal static class StepEnumItems<T> where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByKey
        = System.Enum.GetValues<T>().ToDictionary(v => v.ToString().ToUpperInvariant());

    public static T? Find(StepToken symbol)
        => ByKey.TryGetValue(symbol.ToString().Trim('.').Replace("_", "").ToUpperInvariant(), out var value) ? value : null;
}
