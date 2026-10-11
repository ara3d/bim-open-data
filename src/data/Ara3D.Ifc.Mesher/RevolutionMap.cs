using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Surface-of-revolution parameterization. The 2D domain is (refRadius·angle, meridian arc-length):
/// each boundary point is decomposed into its revolution angle about the axis and its position along
/// the meridian (the swept profile expressed in the axis frame). Interior points triangulated in this
/// domain unproject by evaluating the meridian at the given arc-length and rotating about the axis, so
/// the tessellation follows the true revolved surface instead of a flat chord across it.
/// </summary>
internal sealed class RevolutionMap : SurfaceMap
{
    readonly Frame3D _frame; // origin = axis point, Z = axis dir, X = meridian radial reference
    readonly float _refRadius;
    readonly float[] _s; // cumulative meridian arc-length
    readonly float[] _r; // meridian radial distance from axis
    readonly float[] _a; // meridian axial position along axis
    float _refAngle;
    bool _hasRef;

    RevolutionMap(Frame3D frame, float refRadius, float[] s, float[] r, float[] a)
        => (_frame, _refRadius, _s, _r, _a) = (frame, refRadius, s, r, a);

    /// <summary>Reads the surface's axis and swept profile; false when either is missing, degenerate,
    /// or fails to read, so the caller falls back to a flat projection.</summary>
    public static bool TryBuild(MeshingContext ctx, IfcEntity surface, out RevolutionMap map)
    {
        map = null!;
        try
        {
            var axisEntity = MeshHelpers.ResolveOptional(ctx, surface, IfcSurfaceOfRevolution.Instance.AxisPosition);
            if (axisEntity is null)
                return false;
            var position = Placements.ReadOptionalAxis2Placement3D(ctx, surface, IfcSweptSurface.Instance.Position);

            var axisLocal = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, axisEntity, IfcPlacement.Instance.Location));
            var axisDirLocal = MeshHelpers.ResolveOptional(ctx, axisEntity, IfcAxis1Placement.Instance.Axis) is { } ax
                ? Placements.ReadDirection3D(ctx, ax, Vector3.UnitZ)
                : Vector3.UnitZ;

            var axisPoint = position.ToWorld(axisLocal);
            var axisDir = position.ToWorldDirection(axisDirLocal);

            var sweptCurve = MeshHelpers.ResolveRequired(ctx, surface, IfcSweptSurface.Instance.SweptCurve);
            var meridian = EvaluateMeridianCurve3D(ctx, sweptCurve)
                .Select(p => position.ToWorld(p))
                .ToList();

            var built = Build(axisPoint, axisDir, meridian);
            if (built is null)
                return false;
            map = built;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static RevolutionMap? Build(Vector3 axisPoint, Vector3 axisDir, IReadOnlyList<Vector3> meridian)
    {
        if (meridian.Count < 2)
            return null;
        axisDir = axisDir.Normalize;

        // Radial reference direction: from the axis to the meridian point farthest from it.
        var bestRadial = Vector3.Zero;
        var bestR = 0f;
        foreach (var m in meridian)
        {
            var rel = m - axisPoint;
            var axial = Vector3.Dot(rel, axisDir).Value;
            var radial = rel - axisDir * axial;
            var rr = radial.Length.Value;
            if (rr > bestR)
            {
                bestR = rr;
                bestRadial = radial;
            }
        }
        if (bestR < 1e-7f)
            return null;

        var x0 = bestRadial.Normalize;
        var y0 = Vector3.Cross(axisDir, x0).Normalize;
        var frame = new Frame3D(
            new Point3D(axisPoint.X, axisPoint.Y, axisPoint.Z),
            new OrthonormalBasis3D(new Axes3D(x0, y0, axisDir), true));

        var n = meridian.Count;
        var s = new float[n];
        var r = new float[n];
        var a = new float[n];
        for (var i = 0; i < n; i++)
        {
            var local = frame.ToLocal(meridian[i]);
            r[i] = MathF.Sqrt(local.X.Value * local.X.Value + local.Y.Value * local.Y.Value);
            a[i] = local.Z.Value;
            s[i] = i == 0 ? 0f : s[i - 1] + Hypot(r[i] - r[i - 1], a[i] - a[i - 1]);
        }
        if (s[n - 1] < 1e-9f)
            return null;

        return new RevolutionMap(frame, bestR, s, r, a);
    }

    /// <summary>Evaluates a swept-surface profile (curve or arbitrary profile wrapping a curve) to 3D points.</summary>
    static List<Vector3> EvaluateMeridianCurve3D(MeshingContext ctx, IfcEntity sweptCurve)
    {
        var curve = sweptCurve.GetEntityName() switch
        {
            "IFCARBITRARYOPENPROFILEDEF" or "IFCCENTERLINEPROFILEDEF"
                => MeshHelpers.ResolveOptional(ctx, sweptCurve, IfcArbitraryOpenProfileDef.Instance.Curve),
            "IFCARBITRARYCLOSEDPROFILEDEF"
                => MeshHelpers.ResolveOptional(ctx, sweptCurve, IfcArbitraryClosedProfileDef.Instance.OuterCurve),
            _ => sweptCurve,
        };
        return curve is null ? [] : FaceBounds.DedupeConsecutive3D(CurveEvaluator.Evaluate3D(ctx, curve).ToList());
    }

    public override List<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
    {
        var result = new List<Vector2>(ring.Count);
        var prevAngle = 0f;
        var first = true;
        foreach (var p in ring)
        {
            var local = _frame.ToLocal(p);
            var angle = MathF.Atan2(local.Y.Value, local.X.Value);
            if (!first)
                angle += MathF.Round((prevAngle - angle) / MathF.Tau) * MathF.Tau;
            else if (_hasRef)
                angle += MathF.Round((_refAngle - angle) / MathF.Tau) * MathF.Tau;
            prevAngle = angle;
            first = false;
            var radial = MathF.Sqrt(local.X.Value * local.X.Value + local.Y.Value * local.Y.Value);
            result.Add(new Vector2(_refRadius * angle, ArcLengthAt(radial, local.Z.Value)));
        }
        if (!_hasRef && result.Count > 0)
        {
            _refAngle = result[0].X / _refRadius;
            _hasRef = true;
        }
        return result;
    }

    public override Vector3 Unproject(Vector2 uv)
    {
        var angle = uv.X / _refRadius;
        var (radius, axial) = MeridianAt(uv.Y);
        return _frame.ToWorld(new Vector3(radius * MathF.Cos(angle), radius * MathF.Sin(angle), axial));
    }

    // Arc-length of the meridian point nearest to (r, a) in the meridian half-plane.
    float ArcLengthAt(float r, float a)
    {
        var bestS = _s[0];
        var bestDsq = float.MaxValue;
        for (var i = 0; i + 1 < _s.Length; i++)
        {
            var dr = _r[i + 1] - _r[i];
            var da = _a[i + 1] - _a[i];
            var lenSq = dr * dr + da * da;
            var t = lenSq < 1e-20f ? 0f : ((r - _r[i]) * dr + (a - _a[i]) * da) / lenSq;
            t = Math.Clamp(t, 0f, 1f);
            var pr = _r[i] + dr * t;
            var pa = _a[i] + da * t;
            var dsq = (r - pr) * (r - pr) + (a - pa) * (a - pa);
            if (dsq < bestDsq)
            {
                bestDsq = dsq;
                bestS = _s[i] + (_s[i + 1] - _s[i]) * t;
            }
        }
        return bestS;
    }

    (float Radius, float Axial) MeridianAt(float s)
    {
        var last = _s.Length - 1;
        if (s <= _s[0])
            return (_r[0], _a[0]);
        if (s >= _s[last])
            return (_r[last], _a[last]);
        for (var i = 0; i + 1 < _s.Length; i++)
        {
            if (s <= _s[i + 1])
            {
                var seg = _s[i + 1] - _s[i];
                var t = seg < 1e-20f ? 0f : (s - _s[i]) / seg;
                return (_r[i] + (_r[i + 1] - _r[i]) * t, _a[i] + (_a[i + 1] - _a[i]) * t);
            }
        }
        return (_r[last], _a[last]);
    }

    static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);
}
