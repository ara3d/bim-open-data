using System.Globalization;

namespace Ara3D.Step.CorpusHarness;

public static class Format
{
    public static string Ms(double ms)
        => ms.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A CSV field: quoted, with inner quotes doubled, when it holds a comma, quote or line break.</summary>
    public static string Field(string s)
        => s.IndexOfAny([',', '"', '\n', '\r']) < 0 ? s : $"\"{s.Replace("\"", "\"\"")}\"";
}
