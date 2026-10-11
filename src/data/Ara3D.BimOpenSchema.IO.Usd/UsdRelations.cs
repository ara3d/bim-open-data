namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes the BOS relations table as custom relationships on the source entity's prim
/// (EntityA): one relationship per relation type, named bim:{type} with a lower-case first
/// letter (bim:containedIn, bim:hostedBy), targeting the prims of every EntityB. A wall in a
/// storey gets <c>custom rel bim:containedIn = [&lt;/Model/E_...&gt;]</c>.
/// </summary>
internal sealed class UsdRelations
{
    // Relationship name by RelationType value; null for a value the enum does not define.
    private static readonly string?[] Names = BuildNames();

    private readonly BosScene _scene;
    private readonly string[] _primNames;
    private readonly List<(int Type, int Target)> _scratch = new();

    public UsdRelations(BosScene scene, string[] primNames)
    {
        _scene = scene;
        _primNames = primNames;
        LeftOut = scene.Data.Relations.Count(r => !scene.IsEntity((int)r.EntityA));
    }

    /// <summary>Relationship targets written. A relation listed twice is one target.</summary>
    public int Targets { get; private set; }

    /// <summary>Relations not written because the source or target entity is missing or out of
    /// range, or the relation type is not one BOS defines.</summary>
    public int LeftOut { get; private set; }

    public static string? NameOf(RelationType type)
        => (uint)type < (uint)Names.Length ? Names[(int)type] : null;

    /// <summary>Writes the relationships whose source is <paramref name="entity"/> into the open prim.</summary>
    public void Write(UsdaWriter w, int entity)
    {
        _scratch.Clear();
        foreach (var r in _scene.RelationsFrom(entity))
        {
            var relation = _scene.Data.Relations[r];
            if (NameOf(relation.RelationType) is null || !_scene.IsEntity((int)relation.EntityB))
                LeftOut++;
            else
                _scratch.Add(((int)relation.RelationType, (int)relation.EntityB));
        }
        _scratch.Sort();
        RemoveAdjacentDuplicates(_scratch);

        for (var i = 0; i < _scratch.Count; i++)
        {
            var (type, target) = _scratch[i];
            if (i == 0 || _scratch[i - 1].Type != type)
                w.Line().Text("custom rel ").Text(NameOf((RelationType)type)!).Text(" = [");
            else
                w.Text(", ");
            w.PathRef(UsdNames.EntityPathPrefix, _primNames[target]);
            Targets++;
            if (i + 1 == _scratch.Count || _scratch[i + 1].Type != type)
                w.Text(']').End();
        }
    }

    // USD rejects a relationship that lists one target twice.
    private static void RemoveAdjacentDuplicates(List<(int Type, int Target)> sorted)
    {
        var kept = 0;
        for (var i = 0; i < sorted.Count; i++)
            if (kept == 0 || sorted[kept - 1] != sorted[i])
                sorted[kept++] = sorted[i];
        sorted.RemoveRange(kept, sorted.Count - kept);
    }

    private static string?[] BuildNames()
    {
        var values = Enum.GetValues<RelationType>();
        var names = new string?[values.Max(v => (int)v) + 1];
        foreach (var v in values)
        {
            var text = v.ToString();
            names[(int)v] = UsdNames.Namespace + char.ToLowerInvariant(text[0]) + text[1..];
        }
        return names;
    }
}
