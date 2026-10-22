using System.Globalization;
using System.Text;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>A generated C# value written as the Plato expression that builds it: a record as <c>Type(a, b)</c>, a sum
/// as <c>Type.Case(a, b)</c> (or <c>Type.Case</c> with no payload), an array as <c>[a, b]</c>, numbers in round-trip
/// form, so the law runner rebuilds the value bit for bit. The types and field order come from the generated code
/// (<see cref="SubModel.DataFields"/>), so the output follows the library's declarations.</summary>
public static class PlatoLiteral
{
    public static string Write(object value)
    {
        var text = new StringBuilder();
        Append(text, value, 0);
        return text.ToString();
    }

    /// <summary>Arrays longer than this are written one element per line.</summary>
    const int InlineArray = 4;

    static void Append(StringBuilder text, object value, int depth)
    {
        switch (value)
        {
            case Number n:
                text.Append(NumberLiteral(n));
                return;
            case Integer i:
                text.Append(((long)i).ToString(CultureInfo.InvariantCulture));
                return;
            case Ara3D.StepBrep.Boolean b:
                text.Append((bool)b ? "true" : "false");
                return;
            case Ara3D.StepBrep.String s:
                text.Append('"').Append(s.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                return;
        }
        var t = value.GetType();
        if (SubModel.IsList(t))
        {
            var items = ((System.Collections.IEnumerable)value).Cast<object>().ToArray();
            var multiline = items.Length > InlineArray;
            text.Append('[');
            for (var k = 0; k < items.Length; k++)
            {
                if (k > 0)
                    text.Append(',').Append(multiline ? "" : " ");
                if (multiline)
                    text.Append('\n').Append(' ', 4 * (depth + 1));
                Append(text, items[k], depth + 1);
            }
            text.Append(']');
            return;
        }
        if (SubModel.JsonConstructor(t) == null)
            throw new NotSupportedException($"no Plato literal for a {t.FullName}");
        var kind = SubModel.CaseName(value);
        var fields = SubModel.ActiveFields(value).Where(f => f.Name != "Kind").ToArray();
        text.Append(t.IsGenericType ? t.Name[..t.Name.IndexOf('`')] : t.Name); // Array2D`1 is Plato's Array2D
        if (kind != null)
            text.Append('.').Append(kind);
        if (kind != null && fields.Length == 0)
            return;
        text.Append('(');
        for (var k = 0; k < fields.Length; k++)
        {
            if (k > 0)
                text.Append(", ");
            Append(text, fields[k].GetValue(value)!, depth + 1);
        }
        text.Append(')');
    }

    static string NumberLiteral(Number n)
    {
        var d = (double)n;
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new NotSupportedException($"no Plato literal for the Number {d}");
        var r = d.ToString("R", CultureInfo.InvariantCulture);
        return r.Contains('.') || r.Contains('E') ? r.Replace("E+", "e").Replace("E", "e") : r + ".0";
    }
}
