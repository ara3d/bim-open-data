namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>The result of voting among the meshers other than ours for one product.</summary>
public readonly record struct Consensus(IReadOnlyList<int> Members, bool Exists);

/// <summary>Majority rule for <see cref="DiscrepancyTable"/>. Two meshers agree when their shape
/// similarity is at least <see cref="AgreeThreshold"/> (the 0.9 that ModelComparer counts as matched).
/// The majority is the largest group of the other meshers that agree with one of them (that mesher plus
/// everyone agreeing with it); ties go to the earlier mesher in catalog order. Ours never votes, so it
/// cannot decide its own verdict. With one other mesher, that mesher is the majority; with two or more,
/// a majority needs at least two members.</summary>
public static class DiscrepancyTableConsensus
{
    public const double AgreeThreshold = 0.9;

    public static Consensus Find(IReadOnlyList<int> voters, Func<int, int, double> similarity)
    {
        if (voters.Count == 0)
            return new Consensus([], false);
        var best = new List<int>();
        foreach (var seed in voters)
        {
            var group = voters.Where(v => v == seed || similarity(seed, v) >= AgreeThreshold).ToList();
            if (group.Count > best.Count)
                best = group;
        }
        var needed = voters.Count == 1 ? 1 : 2;
        return new Consensus(best, best.Count >= needed);
    }
}
