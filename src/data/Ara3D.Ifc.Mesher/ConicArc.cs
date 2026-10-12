using Ara3D.Geometry;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// An arc of a circle or ellipse in its own plane: semi-axes, the parameter where it starts, and
/// the signed sweep in radians. Carries the one density rule for conic arcs.
/// </summary>
public readonly record struct ConicArc(float SemiAxisA, float SemiAxisB, float Start, float Sweep)
{
    /// <summary>An arc gets 11/8 of the chords a circle gets at the same <c>CircleSegments</c>: the
    /// other four meshers cut the 6.35 mm fillet arcs of example.ifc's L-angle columns into 11 chords
    /// per quarter turn (TKT-65), where 32 segments per circle gives 8. A short arc is a fillet, and
    /// fillets are where a coarse chord shows in the outline.</summary>
    public const float DensityFactor = 11f / 8f;
    const int MaxChords = 64;

    /// <summary>Chords for this arc: <paramref name="circleSegments"/> per full turn times
    /// <see cref="DensityFactor"/>, scaled by the sweep as a fraction of a turn, rounded up; at
    /// least 2 and at most 64.</summary>
    public int Chords(int circleSegments)
    {
        var turns = MathF.Abs(Sweep) / MathF.Tau;
        return Math.Clamp((int)MathF.Ceiling(turns * circleSegments * DensityFactor), 2, MaxChords);
    }

    /// <summary>The point at chord <paramref name="index"/> of <paramref name="chords"/>, in the conic's plane.</summary>
    public Vector3 LocalPoint(int index, int chords)
    {
        var u = Start + Sweep * index / chords;
        return new Vector3(SemiAxisA * MathF.Cos(u), SemiAxisB * MathF.Sin(u), 0);
    }
}

/// <summary>An IFCTRIMMEDCURVE on a conic: semi-axes, the two trim parameters, and SenseAgreement.</summary>
public readonly record struct ConicTrim(float SemiAxisA, float SemiAxisB, float U1, float U2, bool Sense)
{
    /// <summary>The arc to sample and whether the samples run against the trim direction.
    /// PRIMARK-style fillets use SenseAgreement=.F. with long PARAMETER trims (e.g. 0 to 3 pi / 2);
    /// shortest-path normalization collapses those to 90 degrees, so a long arc sweeps from U2 by the raw span.</summary>
    public (ConicArc Arc, bool Reverse) Resolve()
    {
        var raw = U2 - U1;
        if (Sense)
            return (new ConicArc(SemiAxisA, SemiAxisB, U1, NormalizeSweep(raw)), false);
        if (MathF.Abs(raw) > MathF.PI + 1e-3f)
            return (new ConicArc(SemiAxisA, SemiAxisB, U2, raw), false);
        return (new ConicArc(SemiAxisA, SemiAxisB, U1, -raw), true);
    }

    static float NormalizeSweep(float span)
    {
        while (span < 0)
            span += MathF.Tau;
        return span > MathF.PI ? span - MathF.Tau : span;
    }
}
