using Ara3D.IfcLoader;
using System.Text;
using Ara3D.Ifc.Mesher;
using Ara3D.IO.StepParser;
using Ara3D.Memory;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>A hand-written STEP snippet as a <see cref="MeshingContext"/>: the data lines are wrapped
/// in an IFC4 header and footer, parsed from memory, and resolved. No file and no native code.</summary>
public static class MicroIfc
{
    public static string WrapData(string dataLines)
        => MiniIfc.Document(dataLines, MiniIfc.Ifc4, "micro.ifc", "ViewDefinition [CoordinationView]");

    public static MicroIfcModel Parse(string dataLines, int circleSegments = 32, double? lengthScaleOverride = null)
        => ParseContent(WrapData(dataLines), circleSegments, lengthScaleOverride);

    public static MicroIfcModel ParseContent(string ifcContent, int circleSegments = 32, double? lengthScaleOverride = null)
    {
        var document = new StepDocument(Encoding.ASCII.GetBytes(ifcContent).Fix());
        var ctx = new MeshingContext(document, lengthScaleOverride, circleSegments);
        return new MicroIfcModel(ctx);
    }

    /// <summary>The same snippet written to a temporary file and parsed from disk, for tests of the
    /// file-reading path.</summary>
    public static MicroIfcModel WriteTemp(string dataLines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"micro-ifc-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, WrapData(dataLines));
        return new MicroIfcModel(new MeshingContext(new StepDocument(new FilePath(path))));
    }
}

public sealed class MicroIfcModel : IDisposable
{
    public MicroIfcModel(MeshingContext context)
    {
        Context = context;
    }

    public MeshingContext Context { get; }
    public IfcEntityResolver Resolver => Context.Resolver;

    public IfcEntity Entity(int id) => Context.GetEntity(id);

    public void Dispose() => Context.Dispose();
}
