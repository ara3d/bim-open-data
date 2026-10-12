using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>The one sampler for circles and circular arcs, so every profile, curve and sweep puts
/// its points in the same places and the same order.</summary>
public static class CircleSamples
{
    /// <summary>A full circle centred on the origin: <paramref name="segments"/> points, the first at
    /// angle 0, going counter-clockwise, with no closing duplicate.</summary>
    public static IReadOnlyList<Vector2> Ring(float radius, int segments)
        => Enumerable.Range(0, segments).Select(i =>
        {
            var t = MathF.Tau * i / segments;
            return new Vector2(MathF.Cos(t) * radius, MathF.Sin(t) * radius);
        }).ToList();

    /// <summary><paramref name="segments"/> + 1 points along the arc from angle
    /// <paramref name="start"/> to <paramref name="end"/> (radians), both ends included.</summary>
    public static IReadOnlyList<Vector2> Arc(Vector2 center, float radius, float start, float end, int segments)
        => Enumerable.Range(0, segments + 1).Select(i =>
        {
            var a = start + (end - start) * i / segments;
            return center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }).ToList();
}
