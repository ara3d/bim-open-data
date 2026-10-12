using Ara3D.Geometry;

namespace Ara3D.Ifc.Mesher;

/// <summary>A plane as a point on it and a normal. Pure clip maths, no IFC reading.</summary>
readonly record struct Plane(Vector3 Point, Vector3 Normal)
{
    const float Epsilon = 1e-6f;

    public float SignedDistance(Vector3 p) => Vector3.Dot(p - Point, Normal);

    /// <summary>The point where segment a-b crosses the plane; a and b must lie on opposite sides.</summary>
    public Vector3 Crossing(Vector3 a, Vector3 b)
    {
        var da = SignedDistance(a);
        var db = SignedDistance(b);
        return a + (b - a) * (da / (da - db));
    }

    /// <summary>
    /// The part of triangle a-b-c on the kept side: 0 (nothing), 3 (a triangle) or 4 (a quad) vertices.
    /// </summary>
    public List<Vector3> ClipTriangle(Vector3 a, Vector3 b, Vector3 c, bool keepPositiveSide)
    {
        Span<Vector3> verts = [a, b, c];
        Span<bool> inside = stackalloc bool[3];
        var insideCount = 0;
        for (var i = 0; i < 3; i++)
        {
            var d = SignedDistance(verts[i]);
            inside[i] = keepPositiveSide ? d >= -Epsilon : d <= Epsilon;
            if (inside[i])
                insideCount++;
        }
        if (insideCount == 0)
            return [];
        if (insideCount == 3)
            return [a, b, c];

        var result = new List<Vector3>(4);
        for (var i = 0; i < 3; i++)
        {
            var j = (i + 1) % 3;
            if (inside[i])
                result.Add(verts[i]);
            if (inside[i] != inside[j])
                result.Add(Crossing(verts[i], verts[j]));
        }
        return result;
    }
}
