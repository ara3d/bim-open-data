using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// Cylindrical surface parameterization: 2D domain is (arc-length = radius*angle, height).
/// Angles are unwrapped along each ring so a partial or full wrap forms a simple 2D polygon;
/// holes align to the outer ring's angular band.
/// </summary>
public sealed class CylinderMap : SurfaceMap
{
    readonly Frame3D _frame;
    readonly float _radius;
    float _refU;
    bool _hasRef;

    public CylinderMap(Frame3D frame, float radius)
    {
        _frame = frame;
        _radius = radius;
    }

    public float Radius => _radius;

    public override IReadOnlyList<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
    {
        var result = new List<Vector2>(ring.Count);
        var prevU = 0f;
        var first = true;
        foreach (var p in ring)
        {
            var local = _frame.ToLocal(p);
            var u = MathF.Atan2(local.Y.Value, local.X.Value);
            if (!first)
                u += MathF.Round((prevU - u) / MathF.Tau) * MathF.Tau;
            else if (_hasRef)
                u += MathF.Round((_refU - u) / MathF.Tau) * MathF.Tau;
            prevU = u;
            first = false;
            result.Add(new Vector2(_radius * u, local.Z.Value));
        }
        if (!_hasRef && result.Count > 0)
        {
            _refU = result[0].X / _radius;
            _hasRef = true;
        }
        return result;
    }

    public override Vector3 Unproject(Vector2 uv)
    {
        var u = uv.X / _radius;
        return _frame.ToWorld(new Vector3(_radius * MathF.Cos(u), _radius * MathF.Sin(u), uv.Y));
    }

    /// <summary>The exact distance from <paramref name="chordPoint"/> to the cylinder.</summary>
    public override float ChordDeviation(Vector2 uv, Vector3 chordPoint)
    {
        var local = _frame.ToLocal(chordPoint);
        return MathF.Abs(MathF.Sqrt(local.X.Value * local.X.Value + local.Y.Value * local.Y.Value) - _radius);
    }
}
