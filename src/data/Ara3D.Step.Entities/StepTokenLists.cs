using Ara3D.IO.StepParser;

namespace Ara3D.Step.Entities;

public static class StepTokenLists
{
    /// <summary>The index-th element of a list token without building the list: a nested list or a typed value
    /// such as LENGTH_MEASURE(1.) counts as one element.</summary>
    public static bool TryElementAt(this StepToken list, StepDocument document, int index, out StepToken element)
    {
        var tokens = list.AsSpan(document);
        var i = 0;
        for (var n = 0; i < tokens.Length; n++)
        {
            if (n == index)
            {
                element = tokens[i];
                return true;
            }
            i += tokens[i].IsList ? tokens[i].Length + 1 : tokens[i].IsEntity ? tokens[i + 1].Length + 2 : 1;
        }
        element = default;
        return false;
    }

    /// <summary>True when the bytes spell the ASCII name exactly; Part 21 writes entity names in upper case.</summary>
    public static bool MatchesName(this ReadOnlySpan<byte> bytes, string name)
    {
        if (bytes.Length != name.Length)
            return false;
        for (var i = 0; i < bytes.Length; i++)
            if (bytes[i] != name[i])
                return false;
        return true;
    }
}
