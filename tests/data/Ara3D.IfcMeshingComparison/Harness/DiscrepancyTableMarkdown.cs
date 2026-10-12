using System.Text;
using Ara3D.Geometry;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>Writes a discrepancy table as Markdown: a verdict count, then one row per product.</summary>
public static class DiscrepancyTableMarkdown
{
    public static string Format(string fileName, IReadOnlyList<string> mesherNames, IReadOnlyList<ProductDiscrepancy> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Discrepancies: {fileName}");
        sb.AppendLine();
        sb.AppendLine($"{rows.Count} products, sorted by Ara3D.Ifc.Mesher's distance from the majority (1 minus its shape score), largest first. Majority and shape score are defined in `DiscrepancyTableConsensus.cs`. A `!` after a bounds cell marks bounds more than 5 % from the first majority member's.");
        sb.AppendLine();
        foreach (var g in rows.GroupBy(r => r.Verdict).OrderByDescending(g => g.Count()))
            sb.AppendLine($"- {g.Key}: {g.Count()}");
        sb.AppendLine();
        sb.AppendLine("| Id | Product | Geometry | Verdict | Dist | " + string.Join(" | ", mesherNames.Select(n => $"{n} tris / shape / volume / bounds")) + " |");
        sb.AppendLine("|---:|---|---|---|---:|" + string.Concat(mesherNames.Select(_ => "---|")));
        foreach (var r in rows)
            sb.AppendLine($"| {r.ExpressId} | {r.ProductKind} | {r.GeometryKind} | {r.Verdict} | {r.Distance:F2} | " +
                          string.Join(" | ", mesherNames.Select(n => Cell(r, n))) + " |");
        return sb.ToString();
    }

    static string Cell(ProductDiscrepancy r, string mesher)
    {
        if (!r.Meshers.TryGetValue(mesher, out var v))
            return "–";
        var flag = v.BoundsClose ? "" : "!";
        return $"{v.Triangles} / {v.ShapeScore:F2} / {v.Volume:F3} / {Box(v.Bounds)}{flag}";
    }

    static string Box(Bounds3D b)
        => $"({b.Min.X:F2},{b.Min.Y:F2},{b.Min.Z:F2})-({b.Max.X:F2},{b.Max.Y:F2},{b.Max.Z:F2})";
}
