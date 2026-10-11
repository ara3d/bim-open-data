using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>Circles, ellipses and lines, whole or as trimmed arcs, sampled into chords.</summary>
internal static class Conics
{
    public static List<Vector2> EvaluateCircle2D(MeshingContext ctx, IfcEntity circle, bool full)
    {
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(circle, IfcCircle.Instance.Radius));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, circle, IfcConic.Instance.Position);
        return SampleCircle2D(radius, ctx.CircleSegments, full)
            .Select(p => ToWorld2(placement, p))
            .ToList();
    }

    public static List<Vector3> EvaluateCircle3D(MeshingContext ctx, IfcEntity circle, bool full)
    {
        var radius = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(circle, IfcCircle.Instance.Radius));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, circle, IfcConic.Instance.Position);
        return SampleCircle2D(radius, ctx.CircleSegments, full)
            .Select(p => placement.ToWorld(new Vector3(p.X, p.Y, 0)))
            .ToList();
    }

    static IEnumerable<Vector2> SampleCircle2D(float radius, int segments, bool full)
    {
        var count = full ? segments : segments / 2;
        for (var i = 0; i <= count; i++)
        {
            var t = MathF.Tau * i / segments;
            yield return new Vector2(MathF.Cos(t) * radius, MathF.Sin(t) * radius);
        }
    }

    public static List<Vector2> EvaluateEllipse2D(MeshingContext ctx, IfcEntity ellipse, bool full)
    {
        var semiMajor = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis1));
        var semiMinor = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(ellipse, IfcEllipse.Instance.SemiAxis2));
        var placement = Placements.ReadOptionalAxis2Placement3D(ctx, ellipse, IfcConic.Instance.Position);
        var result = new List<Vector2>();
        var count = full ? ctx.CircleSegments : ctx.CircleSegments / 2;
        for (var i = 0; i <= count; i++)
        {
            var t = MathF.Tau * i / ctx.CircleSegments;
            var local = new Vector3(semiMajor * MathF.Cos(t), semiMinor * MathF.Sin(t), 0);
            var w = placement.ToWorld(local);
            result.Add(new Vector2(w.X.Value, w.Y.Value));
        }
        return result;
    }

    public static List<Vector3> EvaluateEllipse3D(MeshingContext ctx, IfcEntity ellipse, bool full)
        => EvaluateEllipse2D(ctx, ellipse, full).Select(p => new Vector3(p.X, p.Y, 0)).ToList();

    public static List<Vector2> EvaluateLine2D(MeshingContext ctx, IfcEntity line)
    {
        var origin = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, line, IfcLine.Instance.Pnt));
        var dir = ReadLineDirection(ctx, line);
        return [new Vector2(origin.X, origin.Y), new Vector2(origin.X + dir.X, origin.Y + dir.Y)];
    }

    public static List<Vector3> EvaluateLine3D(MeshingContext ctx, IfcEntity line)
    {
        var origin = Placements.ReadPoint3D(ctx, MeshHelpers.ResolveRequired(ctx, line, IfcLine.Instance.Pnt));
        var dir = ReadLineDirection(ctx, line);
        return [origin, origin + dir];
    }

    /// <summary>The line's unit direction; the vector's magnitude is applied before normalizing.</summary>
    public static Vector3 ReadLineDirection(MeshingContext ctx, IfcEntity line)
    {
        var vector = MeshHelpers.ResolveRequired(ctx, line, IfcLine.Instance.Dir);
        var dirEntity = MeshHelpers.ResolveRequired(ctx, vector, IfcVector.Instance.Orientation);
        var dir = Placements.ReadDirection3D(ctx, dirEntity, Vector3.UnitX);
        var magnitude = (float)ctx.ScaleLength(MeshHelpers.ReadNumber(vector, IfcVector.Instance.Magnitude));
        if (magnitude > 1e-12f)
            dir *= magnitude;
        return dir.Normalize;
    }

    /// <summary>
    /// PRIMARK-style fillets use SenseAgreement=.F. with long PARAMETER trims (e.g. 0→3π/2).
    /// Shortest-path normalization collapses those to 90°; long arcs must sweep from u2 by the raw span.
    /// </summary>
    public static List<Vector2> SampleTrimmedConicArc2D(
        Frame3D placement, float a, float b, float u1, float u2, bool sense, int segments)
    {
        var (start, sweep, reverse) = ResolveConicTrimSweep(u1, u2, sense);
        var result = SampleConicArcWithSweep2D(placement, a, b, start, sweep, segments);
        if (reverse)
            result.Reverse();
        return result;
    }

    public static List<Vector3> SampleTrimmedConicArc3D(
        Frame3D placement, float a, float b, float u1, float u2, bool sense, int segments)
    {
        var (start, sweep, reverse) = ResolveConicTrimSweep(u1, u2, sense);
        var steps = ConicArcSampleCount(sweep, segments, Math.Max(a, b));
        var result = new List<Vector3>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var u = start + sweep * i / steps;
            var local = new Vector3(a * MathF.Cos(u), b * MathF.Sin(u), 0);
            result.Add(placement.ToWorld(local));
        }
        if (reverse)
            result.Reverse();
        return result;
    }

    public static List<Vector2> SampleConicArcWithSweep2D(
        Frame3D placement, float a, float b, float start, float sweep, int segments)
    {
        var steps = ConicArcSampleCount(sweep, segments, Math.Max(a, b));
        var result = new List<Vector2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var u = start + sweep * i / steps;
            var local = new Vector3(a * MathF.Cos(u), b * MathF.Sin(u), 0);
            var w = placement.ToWorld(local);
            result.Add(new Vector2(w.X.Value, w.Y.Value));
        }
        return result;
    }

    static (float Start, float Sweep, bool Reverse) ResolveConicTrimSweep(float u1, float u2, bool sense)
    {
        var raw = u2 - u1;
        if (!sense)
        {
            if (MathF.Abs(raw) > MathF.PI + 1e-3f)
                return (u2, raw, false);
            return (u1, -raw, true);
        }

        return (u1, NormalizeAngleSpan(raw, true), false);
    }

    static int ConicArcSampleCount(float span, int segments, float radius)
    {
        var fromCircumference = (int)MathF.Ceiling(MathF.Abs(span) / MathF.Tau * segments);
        var perQuadrant = Math.Max(2, segments / 4);
        var fromSpan = (int)MathF.Ceiling(MathF.Abs(span) / (MathF.PI / 2f) * perQuadrant);
        return Math.Clamp(Math.Max(fromCircumference, fromSpan), 2, 64);
    }

    static float NormalizeAngleSpan(float span, bool sense)
    {
        if (sense)
        {
            while (span < 0)
                span += MathF.Tau;
            if (span > MathF.PI)
                span -= MathF.Tau;
        }
        else
        {
            while (span > 0)
                span -= MathF.Tau;
            if (span < -MathF.PI)
                span += MathF.Tau;
        }
        return span;
    }

    static Vector2 ToWorld2(Frame3D frame, Vector2 local)
    {
        var w = frame.ToWorld(new Vector3(local.X, local.Y, 0));
        return new Vector2(w.X.Value, w.Y.Value);
    }
}
