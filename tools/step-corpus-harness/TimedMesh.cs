using Ara3D.IO.StepParser;
using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher;
using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary>A STEP file meshed the way StepPlatoMesher.MeshFile does it, with every step, edge and face timed:
/// parse, DecodeRaw, PrepareModel, the host's once-per-model tables, the edges, the faces, and layer 6's numbering
/// and assembly. The chordal bound is StepMesher's default (StepMeshingOptions.MaxDeviation in millimetres).</summary>
public sealed record TimedMesh(
    string File, string Path, bool Parallel, int Edges, int Faces,
    double ParseMs, double DecodeMs, double PrepareMs, double TablesMs, double EdgesMs, double FacesMs, double AssembleMs,
    IReadOnlyList<EdgeRow> EdgeRows, IReadOnlyList<FaceRow> FaceRows, MeshDigest Digest, MeshCheck Check,
    IReadOnlyList<BrepEdgeIndex>[] EdgesByFace)
{
    public double TotalMs
        => ParseMs + DecodeMs + PrepareMs + TablesMs + EdgesMs + FacesMs + AssembleMs;

    public static double ChordalMillimetres
        => new StepMeshingOptions().MaxDeviation * 1000.0;

    public static TimedMesh Run(string file, Options options, double? chordal = null)
    {
        if (options.Warmup)
            Run(file, options.Path, options.Parallel, chordal);
        return Run(file, options.Path, options.Parallel, chordal);
    }

    public static TimedMesh Run(string file, string path, bool parallel, double? chordal = null)
    {
        Progress.Report("parse");
        var (model, parseMs) = Clock.Time(() => new StepModel(new StepDocument(new FilePath(file))));
        Progress.Report("decode");
        var (raw, decodeMs) = Clock.Time(() => model.DecodeRaw());
        Progress.Report("prepare");
        var (prepared, prepareMs) = Clock.Time(() => raw.PrepareModel());
        return RunModel(file, prepared.Model, path, parallel, parseMs, decodeMs, prepareMs, chordal ?? ChordalMillimetres);
    }

    /// <summary>The stages after PrepareModel on a prepared model: the host's tables, the edges, the faces and the
    /// assembly, under the chordal bound <paramref name="chordal"/> in model units. The gallery mode starts here with one of
    /// the library's witness models.</summary>
    public static TimedMesh RunModel(string file, BrepModel m, string path, bool parallel, double parseMs, double decodeMs, double prepareMs, double chordal)
    {
        var criteria = StepPlatoMesher.Criteria(chordal);
        Progress.Report("tables");
        var meshPath = MeshPath.Of(path, m);
        var edgesByFace = m.EdgesByFace();
        var surfaces = m.AdjacentSurfaces(edgesByFace);
        var (edges, edgesMs) = Clock.Time(() =>
            Rows.Build(m.Edges.Count, parallel, i => m.Time(i, surfaces[i], criteria.Discretization, meshPath), new("edges", m.Edges.Count)));
        var d = new EdgeDiscretizations(edges.Select(e => e.Discretization).ToArray());
        var (faces, facesMs) = Clock.Time(() =>
            Rows.Build(m.Faces.Count, parallel, i => m.Time(i, edgesByFace[i], d, criteria, meshPath), new("faces", m.Faces.Count)));
        Progress.Report("assemble");
        var faceMeshes = faces.Select(f => f.Outcome.Mesh).ToArray();
        var (mesh, assembleMs) = Clock.Time(() => m.AssembleModelMesh(faceMeshes, m.NumberMeshVertices(d, faceMeshes)));
        Progress.Report("done");
        return new(file, path, parallel, m.Edges.Count, m.Faces.Count, parseMs, decodeMs, prepareMs, meshPath.TablesMs,
            edgesMs, facesMs, assembleMs, edges, faces, MeshDigest.Of(mesh), MeshCheck.Of(mesh), edgesByFace);
    }
}
