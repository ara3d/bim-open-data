using Ara3D.Ifc.Mesher;
using Ara3D.Ifc.Mesher.Approach1;
using Ara3D.IfcLoader;
using Ara3D.Utils;

namespace BimOpenMcp.Ifc;

/// <summary>An open IFC file and the indexes derived from it. Relations and property data each
/// cost a whole-file scan, so they are built on first use and then kept. Every <see cref="IfcEntity"/>
/// handed out points into the file's pinned buffer and is invalid once this session is disposed.
/// Geometry is never loaded, which keeps the native web-ifc DLL out of the picture.</summary>
public sealed class IfcSession : IDisposable
{
    private IfcRelations? _relations;
    private IfcPropData? _properties;
    private IfcParameterIndex? _parameters;
    private readonly BosSessionCache _bosSessions;
    private IfcBosArtifacts? _conversion;
    private IfcMeshingResult? _meshing;

    /// <summary>Opens the file. <paramref name="bosSessions"/> holds the session over its BOS
    /// conversion once one is built; disposing this session closes that one.</summary>
    public IfcSession(FilePath path, BosSessionCache bosSessions)
    {
        _bosSessions = bosSessions;
        Path = path;
        File = IfcFile.Load(path, includeGeometry: false);
        OpenedUtc = DateTime.UtcNow;
    }

    public FilePath Path { get; }

    public IfcFile File { get; }

    public DateTime OpenedUtc { get; }

    public IfcEntityResolver Resolver
        => File.EntityResolver;

    public string Schema
        => File.Document.Header.FileSchema ?? "";

    public IfcRelations Relations
        => _relations ??= new IfcRelations(File);

    public IfcPropData Properties
        => _properties ??= new IfcPropData(File);

    /// <summary>Properties and quantities inverted by parameter, built on first use over
    /// <see cref="Properties"/>. Costs no extra file read.</summary>
    public IfcParameterIndex Parameters
        => _parameters ??= new IfcParameterIndex(this);

    /// <summary>The BOS conversion, built on first use. Unlike the other indexes this one re-reads
    /// the file from disk with geometry enabled, so it is by far the most expensive thing a session
    /// can hold.</summary>
    public IfcBosArtifacts Conversion
        => _conversion ??= new IfcBosArtifacts(Path);

    /// <summary>The BOS session over <see cref="Conversion"/>: its tables, scene, and DuckDB
    /// database, held in the shared <see cref="BosSessionCache"/> under the conversion's bosPath,
    /// so the bos_* tools given that path use it too. Converts on first use, and reads the
    /// conversion's .bos again if the cache has evicted the session since.</summary>
    public BosSession Bos
        => _bosSessions.Get(Conversion.BosPath.FullPath);

    public bool BosIsBuilt
        => _conversion != null;

    /// <summary>Triangle meshes for the model, built on first use and then kept. The Approach1 mesher
    /// reads STEP geometry definitions in pure C# — its own <c>Build(FilePath)</c> overload opens the
    /// file with <c>includeGeometry: false</c> — so meshing reuses this session's already-open file and
    /// never needs the native web-ifc tessellator or a geometry-enabled reopen. Failure comes back as a
    /// non-success result, not an exception.</summary>
    public IfcMeshingResult Meshing
        => _meshing ??= new Approach1Mesher().Build(File);

    public bool MeshingIsBuilt
        => _meshing != null;

    public BosSession RebuildBos()
    {
        DropConversion();
        return Bos;
    }

    private void DropConversion()
    {
        if (_conversion == null)
            return;
        _bosSessions.Close(_conversion.BosPath.FullPath);
        _conversion.Dispose();
        _conversion = null;
    }

    public void Dispose()
    {
        DropConversion();
        File.Dispose();
    }
}
