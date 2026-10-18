using Ara3D.Step.Entities;
using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>
/// Decodes the B-rep items of one occurrence into kernel rows: first every reachable instance is read, in its
/// occurrence's placement (millimetres, radians), into a row builder or a failure; then each table's decoded rows
/// get their indices, ascending by instance id after the rows of earlier occurrences; then the rows are built and
/// appended (<see cref="Commit"/>). An instance that cannot be decoded is skipped with every row that needs it, and
/// becomes a Skipped repair with a finding. Partial files hold the topology, the curves, the surfaces and the
/// parameter-plane curves.
/// </summary>
public sealed partial class OccurrenceReader
{
    readonly StepModel _model;
    readonly int _occurrence;
    readonly Affine _placement;
    readonly double _lengthScale;
    readonly double _radiansPerUnit;
    readonly DecodeFindings _findings;
    readonly PendingRows<BrepVertex> _vertices = new();
    readonly PendingRows<BrepEdge> _edges = new();
    readonly PendingRows<BrepLoop> _loops = new();
    readonly PendingRows<BrepFace> _faces = new();
    readonly PendingRows<BrepShell> _shells = new();
    readonly PendingRows<BrepBody> _bodies = new();
    readonly PendingRows<BrepCurve> _curves = new();
    readonly PendingRows<BrepCurve2D> _curves2D = new();
    readonly PendingRows<Pcurve> _pcurves = new();
    readonly PendingRows<BrepSurface> _surfaces = new();
    readonly List<(int Face, FoldCause Cause, int Via)> _folds = [];
    readonly HashSet<int> _citedFaces = [];

    public OccurrenceReader(StepModel model, int occurrence, FoundOccurrence found, ContextFactors factors, DecodeFindings findings)
    {
        _model = model;
        _occurrence = occurrence;
        _placement = found.Placement;
        _lengthScale = found.Placement.LengthScale;
        _radiansPerUnit = factors.RadiansPerUnit;
        _findings = findings;
    }

    /// <summary>Reads every B-rep item of the occurrence's representation.</summary>
    public OccurrenceReader Read(IReadOnlyList<StepEntity> brepItems)
    {
        foreach (var item in brepItems)
            Body(item);
        return this;
    }

    /// <summary>Appends this occurrence's rows, sources, folds and repairs to the model's tables.</summary>
    public void Commit(KernelTables tables)
    {
        _vertices.Assign(tables.Vertices.Rows.Count);
        _edges.Assign(tables.Edges.Rows.Count);
        _loops.Assign(tables.Loops.Rows.Count);
        _faces.Assign(tables.Faces.Rows.Count);
        _shells.Assign(tables.Shells.Rows.Count);
        _bodies.Assign(tables.Bodies.Rows.Count);
        _curves.Assign(tables.Curves.Rows.Count);
        _curves2D.Assign(tables.Curves2D.Rows.Count);
        _pcurves.Assign(tables.Pcurves.Rows.Count);
        _surfaces.Assign(tables.Surfaces.Rows.Count);
        _vertices.Commit(tables.Vertices, _occurrence);
        _edges.Commit(tables.Edges, _occurrence);
        _loops.Commit(tables.Loops, _occurrence);
        _faces.Commit(tables.Faces, _occurrence);
        _shells.Commit(tables.Shells, _occurrence);
        _bodies.Commit(tables.Bodies, _occurrence);
        _curves.Commit(tables.Curves, _occurrence);
        _curves2D.Commit(tables.Curves2D, _occurrence);
        _pcurves.Commit(tables.Pcurves, _occurrence);
        _surfaces.Commit(tables.Surfaces, _occurrence);
        foreach (var (face, cause, via) in _folds.OrderBy(f => _faces.IndexOf(f.Face)).ThenBy(f => f.Via))
            tables.Folds.Add(new FoldRecord(new BrepFaceIndex(_faces.IndexOf(face)), cause, Source(via)));
        foreach (var id in Failed().Order())
            tables.Repairs.Add(new RepairRecord(Source(id), StepRepair.Skipped()));
        tables.Reached.UnionWith(Reached());
    }

    IEnumerable<int> Failed()
        => _vertices.Failed.Concat(_edges.Failed).Concat(_loops.Failed).Concat(_faces.Failed).Concat(_shells.Failed)
            .Concat(_bodies.Failed).Concat(_curves.Failed).Concat(_curves2D.Failed).Concat(_pcurves.Failed).Concat(_surfaces.Failed);

    IEnumerable<int> Reached()
        => _vertices.Reached.Concat(_edges.Reached).Concat(_loops.Reached).Concat(_faces.Reached).Concat(_shells.Reached)
            .Concat(_bodies.Reached).Concat(_curves.Reached).Concat(_curves2D.Reached).Concat(_pcurves.Reached).Concat(_surfaces.Reached);

    StepSource Source(int id)
        => new(new StepOccurrenceIndex(_occurrence), new StepInstanceId(id));

    void Failure(int id, Exception ex)
    {
        var (code, at) = DecodeErrors.FindingOf(ex, id);
        _findings.Once(code, at);
        if (at != id || code.Kind != StepDiagnosticCode.Kind_InstanceSkipped)
            _findings.Once(StepDiagnosticCode.InstanceSkipped(), id);
    }

    /// <summary>Stops the row being read when a row it needs could not be decoded.</summary>
    static void Need(bool decoded, int id)
    {
        if (!decoded)
            throw new DecodeException(StepDiagnosticCode.InstanceSkipped(), id);
    }

    static DecodeException Unsupported(StepEntity e)
        => new(StepDiagnosticCode.InstanceSkipped(), e.Id);

    Point3D P(Vec3 local) => _placement.Point(local).ToPoint();
    Vector3D V(Vec3 local) => _placement.Vector(local).ToVector();
    Direction3D D(Vec3 local) => (_placement.Vector(local).Normalized() ?? local).ToDirection();
    double Length(double value) => value * _lengthScale;
    double Radians(double value) => value * _radiansPerUnit;
    Frame3D Frame(LocalFrame f) => new(P(f.Origin), D(f.X), D(f.Y), D(f.Z));
}
