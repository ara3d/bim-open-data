using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>Stops the decoding of one kernel row: <see cref="Code"/> is the finding about <see cref="InstanceId"/>
/// (a direction of zero length, an entity the kernel has no case for). The row's reader catches it, records the
/// finding and skips the row with its dependants; it never leaves the decoder.</summary>
public sealed class DecodeException(StepDiagnosticCode code, int instanceId)
    : Exception($"#{instanceId}: {code.Diagnose(Provenance.Nowhere(), double.NaN).Code}")
{
    public StepDiagnosticCode Code { get; } = code;
    public int InstanceId { get; } = instanceId;
}
