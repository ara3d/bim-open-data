using System.Globalization;
using System.Text;

namespace Ara3D.Step.Entities;

/// <summary>
/// Decodes the text between a string's quotes by ISO 10303-21 6.4.3: '' is a quote, \\ a backslash, \S\c the
/// ISO 8859-1 high-half character c, \X\hh one 8-bit character, \X2\hhhh..\X0\ UTF-16 and \X4\hhhhhhhh..\X0\
/// UTF-32. \PA\ selects the ISO 8859-1 default code page; any other \P?\ page is not supported, so it and the \S\
/// directives under it stay as written. A malformed directive stays as written. Line breaks are layout (5.6) and
/// are dropped.
/// </summary>
public static class StepStrings
{
    private static readonly char[] Special = ['\'', '\\', '\r', '\n'];

    public static string Decode(string raw)
        => raw.IndexOfAny(Special) < 0 ? raw : new Decoder(raw).Run();

    private sealed class Decoder(string raw)
    {
        private const char Slash = '\\';
        private readonly StringBuilder _text = new(raw.Length);
        private bool _latin1 = true;
        private int _at;

        public string Run()
        {
            while (_at < raw.Length)
                Step();
            return _text.ToString();
        }

        private void Step()
        {
            var c = raw[_at];
            if (c is '\r' or '\n') _at++;
            else if (c == '\'' && Is(1, '\'')) Emit("'", 2);
            else if (c == Slash) Directive();
            else Emit(c.ToString(), 1);
        }

        private void Directive()
        {
            if (Is(1, Slash)) Emit("\\", 2);
            else if (Is(1, 'S') && Is(2, Slash) && _at + 3 < raw.Length && _latin1) Emit(((char)(raw[_at + 3] + 128)).ToString(), 4);
            else if (Is(1, 'P') && Is(3, Slash) && raw[_at + 2] is >= 'A' and <= 'I') CodePage(raw[_at + 2]);
            else if (Is(1, 'X') && Is(2, Slash) && Hex(_at + 3, 2) is { } b) Emit(((char)b).ToString(), 5);
            else if (Is(1, 'X') && Is(3, Slash) && raw[_at + 2] is '2' or '4') Units(raw[_at + 2] == '2' ? 4 : 8);
            else Emit("\\", 1);
        }

        private void CodePage(char page)
        {
            _latin1 = page == 'A';
            if (_latin1) _at += 4;
            else Emit(raw.Substring(_at, 4), 4);
        }

        private void Units(int width)
        {
            var start = _at + 4;
            var end = raw.IndexOf("\\X0\\", start, StringComparison.Ordinal);
            if (end < 0 || (end - start) % width != 0 || Units(start, end, width) is not { } text) Emit("\\", 1);
            else Emit(text, end + 4 - _at);
        }

        private string? Units(int start, int end, int width)
        {
            var text = new StringBuilder();
            for (var i = start; i < end; i += width)
            {
                if (Hex(i, width) is not { } code) return null;
                if (width == 4) text.Append((char)code);
                else if (code is <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)) text.Append(char.ConvertFromUtf32((int)code));
                else return null;
            }
            return text.ToString();
        }

        private long? Hex(int start, int width)
            => start + width <= raw.Length && long.TryParse(raw.AsSpan(start, width), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v) ? v : null;

        private bool Is(int offset, char c)
            => _at + offset < raw.Length && raw[_at + offset] == c;

        private void Emit(string text, int consumed)
        {
            _text.Append(text);
            _at += consumed;
        }
    }
}
