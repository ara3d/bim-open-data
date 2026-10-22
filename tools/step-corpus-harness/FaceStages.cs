namespace Ara3D.Step.CorpusHarness;

/// <summary>The two timed parts of a face's outcome in <see cref="FaceTiming"/>: check is the host's discretized-face
/// test (layer 8's IsDiscretizedFace), mesh is layer 5's MeshFace, called whole. The stages inside MeshFace are not
/// restated, so they have no clocks of their own here.</summary>
public enum FaceStage { Check, Mesh }

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
