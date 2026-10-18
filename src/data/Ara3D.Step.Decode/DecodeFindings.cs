using Ara3D.StepBrep;
using P21 = Ara3D.IO.StepParser;

namespace Ara3D.Step.Decode;

/// <summary>The decoder's diagnostics in the order found, each built by the Plato library's table
/// (StepDiagnostics.Diagnose), so codes, severities and messages have one source.</summary>
public sealed class DecodeFindings
{
    /// <summary>The code adapter/ADAPTER-SPEC.md assigns a syntax error outside any instance; layer 3's table does not
    /// list it yet, so its severity, code and message are written here.</summary>
    public const string SyntaxOutsideInstance = "P21-SYNTAX-OUTSIDE-INSTANCE";

    readonly List<Diagnostic> _diagnostics = [];
    readonly HashSet<(int Kind, int Id)> _reported = [];

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public void At(StepDiagnosticCode code, int instanceId, double measure = double.NaN)
        => _diagnostics.Add(code.Diagnose(Provenance.AtInstance(new StepInstanceId(instanceId)), measure));

    /// <summary>A finding about an instance, once however many occurrences read it.</summary>
    public void Once(StepDiagnosticCode code, int instanceId, double measure = double.NaN)
    {
        if (_reported.Add((code.Kind, instanceId)))
            At(code, instanceId, measure);
    }

    public void Whole(StepDiagnosticCode code, double measure = double.NaN)
        => _diagnostics.Add(code.Diagnose(Provenance.WholeInput(), measure));

    /// <summary>The parser's findings, mapped as adapter/ADAPTER-SPEC.md ("Mapping StepDocument.Diagnostics") lays
    /// out: the measure is the byte offset.</summary>
    public void AddParse(IReadOnlyList<P21.StepDiagnostic> parse)
    {
        foreach (var d in parse)
        {
            if (d.Code == P21.StepDiagnosticCode.NotStepFile)
                Whole(StepDiagnosticCode.NotPart21File(), d.Offset);
            else if (d.Code == P21.StepDiagnosticCode.HeaderIncomplete)
                Whole(StepDiagnosticCode.HeaderIncomplete(), d.Offset);
            else if (d.DefinitionId > 0)
                At(StepDiagnosticCode.SyntaxError(), d.DefinitionId, d.Offset);
            else
                _diagnostics.Add(new Diagnostic(Severity.Error(), SyntaxOutsideInstance,
                    "The text stops matching Part 21 outside any instance (a cut header, a missing DATA; or ENDSEC;, text between instances); what came before was kept.",
                    Provenance.WholeInput(), d.Offset));
        }
    }
}
