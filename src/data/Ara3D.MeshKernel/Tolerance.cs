namespace Ara3D.MeshKernel;

/// <summary>The kernel's one absolute tolerance, in metres: the distance below which two curve or
/// ring points count as one, and the doubled area below which a thin face triangle is dropped.
/// Triangulation itself (Earcut) uses no tolerance.</summary>
public static class Tolerance
{
    public const float Eps = 1e-6f;
}
