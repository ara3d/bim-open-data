using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>What layer 4's DiscretizeAllRanged and layer 8's FaceOutcomes build once per model before their rows:
/// the model's index-range law, the edge-face table, the flags marking edges of two-edge loops, and the geometry
/// arena's index-range flag. Built once per file and read by every edge and face row, so no row rescans the model
/// (TKT-402: CanDiscretize, FacesOfEdge and IsEveryIndexInRange per row were 69 to 73% of the host's edge time).
/// </summary>
public sealed record StepPlatoTables(bool InRange, EdgeFaceTable Faces, IReadOnlyList<StepBrep.Boolean> TwoEdgeLoop, bool ArenaOk)
{
    static readonly SegmentFloors NoFloors = SegmentFloors.NoFloors();

    public static StepPlatoTables Of(BrepModel m)
        => new(m.Law_IndicesInRange(), m.EdgeFaceTableCounted().Value, m.TwoEdgeLoopEdges(), m.Geometry.IsEveryIndexInRange());

    /// <summary>Layer 4's DiscretizeAllRanged entry for one edge.</summary>
    public EdgeDiscretization Discretized(BrepModel m, BrepEdgeIndex e, DiscretizationCriteria c)
        => m.DiscretizeEdgeRangedCounted(Faces, TwoEdgeLoop, InRange, e, c, NoFloors).Value;

    /// <summary>Layer 8's FaceOutcomeInRange test that the face is intact and every edge of it discretized.</summary>
    public bool IsDiscretized(BrepModel m, BrepFaceIndex f, EdgeDiscretizations d)
        => m.IsDiscretizedFaceIn(f, d, ArenaOk);

    /// <summary>Layer 8's FaceOutcomeInRange with every face chosen.</summary>
    public FaceMeshOutcome FaceOutcome(BrepModel m, BrepFaceIndex f, EdgeDiscretizations d, FaceMeshingCriteria c)
        => IsDiscretized(m, f, d) ? m.MeshFace(f, d, c) : UndiscretizedOutcome(f);

    /// <summary>Layer 8's outcome for a face whose edges are not all discretized.</summary>
    public static FaceMeshOutcome UndiscretizedOutcome(BrepFaceIndex f)
        => f.UnmeshedOutcome("PIPELINE-FACE-UNDISCRETIZED",
            "This face is not intact or cites an edge that has no discretization; it is not meshed.");
}
