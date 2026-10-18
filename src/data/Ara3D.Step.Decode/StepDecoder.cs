using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using P21 = Ara3D.IO.StepParser;

namespace Ara3D.Step.Decode;

/// <summary>
/// The Plato library's host obligation DecodeRaw (sandbox/step-brep-mesh/03-step, step.library.plato): the B-rep as
/// the file writes it, as a RawDecodedModel. In order: the parser's findings and the census; every 3D context's
/// units; the occurrences; each occurrence's B-rep items decoded into the kernel tables, placed in millimetres and
/// radians, with oriented-face and oriented-shell flips recorded as folds (not applied) and every instance that
/// cannot be decoded skipped with a finding. No tolerance is read or set, no closure decided, no vertex merged:
/// that is Plato's PrepareModel. Total: every document gives a model, possibly empty.
/// </summary>
public static class StepDecoder
{
    public static RawDecodedModel DecodeRaw(this P21.StepDocument document)
        => new StepModel(document).DecodeRaw();

    public static RawDecodedModel DecodeRaw(this StepModel model)
    {
        var policy = DecodePolicy.StandardPolicy();
        var findings = new DecodeFindings();
        findings.AddParse(model.Document.Diagnostics);
        StepCensus.Classify(model, findings);
        var links = new StepLinks(model, (e, ex) => findings.Once(DecodeErrors.FindingOf(ex, e.Id).Code, e.Id));
        var units = links.Contexts3D.Select(id => model.Get(id).ReadContextUnits(policy, findings)).ToArray();
        var byContext = units.ToDictionary(u => (int)u.Context.Value, u => u.Factors());
        var fallback = StepReading.DefaultContextUnits(policy).Factors();
        ContextFactors FactorsOf(int context) => byContext.TryGetValue(context, out var f) ? f : fallback;

        var occurrences = new OccurrenceFinder(model, links, FactorsOf, findings).Find();
        if (occurrences.Count == 0)
            findings.Whole(StepDiagnosticCode.NoShapeFound());
        var tables = new KernelTables();
        for (var i = 0; i < occurrences.Count; i++)
        {
            var found = occurrences[i];
            new OccurrenceReader(model, i, found, FactorsOf(found.Context), findings)
                .Read(OccurrenceFinder.BrepItems(model.Get(found.Representation)))
                .Commit(tables);
        }
        Unreferenced(model, tables.Reached, findings);
        var root = occurrences.Count > 0 ? FactorsOf(occurrences[0].Context) : new ContextFactors(1, 1);
        return new RawDecodedModel(tables.ToModel(), occurrences.Select(o => o.ToPlato()).ToArray(), tables.ToSources(),
            tables.Folds.ToArray(), tables.Repairs.ToArray(), units, root.MillimetresPerUnit, root.RadiansPerUnit, findings.Diagnostics.ToArray());
    }

    /// <summary>One InstancesUnreferenced Info per type of kernel element (vertex, edge, loop, face, shell, body,
    /// curve, surface, pcurve) that no occurrence reached, at its first instance, with the count as measure.</summary>
    static void Unreferenced(StepModel model, IReadOnlySet<int> reached, DecodeFindings findings)
    {
        var census = new Dictionary<string, (int First, int Count)>();
        for (var i = 0; i < model.Count; i++)
        {
            var e = model.EntityAt(i);
            if (reached.Contains(e.Id) || !KernelKinds.IsKernelRow(e))
                continue;
            var name = e.EntityName();
            census[name] = census.TryGetValue(name, out var seen) ? (Math.Min(seen.First, e.Id), seen.Count + 1) : (e.Id, 1);
        }
        foreach (var (_, (first, count)) in census.OrderBy(c => c.Value.First))
            findings.At(StepDiagnosticCode.InstancesUnreferenced(), first, count);
    }
}
