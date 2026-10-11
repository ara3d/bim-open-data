using Ara3D.Ifc.Mesher;
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
    private readonly object _conversionLock = new();
    private IfcBosArtifacts? _conversion;
    private SessionLease<BosSession>? _bos;
    private IfcMeshingResult? _meshing;

    /// <summary>Opens the file. <paramref name="bosSessions"/> holds the session over its BOS
    /// conversion once one is built, and this session holds a lease on it, so the cache does not
    /// evict it while this session is open; disposing this session closes it.</summary>
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

    /// <summary>The BOS conversion, built on first use or found where an earlier server built it.
    /// Unlike the other indexes this one re-reads the file from disk with geometry enabled, so it
    /// is by far the most expensive thing a session can hold.</summary>
    public IfcBosArtifacts Conversion
    {
        get
        {
            lock (_conversionLock)
                return _conversion ??= new IfcBosArtifacts(Path, _bosSessions.Files);
        }
    }

    /// <summary>The BOS session over <see cref="Conversion"/>: its tables, scene, and DuckDB
    /// database, held in the shared <see cref="BosSessionCache"/> under the conversion's bosPath,
    /// so the bos_* tools given that path use it too. Converts on first use. The cache keeps it
    /// open, whatever its capacity, until this session closes.</summary>
    public BosSession Bos
    {
        get
        {
            lock (_conversionLock)
                return (_bos ??= _bosSessions.Lease(Conversion.BosPath.FullPath)).Session;
        }
    }

    public bool BosIsBuilt
        => _conversion != null;

    /// <summary>Triangle meshes for the model, built on first use and then kept. The mesher
    /// reads STEP geometry definitions in pure C# — its own <c>Build(FilePath)</c> overload opens the
    /// file with <c>includeGeometry: false</c> — so meshing reuses this session's already-open file and
    /// never needs the native web-ifc tessellator or a geometry-enabled reopen. Failure comes back as a
    /// non-success result, not an exception.</summary>
    public IfcMeshingResult Meshing
        => _meshing ??= IfcMesher.Build(File.EntityResolver);

    public bool MeshingIsBuilt
        => _meshing != null;

    /// <summary>Converts the file again, replacing the stored conversion, and opens a new BOS
    /// session over it.</summary>
    public BosSession RebuildBos()
    {
        lock (_conversionLock)
        {
            DropConversion();
            _conversion = new IfcBosArtifacts(Path, _bosSessions.Files, rebuild: true);
            return Bos;
        }
    }

    /// <summary>Lets go of the BOS session and closes it in the cache; a call still holding it
    /// keeps it until that call ends.</summary>
    private void DropConversion()
    {
        lock (_conversionLock)
        {
            if (_conversion == null)
                return;
            _bos?.Dispose();
            _bos = null;
            _bosSessions.Close(_conversion.BosPath.FullPath);
            _conversion = null;
        }
    }

    public void Dispose()
    {
        DropConversion();
        File.Dispose();
    }
}
