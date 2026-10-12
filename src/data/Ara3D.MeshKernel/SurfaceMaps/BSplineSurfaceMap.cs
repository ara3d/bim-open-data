using Ara3D.Geometry;

namespace Ara3D.MeshKernel;

/// <summary>
/// B-spline (NURBS) surface parameterization. The 2D domain is the surface (u,v) parameter rectangle
/// scaled to world-comparable units. Boundary points are located to (u,v) via nearest-point search on
/// a pre-sampled surface grid; interior points unproject by evaluating the surface (Cox-de Boor tensor
/// product), so wrapping curved panels tessellate on the true surface rather than a flat chord.
/// </summary>
public sealed class BSplineSurfaceMap : SurfaceMap
{
    readonly Vector3[][] _ctrl; // [uIndex][vIndex]
    readonly float[][] _w;
    readonly float[] _uKnots, _vKnots;
    readonly int _pu, _pv;
    readonly float _uMin, _uMax, _vMin, _vMax, _uScale, _vScale;
    readonly Vector3[] _grid3D;
    readonly Vector2[] _grid2D;

    BSplineSurfaceMap(Vector3[][] ctrl, float[][] w, float[] uKnots, float[] vKnots, int pu, int pv)
    {
        _ctrl = ctrl;
        _w = w;
        _uKnots = uKnots;
        _vKnots = vKnots;
        _pu = pu;
        _pv = pv;
        _uMin = uKnots[pu];
        _uMax = uKnots[ctrl.Length];
        _vMin = vKnots[pv];
        _vMax = vKnots[ctrl[0].Length];

        var uMid = 0.5f * (_uMin + _uMax);
        var vMid = 0.5f * (_vMin + _vMax);
        var uLen = (Eval(_uMax, vMid) - Eval(_uMin, vMid)).Length.Value;
        var vLen = (Eval(uMid, _vMax) - Eval(uMid, _vMin)).Length.Value;
        _uScale = _uMax > _uMin ? uLen / (_uMax - _uMin) : 1f;
        _vScale = _vMax > _vMin ? vLen / (_vMax - _vMin) : 1f;
        if (_uScale < 1e-6f) _uScale = 1f;
        if (_vScale < 1e-6f) _vScale = 1f;

        const int gridN = 32;
        _grid3D = new Vector3[(gridN + 1) * (gridN + 1)];
        _grid2D = new Vector2[(gridN + 1) * (gridN + 1)];
        var k = 0;
        for (var i = 0; i <= gridN; i++)
        {
            var u = _uMin + (_uMax - _uMin) * i / gridN;
            for (var j = 0; j <= gridN; j++)
            {
                var v = _vMin + (_vMax - _vMin) * j / gridN;
                _grid3D[k] = Eval(u, v);
                _grid2D[k] = new Vector2((u - _uMin) * _uScale, (v - _vMin) * _vScale);
                k++;
            }
        }
    }

    /// <summary>The map for a surface; null when the net has under two points in a direction, a
    /// degree is below 1, or a knot vector's length is not points + degree + 1.</summary>
    public static BSplineSurfaceMap? Build(BSplineSurface surface)
    {
        var ctrl = surface.ControlPoints.Select(row => row.ToArray()).ToArray();
        if (ctrl.Length < 2 || ctrl[0].Length < 2)
            return null;
        var pu = surface.UDegree;
        var pv = surface.VDegree;
        if (pu < 1 || pv < 1)
            return null;

        var uKnots = surface.UKnots.ToArray();
        var vKnots = surface.VKnots.ToArray();
        if (uKnots.Length != ctrl.Length + pu + 1 || vKnots.Length != ctrl[0].Length + pv + 1)
            return null;

        var w = surface.Weights.Select(row => row.ToArray()).ToArray();
        return new BSplineSurfaceMap(ctrl, w, uKnots, vKnots, pu, pv);
    }

    public override IReadOnlyList<Vector2> ProjectRing(IReadOnlyList<Vector3> ring)
    {
        var result = new List<Vector2>(ring.Count);
        foreach (var p in ring)
        {
            var best = 0;
            var bestDsq = float.MaxValue;
            for (var i = 0; i < _grid3D.Length; i++)
            {
                var dsq = (_grid3D[i] - p).LengthSquared.Value;
                if (dsq < bestDsq)
                {
                    bestDsq = dsq;
                    best = i;
                }
            }
            result.Add(_grid2D[best]);
        }
        return result;
    }

    public override Vector3 Unproject(Vector2 uv)
    {
        var u = Math.Clamp(_uMin + uv.X / _uScale, _uMin, _uMax);
        var v = Math.Clamp(_vMin + uv.Y / _vScale, _vMin, _vMax);
        return Eval(u, v);
    }

    Vector3 Eval(float u, float v)
    {
        var nu = _ctrl.Length;
        var nv = _ctrl[0].Length;
        var su = FindSpan(nu - 1, _pu, u, _uKnots);
        var sv = FindSpan(nv - 1, _pv, v, _vKnots);
        var bu = BasisFuns(su, u, _pu, _uKnots);
        var bv = BasisFuns(sv, v, _pv, _vKnots);

        var num = Vector3.Zero;
        var den = 0f;
        for (var i = 0; i <= _pu; i++)
        {
            var ci = su - _pu + i;
            for (var j = 0; j <= _pv; j++)
            {
                var cj = sv - _pv + j;
                var b = bu[i] * bv[j] * _w[ci][cj];
                num += _ctrl[ci][cj] * b;
                den += b;
            }
        }
        return den < 1e-12f ? num : num / den;
    }

    static int FindSpan(int n, int p, float u, float[] knots)
    {
        if (u >= knots[n + 1]) return n;
        if (u <= knots[p]) return p;
        int low = p, high = n + 1, mid = (low + high) / 2;
        while (u < knots[mid] || u >= knots[mid + 1])
        {
            if (u < knots[mid]) high = mid;
            else low = mid;
            mid = (low + high) / 2;
        }
        return mid;
    }

    static float[] BasisFuns(int span, float u, int p, float[] knots)
    {
        var n = new float[p + 1];
        var left = new float[p + 1];
        var right = new float[p + 1];
        n[0] = 1f;
        for (var j = 1; j <= p; j++)
        {
            left[j] = u - knots[span + 1 - j];
            right[j] = knots[span + j] - u;
            var saved = 0f;
            for (var r = 0; r < j; r++)
            {
                var denom = right[r + 1] + left[j - r];
                var temp = denom == 0f ? 0f : n[r] / denom;
                n[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            n[j] = saved;
        }
        return n;
    }
}
