using System.Diagnostics;
using Ara3D.Parakeet;
using Ara3D.Parakeet.Grammars;

namespace Ara3D.IfcTypeGen;

/// <summary>
/// Reads an EXPRESS file into an <see cref="ExpressSchema"/> with Parakeet's ExpressGrammar. A block the
/// grammar reads only in part (the grammar recovers by skipping to the end and records ParserState.LastError)
/// throws, so a construct the grammar does not know cannot silently drop attributes.
/// </summary>
public static class ExpressReader
{
    public static ExpressSchema GetSchema(this ParserInput pi)
        => new(pi.GetEntities(), pi.GetTypeDecls());

    public static IReadOnlyList<ExpressEntity> GetEntities(this ParserInput pi)
        => pi.GetBlocks(ExpressGrammar.Instance.EntityBlocks).Select(ToEntity).ToList();

    public static IReadOnlyList<IExpressTypeDecl> GetTypeDecls(this ParserInput pi)
        => pi.GetBlocks(ExpressGrammar.Instance.TypeBlocks).Select(ToTypeDecl).ToList();

    public static IReadOnlyList<string> GetBlocks(this ParserInput pi, Rule blocksRule)
    {
        var ps = blocksRule.Parse(pi);
        Debug.Assert(ps.AtEnd());
        return ps.AllEndNodes().Select(n => n.Contents).ToList();
    }

    public static ParserTreeNode ParseBlock(Rule rule, string text)
    {
        var ps = rule.Parse(text);
        if (ps == null || !ps.AtEnd() || ps.LastError != null)
            throw new InvalidOperationException($"ExpressGrammar did not read this block in full ({ps?.LastError}):\n{text}");
        return ps.Node.ToParseTree();
    }

    public static ExpressEntity ToEntity(string text)
        => ToEntity(ParseBlock(ExpressGrammar.Instance.Entity, text));

    public static ExpressEntity ToEntity(ParserTreeNode node)
    {
        Debug.Assert("Entity" == node.Node.Name);
        var header = node.Children[0];
        var name = header.Children[0].Contents;
        var attributes = node.Child("EntityBody")!.Children
            .Where(a => a.Children[0].Node.Name == "Identifier")
            .Select(ToAttribute)
            .ToList();
        var derived = node.Children
            .Where(c => c.Node.Name == "DerivedAttribute")
            .Select(d => d.Children[0])
            .Where(n => n.Node.Name == "RedeclaredAttribute")
            .Select(ToAttributeRef)
            .ToList();
        return new(name, GetSupertypes(header), attributes, derived);
    }

    public static IReadOnlyList<string> GetSupertypes(ParserTreeNode header)
        => header.Child("SubtypeHeader")?.Children[0].Children.Select(c => c.Contents).ToList() ?? [];

    public static ExpressAttribute ToAttribute(ParserTreeNode node)
    {
        Debug.Assert("AttributeDecl" == node.Node.Name);
        return new(node.Children[0].Contents, ToTypeExpr(node.Children[1]));
    }

    public static ExpressAttributeRef ToAttributeRef(ParserTreeNode node)
    {
        Debug.Assert("RedeclaredAttribute" == node.Node.Name);
        return new(node.Children[0].Contents, node.Children[1].Contents);
    }

    public static ExpressTypeExpr ToTypeExpr(ParserTreeNode node)
    {
        Debug.Assert("TypeExpr" == node.Node.Name);
        var partTwo = node.Children[0];

        if (partTwo.Node.Name == "AggregationType")
        {
            Debug.Assert(1 == partTwo.Children.Count);
            return new("List", [ToTypeExpr(partTwo.Children[0])]);
        }

        if (partTwo.Node.Name == "Identifier")
            return new(partTwo.Node.Contents, []);

        throw new Exception($"Unexpected type: {node}");
    }

    public static IExpressTypeDecl ToTypeDecl(string text)
    {
        var tree = ParseBlock(ExpressGrammar.Instance.TypeDecl, text);
        var name = tree.Children[0].Node.Contents;
        var typeDef = tree.Children[1].Children[0];

        if (typeDef.Node.Name == "EnumerationType")
            return new ExpressEnumeration(name, IdentifierListContents(typeDef));
        if (typeDef.Node.Name == "SelectType")
            return new ExpressSelect(name, IdentifierListContents(typeDef));
        if (typeDef.Node.Name == "TypeExpr")
            return new ExpressTypeAlias(name, ToTypeExpr(typeDef));

        throw new Exception($"Unrecognized type {typeDef}");
    }

    public static IReadOnlyList<string> IdentifierListContents(ParserTreeNode node)
        => node.Children[0].Children.Select(c => c.Contents).ToList();

    public static ParserTreeNode? Child(this ParserTreeNode node, string name)
        => node.Children.FirstOrDefault(c => c.Node.Name == name);
}
