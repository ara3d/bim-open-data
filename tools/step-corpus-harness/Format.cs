using System.Globalization;

namespace Ara3D.Step.CorpusHarness;

public static class Format
{
    public static string Ms(double ms)
        => ms.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A measure that may be far below one, such as a deviation in millimetres, to six significant digits.</summary>
    public static string Number(double x)
        => x.ToString("G6", CultureInfo.InvariantCulture);

    /// <summary>A CSV field: quoted, with inner quotes doubled, when it holds a comma, quote or line break.</summary>
    public static string Field(string s)
        => s.IndexOfAny([',', '"', '\n', '\r']) < 0 ? s : $"\"{s.Replace("\"", "\"\"")}\"";
}
