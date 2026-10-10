using System.Text.Json;
using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Attribute = Ara3D.BimOpenSchema.IO.Fragments.Schema.Attribute;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>One attribute of an item: That Open's importer writes each as the JSON text
/// <c>[name, value, ifcType]</c>, for example <c>["OverallHeight",2.01,"IFCPOSITIVELENGTHMEASURE"]</c>.
/// A null attribute is never written, so a missing attribute stays missing.</summary>
public sealed record FragmentsAttribute(string Name, JsonElement Value, string? IfcType);

/// <summary>One named relation of an item, written as the JSON text <c>[name, id, id, ...]</c>:
/// the item points at the local ids it names. The importer writes both sides of an IFC relation
/// (ContainedInStructure on the element, ContainsElements on the storey) and every entity-valued
/// attribute (Material, HasProperties, Units) the same way.</summary>
public sealed record FragmentsRelation(string Name, IReadOnlyList<long> Targets);

/// <summary>The per-item tables of a Fragments model, indexed by item position (the index into
/// <c>local_ids</c>), with the JSON strings parsed. Strings that do not parse are collected in
/// <see cref="Problems"/> instead of stopping the read.</summary>
public sealed class FragmentsItems
{
    public int Count { get; }
    public uint[] LocalIds { get; }
    public string?[] Categories { get; }
    public string?[] GlobalIds { get; }
    public IReadOnlyList<FragmentsAttribute>[] Attributes { get; }
    public IReadOnlyList<FragmentsRelation>[] Relations { get; }
    public Dictionary<uint, int> IndexOfLocalId { get; } = new();
    public List<string> Problems { get; } = [];

    public FragmentsItems(Model model)
    {
        Count = model.LocalIdsLength;
        LocalIds = model.GetLocalIdsArray() ?? [];
        Categories = new string?[Count];
        GlobalIds = new string?[Count];
        Attributes = new IReadOnlyList<FragmentsAttribute>[Count];
        Relations = new IReadOnlyList<FragmentsRelation>[Count];

        for (var i = 0; i < Count; i++)
        {
            IndexOfLocalId.TryAdd(LocalIds[i], i);
            Categories[i] = i < model.CategoriesLength ? model.Categories(i) : null;
            Attributes[i] = i < model.AttributesLength ? ReadAttributes(model.Attributes(i)) : [];
            Relations[i] = [];
        }

        for (var g = 0; g < model.GuidsLength && g < model.GuidsItemsLength; g++)
            if (IndexOfLocalId.TryGetValue(model.GuidsItems(g), out var item))
                GlobalIds[item] = model.Guids(g);

        // relations_items holds the local id each relations entry belongs to (not an item index).
        // The importer records relations for entities of classes it does not keep as items
        // (IfcOpeningElement, IfcDoorStyle), so those entries have no item to belong to.
        var orphans = 0;
        for (var r = 0; r < model.RelationsLength && r < model.RelationsItemsLength; r++)
        {
            var localId = model.RelationsItems(r);
            if (localId >= 0 && IndexOfLocalId.TryGetValue((uint)localId, out var item))
                Relations[item] = ReadRelations(model.Relations(r));
            else
                orphans++;
        }
        if (orphans > 0)
            Problems.Add($"{orphans} relation entries belong to local ids that are not items in the file and were dropped");
    }

    private IReadOnlyList<FragmentsAttribute> ReadAttributes(Attribute? attribute)
    {
        if (attribute is not { } a)
            return [];
        var list = new List<FragmentsAttribute>(a.DataLength);
        for (var j = 0; j < a.DataLength; j++)
            if (ParseAttribute(a.Data(j)) is { } parsed)
                list.Add(parsed);
        return list;
    }

    private IReadOnlyList<FragmentsRelation> ReadRelations(Relation? relation)
    {
        if (relation is not { } r)
            return [];
        var list = new List<FragmentsRelation>(r.DataLength);
        for (var j = 0; j < r.DataLength; j++)
            if (ParseRelation(r.Data(j)) is { } parsed)
                list.Add(parsed);
        return list;
    }

    private FragmentsAttribute? ParseAttribute(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 2 && root[0].ValueKind == JsonValueKind.String)
            {
                var type = root.GetArrayLength() >= 3 && root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;
                return new FragmentsAttribute(root[0].GetString()!, root[1].Clone(), type);
            }
        }
        catch (JsonException) { }
        Problems.Add($"Attribute is not a [name, value, type] array: {json}");
        return null;
    }

    private FragmentsRelation? ParseRelation(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 1 && root[0].ValueKind == JsonValueKind.String)
            {
                var targets = new List<long>(root.GetArrayLength() - 1);
                foreach (var e in root.EnumerateArray().Skip(1))
                    if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var id))
                        targets.Add(id);
                return new FragmentsRelation(root[0].GetString()!, targets);
            }
        }
        catch (JsonException) { }
        Problems.Add($"Relation is not a [name, id, ...] array: {json}");
        return null;
    }
}
