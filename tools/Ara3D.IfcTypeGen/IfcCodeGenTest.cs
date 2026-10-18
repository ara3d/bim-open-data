using System.Text;
using Ara3D.Parakeet;
using Ara3D.Parakeet.Tests;
using Ara3D.Utils;

namespace Ara3D.IfcTypeGen;

/// <summary>
/// Generates the Ara3D.IfcTypes source from the buildingSMART EXPRESS schema files
/// that ship with Parakeet (deps/parakeet/input/exp, from deps.json).
/// </summary>
public static class IfcCodeGenTest
{
    [Test, Explicit]
    public static void TestGenerate()
    {
        var pi2x3 = ExpressTests.GetIfc2x3ExpressFileAsParserInput();
        var pi4 = ExpressTests.GetIfc4ExpressFileAsParserInput();
        var pi4x3 = ExpressTests.GetIfc4x3ExpressFileAsParserInput();

        Generate(pi2x3, "Ifc2x3");
        Generate(pi4, "Ifc4");
        Generate(pi4x3, "Ifc4x3");
    }

    public static (string TypeName, int Rank) GetTypeDetails(ExpressTypeExpr typeExpr)
    {
        var typeStr = typeExpr.Name;
        var isList = typeExpr.GenericParameters.Count > 0;
        if (isList)
        {
            var gp = typeExpr.GenericParameters[0];
            if (typeExpr.GenericParameters.Count > 1)
                throw new Exception("Only a maximum of 1 generic parameter is supported");
            var (name, rank) = GetTypeDetails(gp);
            return (name, rank + 1);
        }

        return (typeStr, 0);
    }

    public static string GetTypeDetailStr(ExpressTypeExpr type, Dictionary<string, IfcTypeKind> typeKinds)
    {
        var (typeStr, rank) = GetTypeDetails(type);
        var kind = typeKinds.GetValueOrDefault(typeStr, IfcTypeKind.Unknown);
        return $"new(typeof({typeStr}), IfcTypeKind.{kind}, {rank})";
    }
    
    public static StringBuilder OutputTypeAliases(ExpressSchema schema, string ns, Dictionary<string, ExpressTypeExpr> typeAliases, Dictionary<string, IfcTypeKind> typeKinds)
    {
        var sb = new StringBuilder();

        var selectLookup = schema.GetSelectsPerEntity();

        sb.AppendLine($"namespace Ara3D.IfcTypes.{ns};");
        foreach (var kv in typeAliases)
        {
            sb.AppendLine();
            sb.AppendLine($"public class {kv.Key}");

            var inherits = new List<string>();
            inherits.Add("TypeAliasBaseClass");

            if (selectLookup.ContainsKey(kv.Key))
                inherits.AddRange(selectLookup[kv.Key].Select(s => s.Name));

            if (inherits.Count > 0)
                sb.AppendLine("    : " + inherits.JoinStringsWithComma());
            
            sb.AppendLine("{");
            sb.AppendLine($"    public static TypeDetails Type = {GetTypeDetailStr(kv.Value, typeKinds)};");
            sb.AppendLine("}");
        }

        return sb;
    }

    public static StringBuilder OutputEntities(ExpressSchema schema, string ns, Dictionary<string, IfcTypeKind> typeKinds, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();
        var selectLookup = schema.GetSelectsPerEntity();
        var entities = schema.Entities.OrderBy(e => e.Name).ToList();
        
        var entityLookup = schema.ToEntityLookup();
        sb.AppendLine("#pragma warning disable CS0108");
        sb.AppendLine($"namespace Ara3D.IfcTypes.{ns};");

        foreach (var e in entities)
        {
            sb.AppendLine();
            sb.AppendLine($"public partial class {e.Name}");
            var inherits = new List<string>();

            if (e.Supertypes.Count > 1)
                throw new NotSupportedException($"{e.Name} has several supertypes; a generated class has one base");
            inherits.Add(e.Supertypes.Count == 1 ? e.Supertypes[0] : "EntityBaseClass");
            
            if (selectLookup.TryGetValue(e.Name, out var value))
            {
                var ifaces = value.OrderBy(s => s.Name);
                foreach (var i in ifaces)
                {
                    inherits.Add(i.Name);
                }
            }

            var code = e.Name.ToUpperInvariant().Fnv1a32bit();

            if (inherits.Count > 0)
                sb.AppendLine($"   : {inherits.JoinStringsWithComma()}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static {e.Name} Instance = new();");
            sb.AppendLine($"    public static ReadOnlySpan<byte> NAME => \"{e.Name.ToUpperInvariant()}\"u8;");
            sb.AppendLine($"    public const uint ENTITY_CODE = {code};");
            sb.AppendLine($"    public override uint EntityTypeCode => ENTITY_CODE;");
            sb.AppendLine($"    public override ReadOnlySpan<byte> EntityTypeName => NAME;");

            var slots = entityLookup.Layout(e.Name);

            foreach (var slot in slots.Where(s => s.Entity == e.Name))
            {
                var attr = slot.Attribute;
                var (typeStr, rank) = GetTypeDetails(attr.TypeExpr);
                var kind = typeKinds.GetValueOrDefault(typeStr, IfcTypeKind.Unknown);
                sb.AppendLine($"    public readonly IfcAttribute<{typeStr}> {attr.Name} = new(\"{attr.Name}\", {slot.Position}, IfcTypeKind.{kind}, {rank});");
            }

            var attrNames = slots.Select(s => s.Attribute.Name).JoinStringsWithComma();
            sb.AppendLine($"    public override IfcAttribute[] Attributes => [ {attrNames} ];");
            sb.AppendLine("}");
        }

        return sb;
    }

    public static StringBuilder OutputSchema(ExpressSchema schema, string ns, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"using Ara3D.IfcTypes.{ns};");
        sb.AppendLine();
        sb.AppendLine("namespace Ara3D.IfcTypes;");
        sb.AppendLine();
        sb.AppendLine("public static partial class IfcSchemas");
        sb.AppendLine("{");
        sb.AppendLine($"    public static IfcSchema {ns} = new(IfcSchemaEnum.{ns}, [");

        var first = true;
        foreach (var e in schema.Entities)
        {
            if (!first)
                sb.AppendLine(",");
            else
                first = false;
            sb.Append($"        {e.Name}.Instance");
        }

        sb.AppendLine("]);");
        sb.AppendLine("}");
        return sb;
    }

    public static StringBuilder OutputInterfaces(ExpressSchema schema, string ns, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();
        var selects = schema.GetSelects();
        sb.AppendLine($"namespace Ara3D.IfcTypes.{ns};");
        foreach (var s in selects)
        {
            sb.AppendLine();
            sb.AppendLine($"public interface {s.Name} : ISelectInterface {{ }}");
        }

        return sb;
    }

    public static StringBuilder OutputEnums(ExpressSchema schema, string ns, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();

        var enums = schema.GetEnums();
        sb.Clear();
        sb.AppendLine($"namespace Ara3D.IfcTypes.{ns};");
        foreach (var e in enums)
        {
            sb.AppendLine();
            sb.AppendLine($"public enum {e.Name}");
            sb.AppendLine("{");
            foreach (var o in e.Options)
                sb.AppendLine($"  {o},");
            sb.AppendLine("}");
        }

        return sb;
    }

    public enum IfcTypeKind
    {
        Enum,
        Select,
        Entity,
        Alias,
        System,
        Unknown,
    }

    public static void Generate(ParserInput input, string ns)
    {
        var schema = input.GetSchema();
        var outputDir = PathUtil.GetCallerSourceFolder().RelativeFolder("..", "..", "src", "data", "Ara3D.IfcTypes");

        var typeKinds = new Dictionary<string, IfcTypeKind>();
        typeKinds.Add("double", IfcTypeKind.System);
        typeKinds.Add("bool", IfcTypeKind.System);
        typeKinds.Add("string", IfcTypeKind.System);
        typeKinds.Add("long", IfcTypeKind.System);

        var typeAliases = schema.GetTypeAliases().ToDictionary(ta => ta.Name, ta => ta.TypeExpr);
        typeAliases.Add("REAL", new("double", []));
        typeAliases.Add("NUMBER", new("double", []));
        typeAliases.Add("LOGICAL", new("bool", []));
        typeAliases.Add("BOOLEAN", new("bool", []));
        typeAliases.Add("STRING", new("string", []));
        typeAliases.Add("BINARY", new("string", []));
        typeAliases.Add("INTEGER", new("long", []));

        foreach (var kv in typeAliases)
            typeKinds.Add(kv.Key, IfcTypeKind.Alias);

        foreach (var s in schema.GetSelects())
            typeKinds.Add("I" + s.Name, IfcTypeKind.Select);

        foreach (var e in schema.GetEnums())
            typeKinds.Add(e.Name, IfcTypeKind.Enum);
        foreach (var e in schema.Entities)
            typeKinds.Add(e.Name, IfcTypeKind.Entity);

        outputDir.RelativeFile($"Entities.{ns}.g.cs").WriteAllText(OutputEntities(schema, ns, typeKinds).ToString());
        outputDir.RelativeFile($"Interfaces.{ns}.g.cs").WriteAllText(OutputInterfaces(schema, ns).ToString());
        outputDir.RelativeFile($"Enums.{ns}.g.cs").WriteAllText(OutputEnums(schema, ns).ToString());
        outputDir.RelativeFile($"Aliases.{ns}.g.cs").WriteAllText(OutputTypeAliases(schema, ns, typeAliases, typeKinds).ToString());
        outputDir.RelativeFile($"Schema.{ns}.g.cs").WriteAllText(OutputSchema(schema, ns).ToString());
    }

    public static IEnumerable<ExpressSelect> GetSelects(this ExpressSchema self)
        => self.Decls.OfType<ExpressSelect>().OrderBy(s => s.Name);

    public static IEnumerable<ExpressEnumeration> GetEnums(this ExpressSchema self)
        => self.Decls.OfType<ExpressEnumeration>().OrderBy(s => s.Name);

    public static IEnumerable<ExpressTypeAlias> GetTypeAliases(this ExpressSchema self)
        => self.Decls.OfType<ExpressTypeAlias>().OrderBy(s => s.Name);

    public static MultiDictionary<string, ExpressSelect> GetSelectsPerEntity(this ExpressSchema self)
    {
        var r = new MultiDictionary<string, ExpressSelect>();
        foreach (var s in self.GetSelects())
        foreach (var o in s.Options)
            r.Add(o, s);
        return r;
    }
}
