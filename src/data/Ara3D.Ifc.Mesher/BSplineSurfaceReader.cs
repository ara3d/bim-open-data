using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.Ifc.Mesher;

/// <summary>Reads a B-spline surface entity's control net, weights, degrees and knots into a
/// <see cref="BSplineSurface"/>.</summary>
internal static class BSplineSurfaceReader
{
    public static BSplineSurface Read(MeshingContext ctx, IfcEntity surface)
    {
        var ctrl = ReadControlGrid(ctx, surface);
        var pu = (int)MeshHelpers.ReadNumber(surface, IfcBSplineSurface.Instance.UDegree);
        var pv = (int)MeshHelpers.ReadNumber(surface, IfcBSplineSurface.Instance.VDegree);
        var uKnots = BSplineKnots.Expand(
            MeshHelpers.ReadNumbers(surface, IfcBSplineSurfaceWithKnots.Instance.UKnots),
            MeshHelpers.ReadNumbers(surface, IfcBSplineSurfaceWithKnots.Instance.UMultiplicities));
        var vKnots = BSplineKnots.Expand(
            MeshHelpers.ReadNumbers(surface, IfcBSplineSurfaceWithKnots.Instance.VKnots),
            MeshHelpers.ReadNumbers(surface, IfcBSplineSurfaceWithKnots.Instance.VMultiplicities));
        return new BSplineSurface(ctrl, ReadWeightGrid(surface, ctrl), uKnots, vKnots, pu, pv);
    }

    static Vector3[][] ReadControlGrid(MeshingContext ctx, IfcEntity surface)
    {
        var token = surface.GetValue(IfcBSplineSurface.Instance.ControlPointsList.Index);
        if (!token.IsList)
            return [];
        var rows = new List<Vector3[]>();
        foreach (var rowTok in token.AsList(surface.Document))
        {
            if (!rowTok.IsList)
                continue;
            var row = new List<Vector3>();
            foreach (var idTok in rowTok.AsList(surface.Document))
                if (idTok.IsId)
                    row.Add(Placements.ReadPoint3D(ctx, ctx.GetEntity(idTok.AsId())));
            if (row.Count > 0)
                rows.Add(row.ToArray());
        }
        return rows.ToArray();
    }

    static float[][] ReadWeightGrid(IfcEntity surface, Vector3[][] ctrl)
    {
        var w = ctrl.Select(row => Enumerable.Repeat(1f, row.Length).ToArray()).ToArray();
        if (surface.GetEntityName() != "IFCRATIONALBSPLINESURFACEWITHKNOTS")
            return w;
        var token = surface.GetValue(IfcRationalBSplineSurfaceWithKnots.Instance.WeightsData.Index);
        if (!token.IsList)
            return w;
        var i = 0;
        foreach (var rowTok in token.AsList(surface.Document))
        {
            if (!rowTok.IsList || i >= w.Length)
                continue;
            var j = 0;
            foreach (var wTok in rowTok.AsList(surface.Document))
            {
                if (wTok.IsNumber && j < w[i].Length)
                    w[i][j] = (float)wTok.AsNumber();
                j++;
            }
            i++;
        }
        return w;
    }
}
