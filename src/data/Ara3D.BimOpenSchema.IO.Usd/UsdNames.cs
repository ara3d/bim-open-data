namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Every prim name, path, and attribute name the exporter writes. Prim and namespace
/// names must be USD identifiers: ASCII letters, digits, and underscores, not starting
/// with a digit.
/// </summary>
public static class UsdNames
{
    /// <summary>The stage's default prim, which holds everything else.</summary>
    public const string Root = "Model";

    public const string MaterialsScope = "Materials";
    public const string PrototypesScope = "Prototypes";

    /// <summary>Declares each parameter attribute once with its BOS group, name, type, and units.</summary>
    public const string DescriptorsScope = "Descriptors";

    /// <summary>Holds the instances whose BOS entity index is missing or out of range.</summary>
    public const string Unassigned = "Unassigned";

    /// <summary>Material i is /Model/Materials/M{i}.</summary>
    public const string MaterialPrefix = "M";

    /// <summary>Mesh i is the child "Mesh" of /Model/Prototypes/P{i}.</summary>
    public const string PrototypePrefix = "P";
    public const string PrototypeMesh = "Mesh";

    /// <summary>BOS instance i is the prim I{i} under its element.</summary>
    public const string InstancePrefix = "I";

    /// <summary>An entity prim is E_{GlobalId} with invalid characters replaced, or
    /// E{entity index} when that is empty or already taken (see <see cref="EntityPrimNames"/>).</summary>
    public const string ElementPrefix = "E";

    /// <summary>Entity prims are children of the root: /Model/{prim name}.</summary>
    public const string EntityPathPrefix = "/" + Root + "/";

    public const string MaterialPath = "/" + Root + "/" + MaterialsScope + "/" + MaterialPrefix;
    public const string PrototypePath = "/" + Root + "/" + PrototypesScope + "/" + PrototypePrefix;

    /// <summary>The namespace of every attribute this exporter adds.</summary>
    public const string Namespace = "bim:";

    /// <summary>The namespace of BOS parameters: bim:param:{group}:{name}.</summary>
    public const string ParameterNamespace = Namespace + "param:";

    public const string EntityIndexAttribute = Namespace + "entityIndex";
    public const string LocalIdAttribute = Namespace + "localId";
    public const string GlobalIdAttribute = Namespace + "globalId";
    public const string NameAttribute = Namespace + "name";
    public const string CategoryAttribute = Namespace + "category";
    public const string TypeAttribute = Namespace + "type";
    public const string DocumentAttribute = Namespace + "document";

    /// <summary>The text as a USD identifier: each character outside [A-Za-z0-9_] becomes "_",
    /// a leading digit gets a "_" in front, and the empty string becomes "_". Not injective:
    /// callers that need unique names check for collisions.</summary>
    public static string ToIdentifier(string text)
    {
        if (text.Length == 0)
            return "_";
        var prefix = char.IsAsciiDigit(text[0]) ? 1 : 0;
        return string.Create(text.Length + prefix, (text, prefix), static (span, state) =>
        {
            if (state.prefix == 1)
                span[0] = '_';
            for (var i = 0; i < state.text.Length; i++)
                span[i + state.prefix] = IsIdentifierChar(state.text[i]) ? state.text[i] : '_';
        });
    }

    public static bool IsIdentifier(string text)
        => text.Length > 0 && !char.IsAsciiDigit(text[0]) && text.All(IsIdentifierChar);

    private static bool IsIdentifierChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c == '_';
}
