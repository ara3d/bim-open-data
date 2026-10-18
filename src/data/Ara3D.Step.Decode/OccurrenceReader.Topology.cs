using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

public sealed partial class OccurrenceReader
{
    /// <summary>manifold_solid_brep, faceted_brep, brep_with_voids, shell_based_surface_model. An oriented shell
    /// becomes its underlying shell; its .F. is recorded as an OrientedShell fold on each of its faces.</summary>
    bool Body(StepEntity body)
        => _bodies.Reach(body.Id, () =>
        {
            if (body.Is<ST.IShellBasedSurfaceModel>())
            {
                var shells = (body.Refs(ST.ShellBasedSurfaceModel.Instance.SbsmBoundary) ?? []).Select(NeedShell).ToArray();
                return () => BrepBody.ShellSet(shells.Select(ShellIndex).ToArray());
            }
            var outer = NeedShell(body.Ref(ST.ManifoldSolidBrep.Instance.Outer)!.Value);
            if (!body.Is<ST.IBrepWithVoids>())
                return () => BrepBody.ManifoldSolid(ShellIndex(outer));
            var voids = (body.Refs(ST.BrepWithVoids.Instance.Voids) ?? []).Select(NeedShell).ToArray();
            return () => BrepBody.SolidWithVoids(ShellIndex(outer), voids.Select(ShellIndex).ToArray());
        }, Failure);

    ShellIndex ShellIndex(int id)
        => new(_shells.IndexOf(id));

    int NeedShell(StepEntity shell)
    {
        var (element, flip) = shell.Is<ST.IOrientedClosedShell>()
            ? (shell.Ref(ST.OrientedClosedShell.Instance.ClosedShellElement)!.Value, shell.Boolean(ST.OrientedClosedShell.Instance.Orientation) == false)
            : shell.Is<ST.IOrientedOpenShell>()
                ? (shell.Ref(ST.OrientedOpenShell.Instance.OpenShellElement)!.Value, shell.Boolean(ST.OrientedOpenShell.Instance.Orientation) == false)
                : (shell, false);
        Need(Shell(element, flip ? shell.Id : -1), element.Id);
        return element.Id;
    }

    /// <summary>closed_shell or open_shell. A face the shell cites that cannot be decoded is left out of it.</summary>
    bool Shell(StepEntity shell, int flippedBy)
        => _shells.Reach(shell.Id, () =>
        {
            var faces = new List<int>();
            foreach (var cited in shell.Refs(ST.ConnectedFaceSet.Instance.CfsFaces) ?? [])
                if (CitedFace(cited, flippedBy) is { } face)
                    faces.Add(face);
            var closed = shell.Is<ST.IClosedShell>();
            return () => new BrepShell(faces.Select(f => new BrepFaceIndex(_faces.IndexOf(f))).ToArray(), closed);
        }, Failure);

    /// <summary>The face a shell cites, through an oriented_face (recording its .F. as an OrientedFace fold), or
    /// null when it cannot be decoded. A face cited a second time in the occurrence reuses its first row and folds.</summary>
    int? CitedFace(StepEntity cited, int flippedBy)
    {
        var face = cited;
        var flips = new List<(FoldCause, int)>();
        while (face.Is<ST.IOrientedFace>())
        {
            if (face.Boolean(ST.OrientedFace.Instance.Orientation) == false)
                flips.Add((FoldCause.OrientedFace(), face.Id));
            face = face.Ref(ST.OrientedFace.Instance.FaceElement)!.Value;
        }
        if (flippedBy >= 0)
            flips.Add((FoldCause.OrientedShell(), flippedBy));
        if (!Face(face))
            return null;
        if (!_citedFaces.Add(face.Id))
        {
            _findings.Once(StepDiagnosticCode.FaceCitedTwice(), face.Id);
            return face.Id;
        }
        foreach (var (cause, via) in flips)
            _folds.Add((face.Id, cause, via));
        return face.Id;
    }

    /// <summary>advanced_face or face_surface: the surface, the sense and the bounds as written. The outer-bound
    /// flag is kept when the face has at most one face_outer_bound and cleared on all its bounds otherwise.</summary>
    bool Face(StepEntity face)
        => _faces.Reach(face.Id, () =>
        {
            if (!face.Is<ST.IFaceSurface>())
                throw Unsupported(face);
            var surface = face.Ref(ST.FaceSurface.Instance.FaceGeometry)!.Value;
            Need(Surface(surface), surface.Id);
            var sameSense = face.Boolean(ST.FaceSurface.Instance.SameSense) ?? true;
            var bounds = new List<(int Loop, bool Orientation, bool Outer)>();
            foreach (var bound in face.Refs(ST.FaceSurface.Instance.Bounds) ?? [])
            {
                var loop = bound.Ref(ST.FaceBound.Instance.Bound)!.Value;
                Need(Loop(loop), loop.Id);
                bounds.Add((loop.Id, bound.Boolean(ST.FaceBound.Instance.Orientation) ?? true, bound.Is<ST.IFaceOuterBound>()));
            }
            var keepOuter = bounds.Count(b => b.Outer) <= 1;
            if (!keepOuter)
                _findings.Once(StepDiagnosticCode.TwoOuterBounds(), face.Id);
            return () => new BrepFace(new SurfaceIndex(_surfaces.IndexOf(surface.Id)), sameSense,
                bounds.Select(b => new FaceBound(new BrepLoopIndex(_loops.IndexOf(b.Loop)), b.Orientation, keepOuter && b.Outer)).ToArray());
        }, Failure);

    /// <summary>edge_loop (oriented_edges, nested ones composed), vertex_loop, poly_loop.</summary>
    bool Loop(StepEntity loop)
        => _loops.Reach(loop.Id, () =>
        {
            if (loop.Is<ST.IEdgeLoop>())
            {
                var coedges = (loop.Refs(ST.EdgeLoop.Instance.EdgeList) ?? []).Select(Coedge).ToArray();
                return () => BrepLoop.EdgeLoop(coedges.Select(c =>
                    new Coedge(new BrepEdgeIndex(_edges.IndexOf(c.Edge)), c.Forward ? EdgeOrientation.Forward() : EdgeOrientation.Reversed())).ToArray());
            }
            if (loop.Is<ST.IVertexLoop>())
            {
                var vertex = loop.Ref(ST.VertexLoop.Instance.LoopVertex)!.Value;
                Need(Vertex(vertex), vertex.Id);
                return () => BrepLoop.VertexLoop(new BrepVertexIndex(_vertices.IndexOf(vertex.Id)));
            }
            if (loop.Is<ST.IPolyLoop>())
            {
                var points = (loop.Refs(ST.PolyLoop.Instance.Polygon) ?? []).Select(p => P(p.PointOf())).ToArray();
                return () => BrepLoop.PolyLoop(points);
            }
            throw Unsupported(loop);
        }, Failure);

    (int Edge, bool Forward) Coedge(StepEntity cited)
    {
        var forward = true;
        var edge = cited;
        var depth = 0;
        while (edge.Is<ST.IOrientedEdge>())
        {
            if (depth++ == 1)
                _findings.Once(StepDiagnosticCode.NestedOrientedEdge(), cited.Id);
            forward = forward == (edge.Boolean(ST.OrientedEdge.Instance.Orientation) ?? true);
            edge = edge.Ref(ST.OrientedEdge.Instance.EdgeElement)!.Value;
        }
        Need(Edge(edge), edge.Id);
        return (edge.Id, forward);
    }

    /// <summary>edge_curve: its two vertices, its curve, and same_sense as written.</summary>
    bool Edge(StepEntity edge)
        => _edges.Reach(edge.Id, () =>
        {
            if (!edge.Is<ST.IEdgeCurve>())
                throw Unsupported(edge);
            var start = edge.Ref(ST.EdgeCurve.Instance.EdgeStart)!.Value;
            var end = edge.Ref(ST.EdgeCurve.Instance.EdgeEnd)!.Value;
            var curve = edge.Ref(ST.EdgeCurve.Instance.EdgeGeometry)!.Value;
            Need(Vertex(start), start.Id);
            Need(Vertex(end), end.Id);
            Need(Curve(curve), curve.Id);
            var sameSense = edge.Boolean(ST.EdgeCurve.Instance.SameSense) ?? true;
            return () => new BrepEdge(new BrepVertexIndex(_vertices.IndexOf(start.Id)), new BrepVertexIndex(_vertices.IndexOf(end.Id)),
                new CurveIndex(_curves.IndexOf(curve.Id)), sameSense);
        }, Failure);

    /// <summary>vertex_point with a cartesian_point.</summary>
    bool Vertex(StepEntity vertex)
        => _vertices.Reach(vertex.Id, () =>
        {
            if (!vertex.Is<ST.IVertexPoint>())
                throw Unsupported(vertex);
            var point = new BrepVertex(P(vertex.Ref(ST.VertexPoint.Instance.VertexGeometry)!.Value.PointOf()));
            return () => point;
        }, Failure);
}
