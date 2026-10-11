#nullable enable
using System.Numerics;

namespace Ara3D.BimOpenSchema;

/// <summary>An axis-aligned box in model coordinates: BOS geometry's z-up metres.</summary>
public readonly record struct BosBox(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    public BosBox Union(BosBox other)
        => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <summary>The box of this box's eight corners moved by <paramref name="matrix"/>
    /// (System.Numerics row vectors). It contains the moved contents of this box.</summary>
    public BosBox Transform(Matrix4x4 matrix)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? Min.X : Max.X, (i & 2) == 0 ? Min.Y : Max.Y, (i & 4) == 0 ? Min.Z : Max.Z);
            var moved = Vector3.Transform(corner, matrix);
            min = Vector3.Min(min, moved);
            max = Vector3.Max(max, moved);
        }
        return new(min, max);
    }
}
