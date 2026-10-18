using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>A double-precision 3D vector for the decoder's own arithmetic, converted to the Plato library's types
/// only when a kernel row is written.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public double Dot(Vec3 b)
        => X * b.X + Y * b.Y + Z * b.Z;

    public Vec3 Cross(Vec3 b)
        => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    public double Length
        => Math.Sqrt(Dot(this));

    /// <summary>The unit vector along this one; null when it is too short to normalize.</summary>
    public Vec3? Normalized()
        => Length is var length && length > 0 && double.IsFinite(length) ? this / length : null;

    /// <summary>The coordinates of a STEP list of one to three numbers; missing ones are 0.</summary>
    public static Vec3 Of(IReadOnlyList<double> c)
        => new(c.Count > 0 ? c[0] : 0, c.Count > 1 ? c[1] : 0, c.Count > 2 ? c[2] : 0);

    public Number3 ToNumber3() => new(X, Y, Z);
    public Point3D ToPoint() => new(X, Y, Z);
    public Vector3D ToVector() => new(X, Y, Z);
    public Direction3D ToDirection() => new(ToVector());
}
