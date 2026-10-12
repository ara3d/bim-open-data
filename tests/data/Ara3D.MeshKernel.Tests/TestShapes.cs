using Ara3D.Geometry;

namespace Ara3D.MeshKernel.Tests;

/// <summary>Hand-built inputs shared by the kernel tests.</summary>
public static class TestShapes
{
    static Vector2 P(float x, float y) => new(x, y);

    /// <summary>Unit square, counter-clockwise.</summary>
    public static IReadOnlyList<Vector2> UnitSquare { get; } = [P(0, 0), P(1, 0), P(1, 1), P(0, 1)];

    /// <summary>2 by 2 square, counter-clockwise, from (0,0) to (2,2).</summary>
    public static IReadOnlyList<Vector2> BigSquare { get; } = [P(0, 0), P(2, 0), P(2, 2), P(0, 2)];

    /// <summary>1 by 1 square centred in <see cref="BigSquare"/>, counter-clockwise.</summary>
    public static IReadOnlyList<Vector2> CentredHole { get; } = [P(0.5f, 0.5f), P(1.5f, 0.5f), P(1.5f, 1.5f), P(0.5f, 1.5f)];

    /// <summary>2 by 2 square with a centred 1 by 1 hole.</summary>
    public static PolygonWithHoles SquareWithHole() => new(BigSquare, [CentredHole]);

    /// <summary>Four points whose edges cross each other.</summary>
    public static IReadOnlyList<Vector2> BowTie { get; } = [P(0, 0), P(2, 1), P(2.5f, 0), P(0.5f, 1)];
}
