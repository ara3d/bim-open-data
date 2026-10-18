using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>One placed copy of a representation that holds B-rep items, as the Plato library's StepOccurrence
/// describes it: <see cref="Placement"/> carries the representation's own coordinates, in its own length unit, to
/// the model's millimetres; <see cref="Parent"/> is an index into the occurrence list or -1 for a root;
/// <see cref="Via"/> is the context_dependent_shape_representation or mapped_item that placed it, or -1.</summary>
public readonly record struct FoundOccurrence(int Representation, int Context, int Parent, int Via, Affine Placement)
{
    public StepOccurrence ToPlato()
        => new(new StepInstanceId(Representation), new StepInstanceId(Context), new StepOccurrenceIndex(Parent),
            new StepInstanceId(Via), Placement.ToPlato());
}
