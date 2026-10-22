using System.Reflection;
using System.Text.Json;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>gallery &lt;file.step | witness:Name&gt; &lt;out.glb&gt; [--faces a,b,c] [--ring n]</c>: one model meshed
/// the way the time mode meshes it, written as a GLB for the gallery page: every meshed face as triangles coloured by
/// surface kind (quads split), every discretized edge as a line, and the edges of every unmeshed face as red lines.
/// <c>witness:Name</c> meshes the library's <c>Witness_Name()</c> model instead of a file, and <c>witness:Name:0.3</c> the
/// overload taking that many Number parameters. <c>--faces</c> keeps only
/// the named faces and, with <c>--ring n</c>, the faces within n shared edges of them, for a close-up of a defect.
/// <c>--chordal x</c> replaces the host's chordal bound (in model units; a witness model is one unit across, so 0.01 is the
/// right order). Beside the GLB, <c>out.json</c> records the counts, the centre subtracted from the positions, the time per stage,
/// and every face that did not mesh with its branch and message; <c>warm</c> says whether <c>--warmup</c> ran first, and
/// <c>drawnMs</c> is the face-meshing time of the drawn faces alone; <c>edgeFailures</c> groups the edges that threw
/// by curve kind and message.</summary>
public static class GalleryMode
{
    static readonly Dictionary<string, (float, float, float)> Colours = new()
    {
        ["plane"] = (0.72f, 0.76f, 0.82f),
        ["cylinder"] = (0.92f, 0.41f, 0.20f),
        ["cone"] = (0.93f, 0.63f, 0.00f),
        ["sphere"] = (0.11f, 0.69f, 0.48f),
        ["torus"] = (0.91f, 0.48f, 0.64f),
        ["revolution"] = (0.00f, 0.51f, 0.00f),
        ["extrusion"] = (0.38f, 0.31f, 0.84f),
        ["bspline"] = (0.16f, 0.47f, 0.84f),
    };

    public static int Run(string source, string glb, string[] rest, Options options)
    {
        var faces = Array.Empty<int>();
        var ring = 0;
        var chordal = TimedMesh.ChordalMillimetres;
        for (var i = 0; i < rest.Length; i++)
            if (rest[i] == "--faces" && i + 1 < rest.Length)
                faces = rest[++i].Split(',').Select(int.Parse).ToArray();
            else if (rest[i] == "--ring" && i + 1 < rest.Length)
                ring = int.Parse(rest[++i]);
            else if (rest[i] == "--chordal" && i + 1 < rest.Length)
                chordal = double.Parse(rest[++i], System.Globalization.CultureInfo.InvariantCulture);
        var t = source.StartsWith("witness:")
            ? TimedMesh.RunModel(source, Witness(source[8..]), options.Path, options.Parallel, 0, 0, 0, chordal)
            : TimedMesh.Run(source, options, chordal);
        Write(t, glb, Chosen(t, faces, ring), options.Warmup);
        Console.WriteLine($"gallery {glb}: {t.Faces} faces, {t.FaceRows.Count(f => f.Triangles + f.Quads > 0)} meshed, {t.Digest}, {t.Check}, {t.Contract}");
        return 0;
    }

    /// <summary>The library's parameterless Witness_&lt;name&gt;() model, found in any of the generated static classes.</summary>
    static BrepModel Witness(string spec)
    {
        var parts = spec.Split(':');
        var name = parts[0];
        var args = Enumerable.Skip(parts, 1).Select(a => (object)(Number)double.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var types = args.Select(_ => typeof(Number)).ToArray();
        var method = typeof(BrepModel).Assembly.GetTypes()
            .Select(type => type.GetMethod($"Witness_{name}", BindingFlags.Public | BindingFlags.Static, types))
            .FirstOrDefault(m => m is not null && m.ReturnType == typeof(BrepModel))
            ?? throw new ArgumentException($"No Witness_{name}() with {args.Length} Number parameters returning BrepModel in Ara3D.StepBrep");
        return (BrepModel)method.Invoke(null, args)!;
    }

    /// <summary>The faces to draw: all of them, or the named ones grown by <paramref name="ring"/> shared edges.</summary>
    static HashSet<int> Chosen(TimedMesh t, int[] faces, int ring)
    {
        if (faces.Length == 0)
            return [.. Enumerable.Range(0, t.Faces)];
        var chosen = new HashSet<int>(faces);
        for (var r = 0; r < ring; r++)
        {
            var edges = chosen.SelectMany(f => t.EdgesByFace[f].Select(e => e.Value)).ToHashSet();
            for (var f = 0; f < t.Faces; f++)
                if (Enumerable.Any(t.EdgesByFace[f], e => edges.Contains(e.Value)))
                    chosen.Add(f);
        }
        return chosen;
    }

    static (double, double, double) P(Point3D p) => ((double)p.X, (double)p.Y, (double)p.Z);

    static void Write(TimedMesh t, string glb, HashSet<int> chosen, bool warm)
    {
        var points = new List<(double x, double y, double z)>();
        foreach (var f in chosen)
            points.AddRange(t.FaceRows[f].Outcome.Mesh.Positions.Select(P));
        foreach (var f in chosen)
            foreach (var e in t.EdgesByFace[f])
                points.AddRange(t.EdgeRows[e.Value].Discretization.Points.Select(P));
        var min = points.Count == 0 ? (0.0, 0.0, 0.0) : (points.Min(p => p.x), points.Min(p => p.y), points.Min(p => p.z));
        var max = points.Count == 0 ? (0.0, 0.0, 0.0) : (points.Max(p => p.x), points.Max(p => p.y), points.Max(p => p.z));
        var w = new GlbWriter { Centre = [(min.Item1 + max.Item1) / 2, (min.Item2 + max.Item2) / 2, (min.Item3 + max.Item3) / 2] };
        var drawnEdges = new HashSet<int>();
        foreach (var f in chosen.Order())
        {
            var row = t.FaceRows[f];
            var mesh = row.Outcome.Mesh;
            var colour = Colours.GetValueOrDefault(row.Surface, (0.5f, 0.5f, 0.5f));
            var pos = mesh.Positions;
            var lv = mesh.LocalVertex;
            Point3D At(VertexIndex v) => pos[(int)lv[(int)v.Value].Value];
            foreach (var tri in mesh.Triangles)
                w.Triangle(P(At(tri.A)), P(At(tri.B)), P(At(tri.C)), colour);
            foreach (var q in mesh.Quads)
            {
                w.Triangle(P(At(q.A)), P(At(q.B)), P(At(q.C)), colour);
                w.Triangle(P(At(q.A)), P(At(q.C)), P(At(q.D)), colour);
            }
            var failed = row.Triangles + row.Quads == 0;
            foreach (var e in t.EdgesByFace[f])
            {
                var polyline = t.EdgeRows[e.Value].Discretization.Points.Select(P).ToArray();
                if (failed)
                    w.Segment(polyline, true);
                if (drawnEdges.Add(e.Value))
                    w.Segment(polyline, false);
            }
        }
        w.Write(glb);
        var unmeshed = chosen.Order().Select(f => t.FaceRows[f]).Where(r => r.Triangles + r.Quads == 0).Select(r => new
        {
            face = r.Index, surface = r.Surface, loops = r.Loops, edges = r.Edges, branch = r.Path, ms = Math.Round(r.Ms, 2),
            exception = r.Exception, message = r.Message == "" ? string.Join(" | ", r.Outcome.Diagnostics.Select(d => (string)d.Message)) : r.Message,
            edgesThrew = t.EdgesByFace[r.Index].Count(e => t.EdgeRows[e.Value].Exception != ""),
        }).ToArray();
        var byKind = chosen.Select(f => t.FaceRows[f]).GroupBy(r => r.Surface).OrderByDescending(g => g.Count())
            .Select(g => new { surface = g.Key, faces = g.Count(), meshed = g.Count(r => r.Triangles + r.Quads > 0), ms = Math.Round(g.Sum(r => r.Ms), 1) }).ToArray();
        var branches = chosen.Select(f => t.FaceRows[f]).GroupBy(r => r.Path).ToDictionary(g => g.Key, g => g.Count());
        var edgeFailures = t.EdgeRows.Where(e => e.Exception != "")
            .GroupBy(e => (e.Curve, e.Exception, Message: e.Message.Length > 160 ? e.Message[..160] : e.Message))
            .OrderByDescending(g => g.Count()).Take(6)
            .Select(g => new { curve = g.Key.Curve, exception = g.Key.Exception, message = g.Key.Message, edges = g.Count() }).ToArray();
        object Brief(FaceRow r) => new { face = r.Index, surface = r.Surface, loops = r.Loops, edges = r.Edges, branch = r.Path, triangles = r.Triangles, quads = r.Quads, ms = Math.Round(r.Ms, 2) };
        var slowest = chosen.Select(f => t.FaceRows[f]).OrderByDescending(r => r.Ms).Take(5).Select(Brief).ToArray();
        var mostEdges = chosen.Select(f => t.FaceRows[f]).OrderByDescending(r => r.Edges).Take(5).Select(Brief).ToArray();
        File.WriteAllText(Path.ChangeExtension(glb, ".json"), JsonSerializer.Serialize(new
        {
            source = t.File, faces = t.Faces, edges = t.Edges, drawnFaces = chosen.Count,
            meshedFaces = chosen.Count(f => t.FaceRows[f].Triangles + t.FaceRows[f].Quads > 0),
            edgesThrew = t.EdgeRows.Count(e => e.Exception != ""),
            triangles = w.Triangles, edgeSegments = w.EdgeSegments, failedSegments = w.FailedSegments,
            centre = w.Centre, size = new[] { max.Item1 - min.Item1, max.Item2 - min.Item2, max.Item3 - min.Item3 },
            warm, drawnMs = R(chosen.Sum(f => t.FaceRows[f].Ms)),
            ms = new { parse = R(t.ParseMs), decode = R(t.DecodeMs), prepare = R(t.PrepareMs), tables = R(t.TablesMs), edges = R(t.EdgesMs), faces = R(t.FacesMs), assemble = R(t.AssembleMs), total = R(t.TotalMs) },
            digest = t.Digest, check = t.Check,
            contract = new { verdict = t.Contract.ToString(), bound = t.Contract.Bound, failing = Enumerable.Take(t.Contract.Failing, 20) },
            branches, byKind, edgeFailures, slowest, mostEdges, unmeshed,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    static double R(double ms) => Math.Round(ms, 1);
}
