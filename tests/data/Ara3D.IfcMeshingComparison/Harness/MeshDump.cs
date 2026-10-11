using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ara3D.Geometry;
using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>What a mesh dump records about its origin: the tool, the input file, and the run.</summary>
public sealed record MeshDumpHeader(
    string Mesher,
    string MesherVersion,
    MeshDumpSource Source,
    long ElapsedMs,
    DateTime GeneratedUtc);

public sealed record MeshDumpSource(string FileName, string Sha256, long Bytes)
{
    public static MeshDumpSource Of(FilePath ifcPath)
    {
        using var stream = File.OpenRead(ifcPath);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
        return new MeshDumpSource(ifcPath.GetFileName(), hash, new FileInfo(ifcPath).Length);
    }
}

/// <summary>
/// A <c>.meshdump</c> file: the meshes and instances of one model as any mesher produced them, in a
/// layout a dozen lines of JavaScript or Python can write. Header: ASCII <c>MDMP</c>, int32 version
/// (1), int32 JSON length, UTF-8 JSON (<see cref="MeshDumpHeader"/> fields plus <c>meshes</c>
/// [{points, triangles}] and <c>instances</c> [{entity, mesh, matrix[16]}]). Body: for each mesh in
/// order, points × 3 float32 then triangles × 3 int32. Lengths are metres. The matrix is the 16 floats
/// of System.Numerics, IfcOpenShell, and web-ifc alike: translation at indices 12, 13, 14.
/// </summary>
public static class MeshDump
{
    public const string Extension = ".meshdump";
    const int Version = 1;
    static readonly byte[] Magic = "MDMP"u8.ToArray();

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static FilePath PathFor(DirectoryPath folder, FilePath ifcPath)
        => folder.RelativeFile(ifcPath.GetFileNameWithoutExtension() + Extension);

    public static (MeshDumpHeader Header, Model3D Model) Read(FilePath path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException($"{path} is not a mesh dump (bad magic)");
        var version = reader.ReadInt32();
        if (version != Version)
            throw new InvalidDataException($"{path}: mesh dump version {version}, expected {Version}");
        var jsonLength = reader.ReadInt32();
        var document = JsonSerializer.Deserialize<DumpDocument>(reader.ReadBytes(jsonLength), Json)
            ?? throw new InvalidDataException($"{path}: empty header");

        var builder = new Model3DBuilder();
        foreach (var mesh in document.Meshes)
        {
            var points = new List<Point3D>(mesh.Points);
            for (var i = 0; i < mesh.Points; i++)
                points.Add(new Point3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            var faces = new List<Integer3>(mesh.Triangles);
            for (var i = 0; i < mesh.Triangles; i++)
                faces.Add(new Integer3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
            builder.Meshes.Add(new TriangleMesh3D(points, faces));
        }
        foreach (var instance in document.Instances)
            builder.AddInstance(instance.Mesh, ToMatrix(instance.Matrix), Material.Default, instance.Entity);

        var header = new MeshDumpHeader(
            document.Mesher, document.MesherVersion, document.Source, document.ElapsedMs, document.GeneratedUtc);
        return (header, builder.Build());
    }

    /// <summary>Reads only the header, to decide whether a dump is current.</summary>
    public static MeshDumpHeader ReadHeader(FilePath path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        reader.ReadBytes(4);
        reader.ReadInt32();
        var jsonLength = reader.ReadInt32();
        var document = JsonSerializer.Deserialize<DumpDocument>(reader.ReadBytes(jsonLength), Json)
            ?? throw new InvalidDataException($"{path}: empty header");
        return new MeshDumpHeader(
            document.Mesher, document.MesherVersion, document.Source, document.ElapsedMs, document.GeneratedUtc);
    }

    public static void Write(FilePath path, MeshDumpHeader header, Model3D model)
    {
        var document = new DumpDocument(
            header.Mesher,
            header.MesherVersion,
            header.Source,
            header.ElapsedMs,
            header.GeneratedUtc,
            model.Meshes.Select(m => new DumpMesh(m.Points.Count, m.FaceIndices.Count)).ToList(),
            model.Instances.Select(i => new DumpInstance(i.EntityIndex, i.MeshIndex, FromMatrix(i.Matrix4x4))).ToList());
        var json = JsonSerializer.SerializeToUtf8Bytes(document, Json);

        path.GetDirectory().Create();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(json.Length);
        writer.Write(json);
        foreach (var mesh in model.Meshes)
        {
            foreach (var p in mesh.Points)
            {
                writer.Write((float)p.X.Value);
                writer.Write((float)p.Y.Value);
                writer.Write((float)p.Z.Value);
            }
            foreach (var f in mesh.FaceIndices)
            {
                writer.Write(f.A);
                writer.Write(f.B);
                writer.Write(f.C);
            }
        }
    }

    static Matrix4x4 ToMatrix(float[] m)
    {
        if (m.Length != 16)
            throw new InvalidDataException($"instance matrix has {m.Length} values, expected 16");
        return new Matrix4x4(new System.Numerics.Matrix4x4(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]));
    }

    static float[] FromMatrix(Matrix4x4 m)
        =>
        [
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44,
        ];

    sealed record DumpDocument(
        string Mesher,
        string MesherVersion,
        MeshDumpSource Source,
        long ElapsedMs,
        DateTime GeneratedUtc,
        IReadOnlyList<DumpMesh> Meshes,
        IReadOnlyList<DumpInstance> Instances);

    sealed record DumpMesh(int Points, int Triangles);

    sealed record DumpInstance(int Entity, int Mesh, float[] Matrix);
}
