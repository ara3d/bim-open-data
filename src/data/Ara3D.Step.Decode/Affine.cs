using Ara3D.StepBrep;

namespace Ara3D.Step.Decode;

/// <summary>A 3D affine map in double precision, row-vector convention as the Plato library's AffineTransform3D
/// (Matrix4x3 rows 1 to 3 the linear part, row 4 the translation): p' = p * L + T, so a child's map composes as
/// child.Then(parent).</summary>
public readonly record struct Affine(Vec3 X, Vec3 Y, Vec3 Z, Vec3 T)
{
    public static readonly Affine Identity = new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 0, 0));

    public static Affine Scale(double s)
        => new(new(s, 0, 0), new(0, s, 0), new(0, 0, s), new(0, 0, 0));

    public Vec3 Point(Vec3 p)
        => Vector(p) + T;

    public Vec3 Vector(Vec3 v)
        => X * v.X + Y * v.Y + Z * v.Z;

    /// <summary>This map followed by <paramref name="next"/>.</summary>
    public Affine Then(Affine next)
        => new(next.Vector(X), next.Vector(Y), next.Vector(Z), next.Point(T));

    public double Determinant
        => X.Dot(Y.Cross(Z));

    /// <summary>How much the map scales lengths: the cube root of the absolute determinant, exact for the
    /// similarities STEP placements and unit conversions produce.</summary>
    public double LengthScale
        => Math.Cbrt(Math.Abs(Determinant));

    /// <summary>The inverse map; null when the linear part is singular.</summary>
    public Affine? Inverse()
    {
        var det = Determinant;
        if (det == 0 || !double.IsFinite(det))
            return null;
        var cx = Y.Cross(Z) / det;
        var cy = Z.Cross(X) / det;
        var cz = X.Cross(Y) / det;
        var linear = new Affine(new(cx.X, cy.X, cz.X), new(cx.Y, cy.Y, cz.Y), new(cx.Z, cy.Z, cz.Z), new(0, 0, 0));
        return linear with { T = -linear.Vector(T) };
    }

    public AffineTransform3D ToPlato()
        => new(new Matrix4x3(X.ToNumber3(), Y.ToNumber3(), Z.ToNumber3(), T.ToNumber3()));
}
