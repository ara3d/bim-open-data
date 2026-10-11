using Ara3D.Ifc.Conventions;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;
using Ara3D.Utils;

namespace Ara3D.IfcLoader;

public class IfcEntity
{
    public IfcEntity(int Id,
        StepDefinition Definition,
        IReadOnlyList<StepToken> Attributes,
        StepDocument Document)
    {
        this.Id = Id;
        this.Definition = Definition;
        this.Attributes = Attributes;
        this.Document = Document;
    }

    public int Id { get; init; }
    public StepDefinition Definition { get; init; }
    public IReadOnlyList<StepToken> Attributes { get; init; }
    public StepDocument Document { get; init; }

    public string GetIfcRootName()
        => GetString(2);

    public string GetIfcRootGlobalId()
        => GetString(0);

    /// <summary>Display name: the entity's name by <see cref="IfcNames.NameOf"/>, the rule every
    /// IFC reader shares, or "#id" when it has none or an empty one.</summary>
    public string GetEntityLabel()
        => GetName(GetSchemaAttributes()).Name is { Length: > 0 } name ? name : $"#{Id}";

    /// <summary>The entity's name and room number by <see cref="IfcNames.NameOf"/>, finding each
    /// attribute by name in <paramref name="schemaAttributes"/>, whose positions are the positions
    /// in the STEP line, inherited attributes first. A name is a string or an enumeration value
    /// (IfcSIUnit.Name is <c>.METRE.</c>, read as METRE, as That Open's importer writes it).</summary>
    public IfcEntityName GetName(IReadOnlyList<IfcAttribute> schemaAttributes)
        => IfcNames.NameOf(GetEntityName(), name =>
        {
            for (var i = 0; i < schemaAttributes.Count; i++)
                if (schemaAttributes[i].Name == name)
                    return GetOptionalString(i) ?? GetOptionalEnumeration(i);
            return null;
        });

    /// <summary>The attribute's enumeration value without its dots, or null when it is not one.
    /// The booleans .T. and .F. are enumeration values too.</summary>
    public string? GetOptionalEnumeration(int index)
        => index < Attributes.Count && Attributes[index].IsSymbol
            ? Attributes[index].ToString().Trim('.')
            : null;

    /// <summary>The schema attributes of this entity's class, in the schema of its file; empty
    /// when the class is not in that schema.</summary>
    public IReadOnlyList<IfcAttribute> GetSchemaAttributes()
        => IfcSchemas.Of(Document).Entities.TryGetValue(GetEntityCode(), out var e) ? e.Attributes : [];

    /// <summary>The attribute's decoded text, or null when it is not a STEP string: unset ($),
    /// redeclared (*), a reference, a number, a list, or past the entity's attributes. A stored ''
    /// is returned as "": it is a value.</summary>
    public string? GetOptionalString(int index)
        => index < Attributes.Count && Attributes[index].IsString
            ? GetString(index).DecodeIfc()
            : null;

    public StepToken GetValue(int index)
        => Attributes.Count > index ? Attributes[index] : default;

    public string GetString(int index)
        => Attributes.Count > index ? Attributes[index].ToString().StripQuotes() : string.Empty;

    /// <summary>Like <see cref="GetString"/> but maps unset ($) / redeclared (*) STEP attributes to empty.</summary>
    public string GetStringOrEmpty(int index)
    {
        if (Attributes.Count <= index)
            return string.Empty;
        var token = Attributes[index];
        return token.IsUnassignedOrRedeclared ? string.Empty : token.ToString().StripQuotes();
    }

    public IReadOnlyList<StepToken> GetArray(int index)
        => Attributes.Count > index ? GetAttribute(index).AsList(Document) : [];

    public int[] GetIdList(int index)
    {
        var vals = GetArray(index);
        var r = new int[vals.Count];
        for (var i = 0; i < vals.Count; i++)
            r[i] = vals[i].AsId();
        return r;
    }

    public double[] GetNumberList(int index)
    {
        var vals = GetArray(index);
        var r = new double[vals.Count];
        for (var i = 0; i < vals.Count; i++)
            r[i] = vals[i].AsNumber();
        return r;
    }

    public StepToken GetAttribute(int n)
        => Attributes[n];

    public int GetId(int index)
        => Attributes.Count > index ? Attributes[index].AsId() : -1;

    public double GetNumber(int index)
        => Attributes.Count > index ? Attributes[index].AsNumber() : 0;

    public uint GetEntityCode()
        => Definition.NameToken.Span.Fnv1a32bit();

    public string GetEntityName()
        => Definition.NameToken.ToString();
}