using Ara3D.IfcLoader;
using Ara3D.Utils;

namespace Ara3D.Ifc.Mesher.Benchmarks;

/// <summary>The three timed operations, shared by the BenchmarkDotNet run and the quick run.</summary>
public static class Operations
{
    /// <summary>Meshing only, over a file already parsed without web-ifc geometry. Returns the mesh count so the work is not elided.</summary>
    public static int Mesh(IfcFile parsed)
        => ModelAssembler.BuildModel(parsed.EntityResolver).Item1.Meshes.Count;

    /// <summary>Opens and parses the file, then meshes it.</summary>
    public static int ParseAndMesh(string path)
    {
        using var file = Parse(path);
        return Mesh(file);
    }

    /// <summary>The web-ifc DLL's own geometry load and conversion to a model.</summary>
    public static int WebIfc(string path)
    {
        using var file = new IfcFile(new FilePath(path), includeGeometry: true);
        return file.ToModel3D().Meshes.Count;
    }

    public static IfcFile Parse(string path) => new(new FilePath(path), includeGeometry: false);
}
