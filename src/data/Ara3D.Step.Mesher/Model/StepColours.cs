using Ara3D.Geometry;
using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>
/// The surface colour of one styled_item, along the chain the CAx-IF styling practice writes:
/// styled_item → presentation_style_assignment → surface_style_usage → surface_side_style →
/// surface_style_fill_area → fill_area_style → fill_area_style_colour → colour_rgb or draughting_pre_defined_colour,
/// or a surface_style_rendering's surface_colour, whose properties may add a surface_style_transparent.
/// </summary>
public static class StepColours
{
    /// <summary>The first surface colour the styled item gives, or null when it gives none.</summary>
    public static Color? SurfaceColour(this StepEntity styledItem)
        => First(styledItem.Refs(StyledItem.Instance.Styles), assignment
            => First(assignment.Is<IPresentationStyleAssignment>() ? assignment.Refs(PresentationStyleAssignment.Instance.Styles) : null, OfUsage));

    /// <summary>A colour_rgb, or a draughting_pre_defined_colour by its ISO 10303-46 name.</summary>
    public static Color? Colour(this StepEntity colour)
        => colour.Is<IColourRgb>()
            ? new Color((float)(colour.Number(ColourRgb.Instance.Red) ?? 0), (float)(colour.Number(ColourRgb.Instance.Green) ?? 0),
                (float)(colour.Number(ColourRgb.Instance.Blue) ?? 0), 1f)
            : colour.Is<IDraughtingPreDefinedColour>() ? PreDefined(colour.Text(DraughtingPreDefinedColour.Instance.Name)) : null;

    static Color? OfUsage(StepEntity usage)
        => usage.Is<ISurfaceStyleUsage>() && usage.Ref(SurfaceStyleUsage.Instance.Style) is { } side && side.Is<ISurfaceSideStyle>()
            ? First(side.Refs(SurfaceSideStyle.Instance.Styles), OfElement)
            : null;

    static Color? OfElement(StepEntity element)
    {
        if (element.Is<ISurfaceStyleFillArea>())
            return element.Ref(SurfaceStyleFillArea.Instance.FillArea) is { } area
                ? First(area.Refs(FillAreaStyle.Instance.FillStyles), fill
                    => fill.Is<IFillAreaStyleColour>() ? fill.Ref(FillAreaStyleColour.Instance.FillColour)?.Colour() : null)
                : null;
        if (!element.Is<ISurfaceStyleRendering>() || element.Ref(SurfaceStyleRendering.Instance.SurfaceColour)?.Colour() is not { } c)
            return null;
        var transparency = element.Is<ISurfaceStyleRenderingWithProperties>()
            ? First(element.Refs(SurfaceStyleRenderingWithProperties.Instance.Properties), p
                => p.Is<ISurfaceStyleTransparent>() ? p.Number(SurfaceStyleTransparent.Instance.Transparency) : null)
            : null;
        return new Color(c.R, c.G, c.B, (float)(1 - (transparency ?? 0)));
    }

    static Color? PreDefined(string? name)
        => name?.ToLowerInvariant() switch
        {
            "red" => new Color(1f, 0f, 0f, 1f),
            "green" => new Color(0f, 1f, 0f, 1f),
            "blue" => new Color(0f, 0f, 1f, 1f),
            "yellow" => new Color(1f, 1f, 0f, 1f),
            "magenta" => new Color(1f, 0f, 1f, 1f),
            "cyan" => new Color(0f, 1f, 1f, 1f),
            "black" => new Color(0f, 0f, 0f, 1f),
            "white" => new Color(1f, 1f, 1f, 1f),
            _ => null,
        };

    static T? First<T>(IReadOnlyList<StepEntity>? entities, Func<StepEntity, T?> pick) where T : struct
    {
        foreach (var e in entities ?? [])
            if (pick(e) is { } found)
                return found;
        return null;
    }
}
