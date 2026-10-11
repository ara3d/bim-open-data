using System;
using System.Linq;

namespace Ara3D.BimOpenSchema.Harmonizer;

/// <summary>
/// Identifies which generator produced a BimData set.
/// Harmonization rules (naming, units) differ per source.
/// </summary>
public enum SourceKind
{
    Unknown,
    Revit,
    Ifc,
}

public static class SourceDetector
{
    /// <summary>
    /// Determines which generator produced the data. Prefers the manifest;
    /// falls back to sniffing descriptor prefixes and category entity names,
    /// since files written before the manifest was persisted have none.
    /// </summary>
    public static SourceKind Detect(IBimData data)
    {
        var app = data.Manifest?.GeneratorApplication ?? "";
        if (app.Contains("Revit", StringComparison.OrdinalIgnoreCase))
            return SourceKind.Revit;
        if (app.Contains("IFC", StringComparison.OrdinalIgnoreCase))
            return SourceKind.Ifc;

        foreach (var d in data.Descriptors)
        {
            if (data.Get(d.Name)?.StartsWith("Rvt:", StringComparison.Ordinal) == true)
                return SourceKind.Revit;
        }

        // Category entities converted from IFC are named after the IFC class (e.g. IFCWALL).
        var categoryIndices = data.Entities
            .Where(e => e.Category >= 0)
            .Select(e => (int)e.Category)
            .Distinct();
        foreach (var ci in categoryIndices)
        {
            if (data.EntityName((EntityIndex)ci)?.StartsWith("IFC", StringComparison.Ordinal) == true)
                return SourceKind.Ifc;
        }

        return SourceKind.Unknown;
    }
}
