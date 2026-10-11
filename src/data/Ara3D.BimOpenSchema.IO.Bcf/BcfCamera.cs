using System.Numerics;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>A BCF perspective camera in model coordinates (z up, metres).
/// <see cref="FieldOfView"/> is the vertical angle in degrees.</summary>
public readonly record struct BcfCamera(
    Vector3 ViewPoint,
    Vector3 Direction,
    Vector3 Up,
    double FieldOfView,
    double AspectRatio)
{
    /// <summary>The view direction: from above, looking north-west and down, so the camera sits
    /// south-east of the elements. Not parallel to z, which BCF forbids for the up vector.</summary>
    public static readonly Vector3 ViewDirection = Vector3.Normalize(new(-1, 1, -1));

    /// <summary>A radius below which a box is framed as if it were this size, so a point-like
    /// element still gets a camera a little way off rather than inside it. In metres.</summary>
    public const float MinimumRadius = 0.5f;

    /// <summary>Looks along <see cref="ViewDirection"/> at the box's centre, from just far
    /// enough that the box's bounding sphere fits the narrower of the two fields of view.</summary>
    public static BcfCamera Frame(BosBox box, double fieldOfView, double aspectRatio)
    {
        var radius = Math.Max(box.Size.Length() / 2, MinimumRadius);
        var halfVertical = fieldOfView * Math.PI / 360;
        var halfHorizontal = Math.Atan(Math.Tan(halfVertical) * aspectRatio);
        var distance = radius / Math.Sin(Math.Min(halfVertical, halfHorizontal));
        var up = Vector3.Normalize(Vector3.UnitZ - Vector3.Dot(Vector3.UnitZ, ViewDirection) * ViewDirection);
        return new(box.Center - ViewDirection * (float)distance, ViewDirection, up, fieldOfView, aspectRatio);
    }
}
