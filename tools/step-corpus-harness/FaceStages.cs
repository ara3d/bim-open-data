namespace Ara3D.Step.CorpusHarness;

/// <summary>The stages of layer 5's MeshRegionOnce as restated in <see cref="FaceTiming"/>. Check is layer 8's
/// IsDiscretizedFace; region builds and orients the UV loops; chart is the chart and its tolerance; simple is the
/// segment-collision test; grid the quad-grid attempt; triangulate the CDT; refine the insertion refinement; lift the
/// 3D lift; advisory the refinement requests and diagnostics.</summary>
public enum FaceStage { Check, Region, Chart, Simple, Grid, Triangulate, Refine, Lift, Advisory }

/// <summary>Milliseconds per <see cref="FaceStage"/>; a stage not reached is 0.</summary>
public sealed record FaceStages(IReadOnlyList<double> Ms)
{
    public static readonly FaceStage[] All = Enum.GetValues<FaceStage>();

    public static readonly string Header = string.Join(',', All.Select(s => $"{s.ToString().ToLowerInvariant()}_ms"));

    public double this[FaceStage s]
        => Ms[(int)s];

    public string Csv()
        => string.Join(',', Ms.Select(Format.Ms));
}
