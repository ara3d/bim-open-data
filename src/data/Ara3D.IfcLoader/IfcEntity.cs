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

    /// <summary>Display name: IfcSpace.LongName (attr 7), the name attribute of a material
    /// resource entity (<see cref="MaterialNameIndex"/>), then IfcRoot.Name (attr 2).</summary>
    public string GetEntityLabel()
    {
        var entityName = GetEntityName();

        // IfcSpace exporters typically put the room number in Name and the descriptive label in LongName.
        if (entityName == "IFCSPACE")
        {
            var longName = GetStringOrEmpty(7);
            if (!string.IsNullOrEmpty(longName))
                return longName;
        }

        if (entityName.StartsWith("IFCMATERIAL", StringComparison.Ordinal))
        {
            var materialName = MaterialNameIndex.TryGetValue(entityName, out var nameIndex) ? GetStringOrEmpty(nameIndex) : "";
            return string.IsNullOrEmpty(materialName) ? $"#{Id}" : materialName;
        }

        var rootName = GetStringOrEmpty(2);
        return string.IsNullOrEmpty(rootName) ? $"#{Id}" : rootName;
    }

    /// <summary>Where the material resource entities keep their name; one not listed here
    /// (IfcMaterialList, IfcMaterialLayerSetUsage) has none. They are not IfcRoot, so attribute 2
    /// is something else: IfcMaterial's Category ('Generisch' in the fantasy office
    /// models), IfcMaterialLayer's IsVentilated, a constituent set's member list.</summary>
    public static readonly IReadOnlyDictionary<string, int> MaterialNameIndex = new Dictionary<string, int>
    {
        ["IFCMATERIAL"] = 0,
        ["IFCMATERIALLAYER"] = 3,
        ["IFCMATERIALLAYERWITHOFFSETS"] = 3,
        ["IFCMATERIALLAYERSET"] = 1,
        ["IFCMATERIALCONSTITUENT"] = 0,
        ["IFCMATERIALCONSTITUENTSET"] = 0,
        ["IFCMATERIALPROFILE"] = 0,
        ["IFCMATERIALPROFILEWITHOFFSETS"] = 0,
        ["IFCMATERIALPROFILESET"] = 0,
    };

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