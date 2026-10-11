using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Cylindrical surface parameterization: 2D domain is (arc-length = radius*angle, height).
/// Angles are unwrapped along each ring so a partial or full wrap forms a simple 2D polygon;
/// holes align to the outer ring's angular band.
/// </summary>
internal sealed class CylinderMap : SurfaceMap
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

    public static bool TryBuild(MeshingContext ctx, IfcEntity surface, out CylinderMap map)
    {
        map = null!;
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(surface, IfcCylindricalSurface.Instance.Radius));
        if (radius <= 1e-7f)
            return false;
        var frame = Placements.ReadOptionalAxis2Placement3D(ctx, surface, IfcElementarySurface.Instance.Position);
        map = new CylinderMap(frame, radius);
        return true;
    }

    public override List<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
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

    /// <summary>
    /// Subdivides cylindrical face rings in surface (u,v) space so long arc chords do not flatten
    /// curved panels. Density is local to the face radius; it does not change global curve sampling.
    /// </summary>
    public static List<Vector2> DensifyUvRing(IReadOnlyList<Vector2> uv, float radius)
    {
        if (uv.Count < 2)
            return uv.ToList();

        var maxArcStep = radius * MathF.Tau / 48f;
        var refined = new List<Vector2>(uv.Count * 2);
        for (var i = 0; i < uv.Count; i++)
        {
            var a = uv[i];
            var b = uv[(i + 1) % uv.Count];
            refined.Add(a);
            var du = MathF.Abs(b.X - a.X);
            var dv = MathF.Abs(b.Y - a.Y);
            if (du <= maxArcStep && dv <= maxArcStep)
                continue;

            var steps = Math.Max(
                du > maxArcStep ? (int)MathF.Ceiling(du / maxArcStep) : 1,
                dv > maxArcStep ? (int)MathF.Ceiling(dv / maxArcStep) : 1);
            steps = Math.Min(steps, 24);
            for (var s = 1; s < steps; s++)
            {
                var t = s / (float)steps;
                refined.Add(new Vector2(
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t));
            }
        }

        return refined;
    }
}
