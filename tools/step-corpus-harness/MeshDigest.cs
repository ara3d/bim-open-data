using System.Security.Cryptography;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>Counts and a SHA-256 of an assembled model mesh (positions' bits, then triangle and quad indices), so two
/// runs, sequential and parallel or host and hoisted, can be shown to give the same mesh.</summary>
public sealed record MeshDigest(int Vertices, int Triangles, int Quads, string Hash)
{
    public static MeshDigest Of(ModelMesh mesh)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[sizeof(double) * 4];
        void Add(int k, double v) => BitConverter.TryWriteBytes(buffer.AsSpan(k * sizeof(double)), v);
        foreach (var p in mesh.Positions)
        {
            Add(0, p.X);
            Add(1, p.Y);
            Add(2, p.Z);
            sha.AppendData(buffer, 0, 3 * sizeof(double));
        }
        foreach (var t in mesh.Triangles)
        {
            Add(0, t.A.Value);
            Add(1, t.B.Value);
            Add(2, t.C.Value);
            sha.AppendData(buffer, 0, 3 * sizeof(double));
        }
        foreach (var q in mesh.Quads)
        {
            Add(0, q.A.Value);
            Add(1, q.B.Value);
            Add(2, q.C.Value);
            Add(3, q.D.Value);
            sha.AppendData(buffer, 0, 4 * sizeof(double));
        }
        return new(mesh.Positions.Count, mesh.Triangles.Count, mesh.Quads.Count, Convert.ToHexString(sha.GetHashAndReset())[..16]);
    }
}
