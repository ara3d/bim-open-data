using Ara3D.Geometry;

namespace Ara3D.Ifc.Mesher.Tests;

public static class MatrixNorms
{
    /// <summary>The Frobenius norm: the square root of the sum of the squares of the 16 elements.
    /// Applied to a difference of two matrices it measures how far apart they are element-wise.</summary>
    public static double Frobenius(Matrix4x4 m)
        => Math.Sqrt(
            Sq(m.M11) + Sq(m.M12) + Sq(m.M13) + Sq(m.M14) +
            Sq(m.M21) + Sq(m.M22) + Sq(m.M23) + Sq(m.M24) +
            Sq(m.M31) + Sq(m.M32) + Sq(m.M33) + Sq(m.M34) +
            Sq(m.M41) + Sq(m.M42) + Sq(m.M43) + Sq(m.M44));

    static double Sq(float x)
        => (double)x * x;
}
