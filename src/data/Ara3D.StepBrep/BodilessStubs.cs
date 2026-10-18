namespace Ara3D.StepBrep;

/// <summary>
/// Plato's C# writer emits no stub for a function declared without a body whose receiver is an array (TKT-315 writer
/// fix 6, Plato tracker plato-459, paused on 2026-10-18), so Generated/ calls one that does not exist. The one such
/// function in layers 00 to 07, the deferred FlatRecord of 03-step/step-schema.library.plato, gets here the stub the
/// fixed writer will emit. Nothing in RawDecodedModel, PrepareModel or the witnesses calls it. When plato-459 lands
/// and Generated/ is regenerated, the two definitions collide (CS0121): delete this file then.
/// </summary>
public static class BodilessStubs
{
    public static IReadOnlyList<RecordSlot> FlatRecord(this IReadOnlyList<EntityDefinition> schema, String name)
        => throw new NotImplementedException("plato: FlatRecord is declared without a body");
}
