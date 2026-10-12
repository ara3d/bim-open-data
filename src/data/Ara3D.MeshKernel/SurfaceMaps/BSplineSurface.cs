using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>A tensor-product B-spline surface: the control net indexed [u][v], a weight per control
/// point (all 1 for a non-rational surface), both knot vectors already expanded to one entry per
/// multiplicity, and the degree in each direction.</summary>
public sealed record BSplineSurface(
    IReadOnlyList<IReadOnlyList<Vector3>> ControlPoints,
    IReadOnlyList<IReadOnlyList<float>> Weights,
    IReadOnlyList<float> UKnots,
    IReadOnlyList<float> VKnots,
    int UDegree,
    int VDegree);
