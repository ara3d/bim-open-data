namespace Ara3D.MeshKernel;

public enum FaceDropKind
{
    /// <summary>The bounds read gave fewer than three points; a reader reports this before it builds the face's map.</summary>
    TooFewBoundaryPoints,
    TooFewDistinctPoints,
    TriangulationFailed,
    AreaOff,
}

/// <summary>Why a face produced no triangles. <see cref="AreaDeviation"/> is the relative difference
/// between the ring's area and its triangles' area, set only for <see cref="FaceDropKind.AreaOff"/>.</summary>
public readonly record struct FaceDrop(FaceDropKind Kind, double AreaDeviation = 0)
{
    public string Message
        => Kind switch
        {
            FaceDropKind.TooFewBoundaryPoints => "fewer than 3 boundary points",
            FaceDropKind.TooFewDistinctPoints => "fewer than 3 distinct boundary points",
            FaceDropKind.AreaOff => $"area off by {AreaDeviation * 100:0.#} %",
            _ => "triangulation failed",
        };
}
