using Ara3D.IO.StepParser;
using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher;
using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary>A STEP file meshed the way StepPlatoMesher.MeshFile does it, with every step, edge and face timed:
/// parse, DecodeRaw, PrepareModel, the path's once-per-model tables, the edges, the faces, and layer 6's numbering
/// and assembly. The chordal bound is StepMesher's default (StepMeshingOptions.MaxDeviation in millimetres).</summary>
public sealed record TimedMesh(
    string File, string Path, bool Parallel, int Edges, int Faces,
    double ParseMs, double DecodeMs, double PrepareMs, double TablesMs, double EdgesMs, double FacesMs, double AssembleMs,
    IReadOnlyList<EdgeRow> EdgeRows, IReadOnlyList<FaceRow> FaceRows, MeshDigest Digest)
{
    public double TotalMs
        => ParseMs + DecodeMs + PrepareMs + TablesMs + EdgesMs + FacesMs + AssembleMs;

    public static double ChordalMillimetres
        => new StepMeshingOptions().MaxDeviation * 1000.0;

    public static TimedMesh Run(string file, Options options)
    {
        if (options.Warmup)
            Run(file, options.Path, options.Parallel);
        return Run(file, options.Path, options.Parallel);
    }

    public static TimedMesh Run(string file, string path, bool parallel)
    {
        Progress.Report("parse");
        var (model, parseMs) = Clock.Time(() => new StepModel(new StepDocument(new FilePath(file))));
        Progress.Report("decode");
        var (raw, decodeMs) = Clock.Time(() => model.DecodeRaw());
        Progress.Report("prepare");
        var (prepared, prepareMs) = Clock.Time(() => raw.PrepareModel());
        var m = prepared.Model;
        var criteria = StepPlatoMesher.Criteria(ChordalMillimetres);
        Progress.Report("tables");
        var meshPath = MeshPath.Of(path, m, criteria.Discretization);
        var edgesByFace = m.EdgesByFace();
        var surfaces = m.AdjacentSurfaces(edgesByFace);
        var (edges, edgesMs) = Clock.Time(() =>
            Rows.Build(m.Edges.Count, parallel, i => m.Time(i, surfaces[i], meshPath), new("edges", m.Edges.Count)));
        var d = new EdgeDiscretizations(edges.Select(e => e.Discretization).ToArray());
        var (faces, facesMs) = Clock.Time(() =>
            Rows.Build(m.Faces.Count, parallel, i => m.Time(i, edgesByFace[i], d, criteria, meshPath), new("faces", m.Faces.Count)));
        Progress.Report("assemble");
        var faceMeshes = faces.Select(f => f.Outcome.Mesh).ToArray();
        var (mesh, assembleMs) = Clock.Time(() => m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(d, faceMeshes)));
        Progress.Report("done");
        return new(file, path, parallel, m.Edges.Count, m.Faces.Count, parseMs, decodeMs, prepareMs, meshPath.TablesMs,
            edgesMs, facesMs, assembleMs, edges, faces, MeshDigest.Of(mesh));
    }
}
