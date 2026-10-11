using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D.Collections;

namespace Ara3D.BimOpenSchema;

public static class BimObjectModelExtensions
{
    /// <summary>The distinct (level name, elevation) pairs, ordered by elevation; a null name or
    /// elevation is absent (no level, or a level with no elevation) and sorts first.</summary>
    public static IReadOnlyList<(string? LevelName, float? Elevation)> GetDistinctLevels(this BimObjectModel self)
    {
        var tmp = self.Entities.Select(e => (e.LevelName, e.Elevation)).Distinct().OrderBy(pair => pair.Elevation).ToList();

        if (tmp.Count == 0) return tmp;

        var prev = tmp[0];
        var r = new List<(string? LevelName, float? Elevation)>() { prev };
        for (var i = 1; i < tmp.Count; i++)
        {
            var cur = tmp[i];
            // TODO: this is a hack.
            const double levelDiff = 0.001;
            if (cur.LevelName != prev.LevelName || !SameElevation(cur.Elevation, prev.Elevation, levelDiff))
            {
                r.Add(cur);
                prev = cur;
            }
        }

        return r;
    }

    /// <summary>True when both elevations are absent, or both present and within tolerance.</summary>
    public static bool SameElevation(float? a, float? b, double tolerance)
        => a is { } x && b is { } y ? Math.Abs(x - y) <= tolerance : a.HasValue == b.HasValue;
}