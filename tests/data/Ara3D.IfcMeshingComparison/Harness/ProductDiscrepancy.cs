using Ara3D.Geometry;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>What one mesher produced for one product, with its shape score against the majority.</summary>
/// <param name="ShapeScore">Mean shape similarity to the majority members other than itself (1 = same shape).</param>
/// <param name="BoundsClose">Bounds within 5 % of the first majority member's bounds.</param>
public readonly record struct MesherView(
    int Triangles,
    Bounds3D Bounds,
    double Volume,
    double ShapeScore,
    bool BoundsClose);

/// <summary>The one-word verdicts of a <see cref="ProductDiscrepancy"/>.</summary>
public static class Verdicts
{
    public const string AllAgree = "all-agree";
    public const string OursAgrees = "ours-agrees";
    public const string OursDiffers = "ours-differs";
    public const string NoConsensus = "no-consensus";
    public const string MissingInOurs = "missing-in-ours";
    public const string ExtraInOurs = "extra-in-ours";
}

/// <summary>One product (express id) across the meshers. <see cref="Meshers"/> holds only the meshers
/// that produced geometry for it. <see cref="Distance"/> is 1 minus our shape score against the majority
/// (1 when we are missing or extra); the table is sorted by it, largest first.</summary>
public sealed record ProductDiscrepancy(
    int ExpressId,
    string ProductKind,
    string GeometryKind,
    IReadOnlyDictionary<string, MesherView> Meshers,
    IReadOnlyList<string> Majority,
    string Verdict,
    double Distance);
