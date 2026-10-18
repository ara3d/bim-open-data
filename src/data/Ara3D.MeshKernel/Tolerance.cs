namespace Ara3D.MeshKernel;

/// <summary>The kernel's absolute tolerances, in metres (lengths are scaled to metres while a file is read).</summary>
public static class Tolerance
{
    /// <summary>The distance below which two curve or ring points count as one, and the doubled area
    /// below which a thin face triangle is dropped. Triangulation itself (Earcut) uses no tolerance.</summary>
    public const float Eps = 1e-6f;

    /// <summary>The default for how far a face's triangles may stray from its curved surface
    /// (<see cref="FaceMesher.AddFace"/>): 1 mm, the precision building dimensions are given to. A
    /// reader that knows its file's own tolerance passes that instead.</summary>
    public const float ChordDeviation = 1e-3f;
}
