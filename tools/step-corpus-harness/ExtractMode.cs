using System.Security.Cryptography;
using System.Text;
using Ara3D.IO.StepParser;
using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>extract file faces out.plato [--ring n] [--name Name] [--chordal x]</c>: the named faces of a STEP file's
/// prepared model (or of a library witness, <c>witness:Name[:x]</c>), with their neighbours <c>--ring</c> edges deep,
/// written as a Plato witness, <c>Witness_&lt;Name&gt;(_: BrepModel): BrepModel</c>, in a library block of its own
/// (TKT-439). A face that breaks the contract in the corpus becomes a value a law can quantify over. Before writing,
/// both models are meshed the host's way and each named face's mesh is compared (branch, element counts, a digest of
/// its positions and elements), so the file says whether the witness reproduces the face; a face whose mesh depends
/// on a neighbour left out (an edge's segment count reads the surfaces of every face citing it) needs a larger ring.</summary>
public static class ExtractMode
{
    public static int Run(string source, string faceList, string output, string[] rest, Options options)
    {
        var ring = 0;
        string? name = null;
        var chordal = TimedMesh.ChordalMillimetres;
        for (var i = 0; i < rest.Length; i++)
            if (rest[i] == "--ring" && i + 1 < rest.Length)
                ring = int.Parse(rest[++i]);
            else if (rest[i] == "--name" && i + 1 < rest.Length)
                name = rest[++i];
            else if (rest[i] == "--chordal" && i + 1 < rest.Length)
                chordal = double.Parse(rest[++i], System.Globalization.CultureInfo.InvariantCulture);
        var faces = faceList.Split(',').Select(int.Parse).ToArray();
        name ??= DefaultName(source, faces);

        var model = source.StartsWith("witness:") ? GalleryMode.Witness(source[8..]) : Prepared(source);
        var whole = TimedMesh.RunModel(source, model, options.Path, options.Parallel, 0, 0, 0, chordal);
        var sub = SubModel.Of(model, faces, ring);
        var part = TimedMesh.RunModel($"{name} (extracted)", sub.Model, options.Path, options.Parallel, 0, 0, 0, chordal);

        var rows = faces.Select(f =>
        {
            var k = sub.Faces.ToList().IndexOf(f);
            var before = whole.FaceRows[f];
            var after = part.FaceRows[k];
            var same = before.Path == after.Path && Digest(before.Outcome.Mesh) == Digest(after.Outcome.Mesh);
            return (Face: f, Index: k, Before: before, After: after, Same: same);
        }).ToArray();
        var lines = rows.Select(r =>
            $"face {r.Face} (witness face {r.Index}): {Describe(r.Before)} in the file, {Describe(r.After)} in the witness, " +
            (r.Same ? "the same mesh" : "a different mesh")).ToArray();

        var text = new StringBuilder();
        text.Append($"// Extracted by step-corpus-harness extract (TKT-439) on {DateTime.Now:yyyy-MM-dd} from {source},\n");
        text.Append($"// faces {faceList} of the prepared model with a ring of {ring}: {sub.Faces.Count} faces, {sub.Model.Edges.Count} edges.\n");
        text.Append($"// Witness face k is source face [{string.Join(", ", sub.Faces)}][k]. Meshed the host's way at chordal {chordal}:\n");
        foreach (var line in lines)
            text.Append("//   ").Append(line).Append('\n');
        text.Append("// One open shell in a shell-set body holds the faces; tolerances and unit are the source model's.\n");
        text.Append($"library CorpusWitness{name}\n{{\n");
        text.Append($"    Witness_{name}(_: BrepModel): BrepModel\n        => ");
        text.Append(PlatoLiteral.Write(sub.Model).Replace("\n", "\n        "));
        text.Append(";\n}\n");
        File.WriteAllText(output, text.ToString());

        foreach (var line in lines)
            Console.WriteLine(line);
        Console.WriteLine($"wrote Witness_{name} to {output}: {sub.Faces.Count} faces, {sub.Model.Edges.Count} edges, " +
                          $"{sub.Model.Geometry.Surfaces.Count} surfaces, {text.Length} characters");
        return Enumerable.All(rows, r => r.Same) ? 0 : 1;
    }

    static BrepModel Prepared(string file)
        => new StepModel(new StepDocument(new FilePath(file))).DecodeRaw().PrepareModel().Model;

    static string Describe(FaceRow r) => $"{r.Path}, {r.Triangles} triangles, {r.Quads} quads";

    /// <summary>A digest of a face mesh that does not depend on the model's numbering: its positions and its elements
    /// over local point indices.</summary>
    static string Digest(FaceMesh fm)
    {
        var text = new StringBuilder();
        foreach (var p in fm.Positions)
            text.Append(((double)p.X).ToString("R")).Append(',').Append(((double)p.Y).ToString("R")).Append(',').Append(((double)p.Z).ToString("R")).Append(';');
        foreach (var t in fm.Triangles)
            text.Append($"t{(int)t.A.Value},{(int)t.B.Value},{(int)t.C.Value};");
        foreach (var q in fm.Quads)
            text.Append($"q{(int)q.A.Value},{(int)q.B.Value},{(int)q.C.Value},{(int)q.D.Value};");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16];
    }

    /// <summary>The file's name and the faces as a Plato identifier: SHO/ODIN-W160.STEP, faces 12 and 30 give OdinW160Face12And30.</summary>
    static string DefaultName(string source, IReadOnlyList<int> faces)
    {
        var stem = Path.GetFileNameWithoutExtension(source.StartsWith("witness:") ? source[8..].Replace(':', '_') : source);
        var words = stem.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Concat(words) + "Face" + string.Join("And", faces);
    }
}
