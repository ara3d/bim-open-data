using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>The model's ten kernel tables as the occurrences fill them, one after another, with each row's source,
/// the folds and repairs, and every instance a row was read for.</summary>
public sealed class KernelTables
{
    public KernelTable<BrepVertex> Vertices { get; } = new();
    public KernelTable<BrepEdge> Edges { get; } = new();
    public KernelTable<BrepLoop> Loops { get; } = new();
    public KernelTable<BrepFace> Faces { get; } = new();
    public KernelTable<BrepShell> Shells { get; } = new();
    public KernelTable<BrepBody> Bodies { get; } = new();
    public KernelTable<BrepCurve> Curves { get; } = new();
    public KernelTable<BrepCurve2D> Curves2D { get; } = new();
    public KernelTable<Pcurve> Pcurves { get; } = new();
    public KernelTable<BrepSurface> Surfaces { get; } = new();
    public List<FoldRecord> Folds { get; } = [];
    public List<RepairRecord> Repairs { get; } = [];
    public HashSet<int> Reached { get; } = [];

    /// <summary>The B-rep as written: no closure mark, the tolerances unset, millimetres.</summary>
    public BrepModel ToModel()
        => new(new GeometrySet(Curves.Rows.ToArray(), Curves2D.Rows.ToArray(), Pcurves.Rows.ToArray(), Surfaces.Rows.ToArray(), Array.Empty<ClosureMark>()),
            Vertices.Rows.ToArray(), Edges.Rows.ToArray(), Loops.Rows.ToArray(), Faces.Rows.ToArray(), Shells.Rows.ToArray(), Bodies.Rows.ToArray(),
            Tolerances.UnsetTolerances(), LengthUnit.Millimetre());

    public KernelSources ToSources()
        => new(Vertices.Sources.ToArray(), Edges.Sources.ToArray(), Loops.Sources.ToArray(), Faces.Sources.ToArray(), Shells.Sources.ToArray(),
            Bodies.Sources.ToArray(), Curves.Sources.ToArray(), Curves2D.Sources.ToArray(), Pcurves.Sources.ToArray(), Surfaces.Sources.ToArray());
}
