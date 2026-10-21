using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>How each edge and face reaches the library. <see cref="Host"/> is StepPlatoMesher.MeshFile as committed:
/// CanDiscretize then Discretize per edge, IsDiscretizedFace per face, each of which rescans the whole model
/// (Law_IndicesInRange, FacesOfEdge, IsEveryIndexInRange). <see cref="Hoisted"/> is layer 4's DiscretizeAllRanged and
/// layer 8's FaceOutcomes after the complexity sweep: the in-range flag, the edge-face table, the two-edge-loop flags
/// and the arena flag built once per model (<see cref="TablesMs"/>), then DiscretizeEdgeRangedCounted per edge and
/// IsDiscretizedFaceIn per face.</summary>
public sealed record MeshPath(
    string Name,
    double TablesMs,
    Func<BrepEdgeIndex, bool> CanDiscretize,
    Func<BrepEdgeIndex, EdgeDiscretization> Discretize,
    Func<BrepFaceIndex, EdgeDiscretizations, bool> IsDiscretized)
{
    public const string HostName = "host";
    public const string HoistedName = "hoisted";

    public static MeshPath Of(string name, BrepModel m, DiscretizationCriteria c)
        => name switch
        {
            HostName => Host(m, c),
            HoistedName => Hoisted(m, c),
            _ => throw new ArgumentException($"--path is {HostName} or {HoistedName}, not {name}", nameof(name)),
        };

    public static MeshPath Host(BrepModel m, DiscretizationCriteria c)
        => new(HostName, 0,
            e => m.CanDiscretize(e, c),
            e => m.Discretize(e, c, SegmentFloors.NoFloors()),
            (f, d) => m.IsDiscretizedFace(f, d));

    public static MeshPath Hoisted(BrepModel m, DiscretizationCriteria c)
    {
        var ((inRange, faces, twoEdgeLoop, arenaOk), ms) = Clock.Time(() =>
            (m.Law_IndicesInRange(), m.EdgeFaceTableCounted().Value, m.TwoEdgeLoopEdges(), m.Geometry.IsEveryIndexInRange()));
        var floors = SegmentFloors.NoFloors();
        return new(HoistedName, ms,
            _ => true,
            e => m.DiscretizeEdgeRangedCounted(faces, twoEdgeLoop, inRange, e, c, floors).Value,
            (f, d) => m.IsDiscretizedFaceIn(f, d, arenaOk));
    }
}
