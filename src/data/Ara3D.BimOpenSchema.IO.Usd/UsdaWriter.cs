using System.Globalization;

namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes USDA (USD text) tokens and literals straight to a <see cref="TextWriter"/>: no
/// per-value strings, and every number in the invariant culture whatever the writer's own
/// culture is. Tracks the indentation of nested prim blocks. Each method returns this writer
/// so a line reads left to right.
/// </summary>
internal sealed class UsdaWriter
{
    private const string IndentUnit = "    ";

    // Zero-padded formats for one to four fraction digits, indexed by digit count.
    private static readonly string[] FractionFormats = ["", "D1", "D2", "D3", "D4"];

    private readonly TextWriter _out;
    private readonly char[] _number = new char[64];
    private int _depth;

    public UsdaWriter(TextWriter output) => _out = output;

    /// <summary>Starts a line at the current depth.</summary>
    public UsdaWriter Line()
    {
        for (var i = 0; i < _depth; i++)
            _out.Write(IndentUnit);
        return this;
    }

    /// <summary>A whole line at the current depth.</summary>
    public UsdaWriter Line(string text) => Line().Text(text).End();

    public UsdaWriter End()
    {
        _out.Write('\n');
        return this;
    }

    /// <summary>Writes "{" on its own line and indents what follows.</summary>
    public UsdaWriter Open()
    {
        Line("{");
        _depth++;
        return this;
    }

    /// <summary>Indents what follows, after a line that ended with its own "{".</summary>
    public UsdaWriter Nest()
    {
        _depth++;
        return this;
    }

    /// <summary>Unindents and writes "}" on its own line.</summary>
    public UsdaWriter Close()
    {
        _depth--;
        return Line("}");
    }

    /// <summary>Writes "(" for a metadata block and indents what follows.</summary>
    public UsdaWriter OpenMetadata()
    {
        Text(" (").End();
        _depth++;
        return this;
    }

    /// <summary>Unindents and writes ")" on its own line.</summary>
    public UsdaWriter CloseMetadata()
    {
        _depth--;
        return Line(")");
    }

    public UsdaWriter Text(string text)
    {
        _out.Write(text);
        return this;
    }

    public UsdaWriter Text(char c)
    {
        _out.Write(c);
        return this;
    }

    public UsdaWriter Int(long value)
    {
        value.TryFormat(_number, out var n, default, CultureInfo.InvariantCulture);
        _out.Write(_number, 0, n);
        return this;
    }

    /// <summary>The shortest text that reads back as the same float; NaN and infinities in USD's spelling.</summary>
    public UsdaWriter Float(float value)
    {
        if (float.IsNaN(value))
            return Text("nan");
        if (float.IsInfinity(value))
            return Text(value > 0 ? "inf" : "-inf");
        value.TryFormat(_number, out var n, default, CultureInfo.InvariantCulture);
        _out.Write(_number, 0, n);
        return this;
    }

    /// <summary>A fixed-point integer with four implied decimals (a BOS vertex coordinate in units
    /// of 0.1 mm) as an exact decimal in metres: 12345 becomes "1.2345", 20000 becomes "2".</summary>
    public UsdaWriter Fixed4(int value)
    {
        long v = value;
        if (v < 0)
        {
            _out.Write('-');
            v = -v;
        }
        Int(v / 10_000);
        var fraction = (int)(v % 10_000);
        if (fraction == 0)
            return this;
        var digits = 4;
        while (fraction % 10 == 0)
        {
            fraction /= 10;
            digits--;
        }
        _out.Write('.');
        fraction.TryFormat(_number, out var n, FractionFormats[digits], CultureInfo.InvariantCulture);
        _out.Write(_number, 0, n);
        return this;
    }

    /// <summary>A double-quoted string with backslash escapes for quotes, backslashes, and control characters.</summary>
    public UsdaWriter Quoted(string text)
    {
        _out.Write('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': _out.Write("\\\""); break;
                case '\\': _out.Write("\\\\"); break;
                case '\n': _out.Write("\\n"); break;
                case '\r': _out.Write("\\r"); break;
                case '\t': _out.Write("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        _out.Write("\\x");
                        ((int)c).TryFormat(_number, out var n, "x2", CultureInfo.InvariantCulture);
                        _out.Write(_number, 0, n);
                    }
                    else
                        _out.Write(c);
                    break;
            }
        }
        _out.Write('"');
        return this;
    }

    /// <summary>A path literal: &lt;/Model/Materials/M3&gt;.</summary>
    public UsdaWriter PathRef(string prefix, long index)
        => Text('<').Text(prefix).Int(index).Text('>');

    /// <summary>A tuple of three floats: (x, y, z).</summary>
    public UsdaWriter Float3(float x, float y, float z)
        => Text('(').Float(x).Text(", ").Float(y).Text(", ").Float(z).Text(')');
}
