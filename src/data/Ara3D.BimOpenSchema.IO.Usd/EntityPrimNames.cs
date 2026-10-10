namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// The prim name of every BOS entity, computed before anything is written so a relationship
/// can point at an entity that comes later in the file.
/// </summary>
internal static class EntityPrimNames
{
    /// <summary>One name per entity, by entity index: E_{GlobalId} made an identifier, or
    /// E{entity index} when the GlobalId is missing or an earlier entity already has that name
    /// (duplicate GlobalIds occur in federated models). The two forms cannot collide: the
    /// second character is "_" in one and a digit in the other.</summary>
    public static string[] ForEntities(IBimData data)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var names = new string[data.Entities.Length];
        for (var e = 0; e < names.Length; e++)
        {
            var globalId = BosValues.NonEmptyString(data, data.Entities[e].GlobalId);
            var fromGlobalId = globalId is null ? null : UsdNames.ToIdentifier(UsdNames.ElementPrefix + "_" + globalId);
            names[e] = fromGlobalId is not null && used.Add(fromGlobalId)
                ? fromGlobalId
                : UsdNames.ElementPrefix + e;
        }
        return names;
    }
}
