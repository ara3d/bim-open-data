namespace Ara3D.Ifc.Mesher.Approach1;

/// <summary>Approach1's absolute tolerance, in scaled model units (metres): the distance below
/// which two curve or ring points count as one, and the doubled area below which a thin face
/// triangle is dropped. It is the 1e-6 that Approach1 used to borrow from the SDK's
/// PolygonTriangulator; triangulation itself (Earcut) uses no tolerance.</summary>
internal static class Tolerance
{
    public const float Eps = 1e-6f;
}
