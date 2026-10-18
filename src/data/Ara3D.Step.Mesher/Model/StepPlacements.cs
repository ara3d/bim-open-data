using Ara3D.Step.Entities;
using Ara3D.StepTypes;
using SN = System.Numerics;

namespace Ara3D.Step.Mesher;

/// <summary>
/// Frames and transforms in metres, as row-vector matrices (System.Numerics convention: world = local * M), so a
/// child's matrix composes as child * parent. The frame of an axis2_placement_3d follows ISO 10303-42's build_axes
/// (STEP-BREP-MODEL.md section 3.2); a cartesian_transformation_operator_3d may scale and mirror.
/// </summary>
public static class StepPlacements
{
    static readonly SN.Vector3 UnitX = SN.Vector3.UnitX;
    static readonly SN.Vector3 UnitY = SN.Vector3.UnitY;
    static readonly SN.Vector3 UnitZ = SN.Vector3.UnitZ;

    /// <summary>The matrix that carries local coordinates of a placement item (axis2_placement_3d or
    /// cartesian_transformation_operator_3d) into the coordinates of the representation holding it; null for
    /// any other item.</summary>
    public static SN.Matrix4x4? Frame(this StepEntity item, StepUnits units)
    {
        if (item.Is<IAxis2Placement3d>())
        {
            var z = item.Direction(Axis2Placement3d.Instance.Axis) ?? UnitZ;
            var x = ProjectOff(item.Direction(Axis2Placement3d.Instance.RefDirection) ?? DefaultRef(z), z) ?? ProjectOff(DefaultRef(z), z)!.Value;
            return Rows(x, SN.Vector3.Cross(z, x), z, item.Point(Axis2Placement3d.Instance.Location, units));
        }
        if (!item.Is<ICartesianTransformationOperator3d>())
            return null;
        var op = CartesianTransformationOperator3d.Instance;
        var u3 = item.Direction(op.Axis3) ?? UnitZ;
        var u1 = ProjectOff(item.Direction(op.Axis1) ?? DefaultRef(u3), u3) ?? ProjectOff(DefaultRef(u3), u3)!.Value;
        var u2 = ProjectOff(ProjectOff(item.Direction(op.Axis2) ?? UnitY, u3) ?? UnitY, u1) ?? SN.Vector3.Cross(u3, u1);
        var s = (float)(item.Number(op.Scale) ?? 1.0);
        return Rows(u1 * s, u2 * s, u3 * s, item.Point(op.LocalOrigin, units));
    }

    /// <summary>The transform that carries the coordinates of the space holding <paramref name="from"/> onto the
    /// space holding <paramref name="to"/>, so that frame lands on frame: inverse(F(from)) * F(to). Null when
    /// either item is not a placement or the source frame is singular.</summary>
    public static SN.Matrix4x4? Between(StepEntity from, StepUnits fromUnits, StepEntity to, StepUnits toUnits)
        => from.Frame(fromUnits) is { } f && to.Frame(toUnits) is { } t && SN.Matrix4x4.Invert(f, out var inverse)
            ? inverse * t
            : null;

    static SN.Vector3 DefaultRef(SN.Vector3 z)
        => z == UnitX || z == -UnitX ? UnitY : UnitX;

    static SN.Vector3? ProjectOff(SN.Vector3 a, SN.Vector3 axis)
    {
        var v = a - SN.Vector3.Dot(a, axis) * axis;
        return v.LengthSquared() < 1e-12f ? null : SN.Vector3.Normalize(v);
    }

    static SN.Matrix4x4 Rows(SN.Vector3 x, SN.Vector3 y, SN.Vector3 z, SN.Vector3 origin)
        => new(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, origin.X, origin.Y, origin.Z, 1);

    static SN.Vector3? Direction(this StepEntity e, StepAttribute<IDirection> attribute)
        => e.Ref(attribute)?.Numbers(StepTypes.Direction.Instance.DirectionRatios) is { Count: > 0 } r
           && Vector(r, 1) is var v && v.LengthSquared() > 1e-24f
            ? SN.Vector3.Normalize(v)
            : null;

    static SN.Vector3 Point(this StepEntity e, StepAttribute<ICartesianPoint> attribute, StepUnits units)
        => e.Ref(attribute)?.Numbers(CartesianPoint.Instance.Coordinates) is { Count: > 0 } c ? Vector(c, units.MetresPerUnit) : SN.Vector3.Zero;

    static SN.Vector3 Vector(IReadOnlyList<double> c, double scale)
        => new((float)(c[0] * scale), (float)(c.Count > 1 ? c[1] * scale : 0), (float)(c.Count > 2 ? c[2] * scale : 0));
}
