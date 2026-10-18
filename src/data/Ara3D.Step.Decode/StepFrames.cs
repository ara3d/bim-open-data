using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>A right-handed or mirrored frame in a representation's own coordinates and length unit: unit axes and
/// the origin as the file writes them.</summary>
public readonly record struct LocalFrame(Vec3 Origin, Vec3 X, Vec3 Y, Vec3 Z)
{
    /// <summary>The map from this frame's coordinates into the representation's, lengths times
    /// <paramref name="scale"/> (millimetres per unit) and the axes times <paramref name="axisScale"/> (a
    /// cartesian_transformation_operator's scale).</summary>
    public Affine ToAffine(double scale, double axisScale = 1)
        => new(X * axisScale, Y * axisScale, Z * axisScale, Origin * scale);
}

/// <summary>Points, directions and frames read from their instances, in the file's numbers. Frames follow ISO
/// 10303-42 build_axes (axis2_placement_3d) and base_axis (cartesian_transformation_operator_3d), with the
/// default reference direction X, or Y when the axis lies along X, as Ara3D.Step.Mesher's StepPlacements does.</summary>
public static class StepFrames
{
    static readonly Vec3 UnitX = new(1, 0, 0);
    static readonly Vec3 UnitY = new(0, 1, 0);
    static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 PointOf(this StepEntity point)
        => point.Is<ST.ICartesianPoint>() && point.Numbers(ST.CartesianPoint.Instance.Coordinates) is { } c
            ? Vec3.Of(c)
            : throw new DecodeException(StepDiagnosticCode.InstanceSkipped(), point.Id);

    /// <summary>The unit vector of a direction; a direction with no non-zero ratio stops the row.</summary>
    public static Vec3 DirectionOf(this StepEntity direction)
        => direction.Numbers(ST.Direction.Instance.DirectionRatios) is { } r && Vec3.Of(r).Normalized() is { } unit
            ? unit
            : throw new DecodeException(StepDiagnosticCode.ZeroDirection(), direction.Id);

    /// <summary>A vector: its orientation's unit vector times its magnitude.</summary>
    public static Vec3 VectorOf(this StepEntity vector)
        => vector.Ref(ST.Vector.Instance.Orientation)!.Value.DirectionOf() * (vector.Number(ST.Vector.Instance.Magnitude) ?? 0);

    public static Vec3 PointAt<T>(this StepEntity e, ST.StepAttribute<T> attribute) where T : ST.IStepValue
        => e.Ref(attribute)!.Value.PointOf();

    public static Vec3? DirectionAt<T>(this StepEntity e, ST.StepAttribute<T> attribute) where T : ST.IStepValue
        => e.Ref(attribute)?.DirectionOf();

    /// <summary>The frame of an axis2_placement_3d; an axis parallel to the reference direction is reported
    /// and the default reference direction used.</summary>
    public static LocalFrame Axis2Placement3D(this StepEntity placement, DecodeFindings findings)
    {
        var p = ST.Axis2Placement3d.Instance;
        var z = placement.DirectionAt(p.Axis) ?? UnitZ;
        var x = FirstProjectionAxis(z, placement.DirectionAt(p.RefDirection), placement.Id, findings);
        return new(placement.PointAt(p.Location), x, z.Cross(x), z);
    }

    /// <summary>The frame of an axis2_placement_2d, in the plane (Z is 0).</summary>
    public static LocalFrame Axis2Placement2D(this StepEntity placement)
    {
        var p = ST.Axis2Placement2d.Instance;
        var x = placement.DirectionAt(p.RefDirection) ?? UnitX;
        return new(placement.PointAt(p.Location), x, new(-x.Y, x.X, 0), UnitZ);
    }

    /// <summary>The map of a placement item (axis2_placement_3d or cartesian_transformation_operator_3d) from its
    /// coordinates into its representation's, lengths in millimetres; null for any other item.</summary>
    public static Affine? PlacementMap(this StepEntity item, double millimetresPerUnit, DecodeFindings findings)
    {
        if (item.Is<ST.IAxis2Placement3d>())
            return item.Axis2Placement3D(findings).ToAffine(millimetresPerUnit);
        if (!item.Is<ST.ICartesianTransformationOperator3d>())
            return null;
        var op = ST.CartesianTransformationOperator3d.Instance;
        var u3 = item.DirectionAt(op.Axis3) ?? UnitZ;
        var u1 = FirstProjectionAxis(u3, item.DirectionAt(op.Axis1), item.Id, findings);
        var u2 = item.DirectionAt(op.Axis2) is { } a2 && ProjectOff(ProjectOff(a2, u3), u1).Normalized() is { } v ? v : u3.Cross(u1);
        var origin = item.Ref(op.LocalOrigin) is { } o ? o.PointOf() : new Vec3(0, 0, 0);
        return new LocalFrame(origin, u1, u2, u3).ToAffine(millimetresPerUnit, item.Number(op.Scale) ?? 1);
    }

    static Vec3 FirstProjectionAxis(Vec3 z, Vec3? reference, int id, DecodeFindings findings)
    {
        if (reference is { } r && ProjectOff(r, z).Normalized() is { } x)
            return x;
        if (reference is not null)
            findings.Once(StepDiagnosticCode.ParallelPlacementAxes(), id);
        var fallback = z == UnitX || z == -UnitX ? UnitY : UnitX;
        return ProjectOff(fallback, z).Normalized()!.Value;
    }

    static Vec3 ProjectOff(Vec3 a, Vec3 axis)
        => a - axis * a.Dot(axis);
}
