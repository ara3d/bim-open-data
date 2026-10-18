using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>
/// The census the Plato library's ClassificationDiagnostics specifies: each instance is Unsupported when any of its
/// records is unsupported or unknown, otherwise Ignored when any is ignored, otherwise Decoded, by the treatment
/// column of the library's own schema table (Part21File.SupportedSchema). One EntityTypeIgnored Info and one
/// EntityTypeUnsupported Warning per type, at its first instance, with the instance count as the measure.
/// </summary>
public static class StepCensus
{
    enum Treatment { Decoded, Ignored, Unsupported }

    static readonly Lazy<Dictionary<string, Treatment>> Table = new(() =>
        Part21File.SupportedSchema().ToDictionary(d => (string)d.Name,
            d => d.Treatment.IsDecoded() ? Treatment.Decoded : d.Treatment.IsIgnored() ? Treatment.Ignored : Treatment.Unsupported,
            StringComparer.OrdinalIgnoreCase));

    /// <summary>True when the schema table decodes every record of the instance.</summary>
    public static bool IsDecoded(this StepEntity e)
        => TreatmentOf(e) == Treatment.Decoded;

    public static void Classify(StepModel model, DecodeFindings findings)
    {
        var census = new Dictionary<string, (Treatment Treatment, int First, int Count)>();
        var order = new List<string>();
        for (var i = 0; i < model.Count; i++)
        {
            var e = model.EntityAt(i);
            var treatment = TreatmentOf(e);
            if (treatment == Treatment.Decoded)
                continue;
            var name = e.EntityName();
            if (census.TryGetValue(name, out var seen))
                census[name] = (treatment, Math.Min(seen.First, e.Id), seen.Count + 1);
            else
            {
                census[name] = (treatment, e.Id, 1);
                order.Add(name);
            }
        }
        foreach (var name in order)
        {
            var (treatment, first, count) = census[name];
            findings.At(treatment == Treatment.Ignored ? StepDiagnosticCode.EntityTypeIgnored() : StepDiagnosticCode.EntityTypeUnsupported(), first, count);
        }
    }

    static Treatment TreatmentOf(StepEntity e)
    {
        if (!e.IsComplex)
            return Of(e.Type?.EntityName ?? e.EntityName());
        var worst = Treatment.Decoded;
        foreach (var part in e.Parts)
        {
            var t = Of(part.Type?.EntityName ?? part.Partial.NameToken.ToString());
            if (t > worst)
                worst = t;
        }
        return worst;
    }

    static Treatment Of(string name)
        => Table.Value.TryGetValue(name, out var t) ? t : Treatment.Unsupported;
}
