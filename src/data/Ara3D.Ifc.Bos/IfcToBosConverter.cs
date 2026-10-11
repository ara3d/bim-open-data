using Ara3D.DataTable;
using Ara3D.Ifc.Conventions;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;
using Ara3D.Logging;
using Ara3D.Models;
using Ara3D.Utils;
using Parquet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ara3D.BimOpenSchema.IO;

public class IfcToBosConverter
{
    public const EntityIndex InvalidEntityIndex = (EntityIndex)(-1);

    /// <summary>Kept for callers; the name lives in <see cref="IfcParameterNames.AxisTag"/>.</summary>
    public const string AxisTagParameter = IfcParameterNames.AxisTag;

    public IfcFile IfcFile;
    public IfcPropData PropData;
    public Model3D Model3D;
    public IfcInstanceTypeRelations TypeRelations;
    public BimDataBuilder BimDataBuilder = new();
    public BimGeometryBuilder BimGeometryBuilder = new();
    public BimGeometry BimGeometry { get; }
    public List<IfcEntity> BosEntities;
    public Dictionary<int, EntityIndex> IfcIdToBosId = new();
    public Dictionary<string, EntityIndex> CatEntities = new();
    public FilePath Input;
    public IDataSet DataSet;
    DocumentIndex _docIndex = (DocumentIndex)(-1);

    public EntityIndex GetCatEntityIndex(int ifcId)
    {
        var entity = GetEntity(ifcId);
        var entityName = entity.GetEntityName();
        if (!CatEntities.TryGetValue(entityName, out var r))
            throw new Exception($"Could not find category index of {entityName}");

        return r;
    }

    public IfcEntity GetEntity(int id)
        => IfcFile.EntityResolver.GetEntity(id);

    public IfcEntity GetEntityOrDefault(int id)
        => IfcFile.EntityResolver.GetEntityOrDefault(id);
    
    public EntityIndex GetBosEntityIndexFromIfc(int id)
        => IfcIdToBosId.GetValueOrDefault(id, InvalidEntityIndex);

    public static bool IsMaybeIfcElement(IfcEntity entity)
        => IfcClasses.IsMaybeElement(entity.GetEntityName());

    public static void Convert(FilePath input, FilePath output, ILogger? logger = null)
        => new IfcToBosConverter(input, logger).SaveToBos(output);

    public IfcToBosConverter(FilePath input, ILogger? logger = null)
    {
        Input = input;

        BimDataBuilder.Manifest.GeneratorApplication = "Ara3D IFC to BIM Open Schema Converter";
        BimDataBuilder.Manifest.GeneratorVersion = "0.1.0";

        IfcFile = new IfcFile(input, true, logger);

        logger?.Log("Loaded file");
        var model = IfcFile.Model;
        var doc = IfcFile.Document;
        var schema = IfcFile.Schema;

        // NOTE: if I want this to go a LOT faster, I can skip STUPID definitions. 
        // We have properties, we have property sets, we have entities, which have properties. 
        // We definitely don't care about all of the geometric bull-shit. Even storing that ... is borderline pointless.
        // Ideally I would throw it away, as the thing loads. 

        logger?.Log("Creating prop data");
        PropData = new IfcPropData(IfcFile);
        logger?.Log($"{PropData.Errors.Count} errors");
        logger?.Log($"{PropData.ObjectToPropSets.Count} objects with prop-sets");
        logger?.Log($"{PropData.PropValues.Count} prop-values");
        logger?.Log($"{PropData.PropSets.Count} prop-sets");

        logger?.Log("Creating type to instance relations");
        TypeRelations = new IfcInstanceTypeRelations(doc);
        logger?.Log($"Found {TypeRelations.InstancesToTypes.Count} instances and {TypeRelations.TypeIds.Count} types");

        logger?.Log("Creating list of IFC entities to convert to BOS entities");

        BosEntities = IfcFile.EntityResolver.GetEntities().Where(IsMaybeIfcElement).ToList();

        logger?.Log($"Found {BosEntities.Count} entities, among {IfcFile.EntityResolver.EntityLookup.Count} total entities");

        _docIndex = BimDataBuilder.AddDocument(input.GetFileNameWithoutExtension(), input);

        logger?.Log($"Creating categories");
        var uniqueNames = BosEntities.Select(e => e.GetEntityName().DecodeIfc()).Distinct().ToList();
        foreach (var stepEntityName in uniqueNames)
        {
            var ei = BimDataBuilder.AddEntity(-1, null, _docIndex, stepEntityName, (EntityIndex)(-1), (EntityIndex)(-1));
            CatEntities.Add(stepEntityName, ei);
        }

        logger?.Log($"Found and created {uniqueNames.Count} categories from entity names in IFC file");

        logger?.Log($"Creating {TypeRelations.TypeIds.Count} type entities");
        foreach (var id in TypeRelations.TypeIds)
        {
            var e = GetEntity(id);
            var catEi = GetCatEntityIndex(id);
            var name = NameOrNull(e);
            var gid = OptionalString(e, 0);
            var ei = BimDataBuilder.AddEntity(id, gid, _docIndex, name, catEi, InvalidEntityIndex);
            IfcIdToBosId.Add(id, ei);
        }

        // NOTE: this is all of the ids. Types have already been processed.   
        var ids = BosEntities.Select(e => e.Id).ToList();

        logger?.Log($"Founds {ids.Count} potential instance entities");
        var catIds = new HashSet<int>();
        foreach (var id in ids)
        {
            if (IfcIdToBosId.ContainsKey(id))
                continue;

            var e = GetEntity(id);
            var catEi = GetCatEntityIndex(id);
            var typeEi = InvalidEntityIndex;

            var entityCode = e.GetEntityCode();

            // NOTE: if there is a schema mismatch, this will throw an exception.
            var ifcEntityPrototype = schema.Entities[entityCode];
            var protoName = ifcEntityPrototype.GetEntityName();
            Debug.Assert(e.GetEntityName() == protoName);

            var attributes = ifcEntityPrototype.Attributes;
            
            // Only an IfcRoot has a GlobalId; any other entity keeps none (-1), as does an unset one.
            var gid = attributes.Length > 0 && attributes[0].Name == "GlobalId" ? OptionalString(e, 0) : null;

            var name = NameOrNull(e);

            if (TypeRelations.InstancesToTypes.TryGetValue(id, out var typeId))
                typeEi = GetBosEntityIndexFromIfc(typeId);

            catIds.Add((int)catEi);
            var ei = BimDataBuilder.AddEntity(id, gid, _docIndex, name, catEi, typeEi);
            IfcIdToBosId.Add(id, ei);

            if (e.GetEntityName() == "IFCSPACE")
            {
                var roomNumber = e.GetStringOrEmpty(2).DecodeIfc();
                if (!string.IsNullOrEmpty(roomNumber))
                    BimDataBuilder.AddParameter(ei, roomNumber, IfcParameterNames.RoomNumber, "", e.GetEntityName());
            }
            else if (e.GetEntityName() == "IFCGRIDAXIS")
            {
                // IFCGRIDAXIS is not an IfcRoot, so attribute 0 is AxisTag rather than GlobalId.
                // ProcessAttributeAsProp below starts at index 3 and never sees it, so it is
                // recorded here instead.
                var axisTag = e.GetStringOrEmpty(0).DecodeIfc();
                if (!string.IsNullOrEmpty(axisTag))
                    BimDataBuilder.AddParameter(ei, axisTag, IfcParameterNames.AxisTag, "", e.GetEntityName());
            }

            // Additional attributes are added as properties. An IfcRoot entity's first three are
            // GlobalId, OwnerHistory, and Name; a material resource entity is not IfcRoot, and its
            // first attributes carry what it is about (IfcMaterialLayer.LayerThickness is attribute 1).
            var isMaterial = IsMaterialResource(e);
            for (var i = isMaterial ? 0 : 3; i < attributes.Length; i++)
            {
                if (i >= e.Attributes.Count)
                    break;
                // A material set's member list is recorded on each member instead, by AddMaterialSetMembers.
                if (isMaterial && e.GetAttribute(i).IsList)
                    continue;
                ProcessAttributeAsProp(e, attributes[i], i, ei);
            }

        }

        AddMaterialSetMembers();

        logger?.Log("Recording project length unit");
        var lengthUnit = IfcLengthUnit.Read(IfcFile);
        if (lengthUnit != null)
        {
            var projectEntity = IfcFile.EntityResolver.GetEntities().FirstOrDefault(e => e.GetEntityName() == "IFCPROJECT");
            var projectEi = projectEntity != null ? GetBosEntityIndexFromIfc(projectEntity.Id) : InvalidEntityIndex;
            if (projectEi != InvalidEntityIndex)
            {
                BimDataBuilder.AddParameter(projectEi, lengthUnit.Value.Name, IfcLengthUnit.NameParameter, "", "IFCPROJECT");
                BimDataBuilder.AddParameter(projectEi, lengthUnit.Value.ToMetre, IfcLengthUnit.ScaleParameter, "", "IFCPROJECT");
            }
        }

        logger?.Log("Creating relations");
        var ifcRels = new IfcRelations(IfcFile);
        var addedRelations = 0;
        foreach (var rel in ifcRels.Relations)
        {
            // TODO: relations whose endpoints were filtered out (not in IfcIdToBosId) are silently
            // dropped here. Consider counting/reporting them as diagnostics, and check whether
            // upstream IfcRelations yields endpoints that should have been converted as entities.
            var a = GetBosEntityIndexFromIfc(rel.From);
            var b = GetBosEntityIndexFromIfc(rel.To);
            if (a == InvalidEntityIndex || b == InvalidEntityIndex)
                continue;
            BimDataBuilder.AddRelation(a, b, IfcRelationMapping.ToBos(rel.Kind));
            addedRelations++;
        }
        
        logger?.Log($"Added {addedRelations} relations");
        foreach (var (type, count) in BimDataBuilder.Relations
                     .GroupBy(r => r.RelationType)
                     .Select(g => (g.Key, count: g.Count()))
                     .OrderByDescending(x => x.count))
            logger?.Log($"  {type}: {count}");

        // TEMP:
        logger?.Log($"Found {catIds.Count} unique cat ids");
        var d = CatEntities.ToDictionary(kv => (int)kv.Value, kv => kv.Key);
        var catNames = catIds.Select(ci => d.GetValueOrDefault(ci) ?? "_NOCAT_").Distinct().OrderBy(c => c).ToList();
        foreach (var n in catNames)
            logger?.Log($"Category: {n}");

        logger?.Log($"Computing {PropData.ObjectToPropSets.Count} object-property set pairs");
        foreach (var objectId in PropData.ObjectToPropSets.Keys)
        {
            var bosId = GetBosEntityIndexFromIfc(objectId);
            if (bosId == InvalidEntityIndex)
                continue;

            foreach (var (propSet, p) in PropData.GetDistinctProperties(objectId))
            {
                var propSetName = propSet.Name.DecodeIfc();
                var propName = p.Name.DecodeIfc();
                // A property without a value adds no row: absent is not "".
                if (p.Value.HasValue)
                    ProcessPropValue(propName, p.Value.Value, p, bosId, propSetName);
            }
        }

        logger?.Log($"Computed all parameters. Found {BimDataBuilder.Parameters.Count} parameters and {BimDataBuilder.Descriptors.Count} descriptors");

        logger?.Log("Creating geometry");
        Model3D = IfcFile.ToModel3D();

        var tmp = new HashSet<int>();

        var geometryIdPatch = new MultiDictionary<int, int>();
        foreach (var def in doc.Definitions)
        {
            var g = model.GetGeometry((uint)def.Id);
            if (g != null)
            {
                var n = (int)g.Id;
                // TODO: the following assertion seems pointless. It triggers when I don't expect it. 
                // I wonder what caused it to be an issue?
                //Debug.Assert(n != def.Id);
                geometryIdPatch.Add(n, def.Id);
                tmp.Add(def.Id);
            }
        }

        var cntTotal = 0;
        var cntNotFound = 0;
        var cntNotFound2 = 0;
        foreach (var inst in Model3D.Instances)
        {
            cntTotal++;
            if (!geometryIdPatch.ContainsKey(inst.EntityIndex))
                cntNotFound++;

            if (!tmp.Contains(inst.EntityIndex))
                cntNotFound2++;
        }

        logger?.Log($"Missing {cntNotFound} of {cntTotal}");
        logger?.Log($"Also missing {cntNotFound2} of {cntTotal}");

        Debug.Assert(BimGeometryBuilder.Meshes.Count == 0);
        foreach (var m in Model3D.Meshes)
            BimGeometryBuilder.AddMesh(m);

        var missingIdCount = 0;
        foreach (var inst in Model3D.Instances)
        {
            var matIndex = BimGeometryBuilder.AddMaterial(inst.Material);
            var tfmIndex = BimGeometryBuilder.AddTransform(inst.Matrix4x4);

            if (!geometryIdPatch.TryGetValue(inst.EntityIndex, out var ifcIdList))
            {
                //logger?.Log($"Count not find {inst.EntityIndex} in the geometryIdPatch");
                missingIdCount++;
                continue;
            }

            if (ifcIdList.Count > 1)
            {
                // TODO: this drops the geometry instance entirely, after the material and
                // transform above were already added (leaving unused entries). Instances whose
                // definition maps to multiple IFC ids should instead be emitted once per id,
                // or at least once with the first id.
                logger?.Log($"Found multiple ids for {inst.EntityIndex}");
                continue;
            }

            var ifcId = ifcIdList[0];

            var entityIndex = IfcIdToBosId.GetValueOrDefault(ifcId, InvalidEntityIndex);

            if (entityIndex < 0)
            {
                //logger?.Log($"IfcIdToBosID does not contain {ifcId}");
                //missingIdCount++;
            }

            var entity = IfcFile.EntityResolver.GetEntity(ifcId);
            var flags = IfcClasses.InstanceFlags(entity?.GetEntityName(), inst.Flags);
            BimGeometryBuilder.AddInstance((int)entityIndex, matIndex, inst.MeshIndex, tfmIndex, flags);
        }

        logger?.Log($"Found {missingIdCount} missing IDs");

        logger?.Log("Building geometry");
        BimDataBuilder.Geometry = BimGeometry = BimGeometryBuilder.BuildModel();

        logger?.Log("Building BIM data");
        var bimData = BimDataBuilder.Build();

        logger?.Log("Creating dataset");
        DataSet = bimData.ToDataSet();
    }

    /// <summary>Adds the property's value as a parameter; an unset ($ or *) value, or a reference
    /// to an entity that was not converted, adds nothing.</summary>
    private void ProcessPropValue(string name, StepToken val, IfcPropValue p, EntityIndex bosId, string propSetName)
    {
        if (val.IsUnassignedOrRedeclared)
            return;
        if (val.IsId)
        {
            var refId = GetBosEntityIndexFromIfc(p.Value!.Value.AsId());
            if (refId != InvalidEntityIndex)
                BimDataBuilder.AddParameter(bosId, refId, name, "", propSetName);
        }
        else if (val.IsEntity)
        {
            var (type, val2) = val.AsSimpleEntity(IfcFile.Document);
            ProcessPropValue(name, val2, p, bosId, propSetName);
        }
        else if (val.IsNumber)
        {
            var num = val.AsNumber();
            BimDataBuilder.AddParameter(bosId, num, name, "", propSetName);
        }
        else if (val.IsString)
        {
            var str = val.AsString().DecodeIfc();
            BimDataBuilder.AddParameter(bosId, str, name, "", propSetName);
        }
        else
        {
            BimDataBuilder.AddParameter(bosId, val.ToString()?.DecodeIfc(), name, "", propSetName);
        }
    }

    /// <summary>The attribute's decoded text, or null when it is unset ($), redeclared (*), or
    /// past the entity's attributes. A stored '' is returned as "": it is a value.</summary>
    public static string? OptionalString(IfcEntity entity, int index)
        => index < entity.Attributes.Count && !entity.Attributes[index].IsUnassignedOrRedeclared
            ? entity.GetString(index).DecodeIfc()
            : null;

    /// <summary>The entity's name, chosen as <see cref="IfcEntity.GetEntityLabel"/> chooses it
    /// (IfcSpace.LongName, a material resource's name attribute, then IfcRoot.Name), or null
    /// when that attribute is unset: no "#id" stands in for a missing name.</summary>
    public static string? NameOrNull(IfcEntity entity)
    {
        var ifcClass = entity.GetEntityName();
        if (ifcClass == "IFCSPACE" && OptionalString(entity, 7) is { Length: > 0 } longName)
            return longName;
        if (ifcClass.StartsWith("IFCMATERIAL", StringComparison.Ordinal))
            return IfcEntity.MaterialNameIndex.TryGetValue(ifcClass, out var nameIndex) ? OptionalString(entity, nameIndex) : null;
        return OptionalString(entity, 2);
    }

    /// <summary>See <see cref="IfcClasses.IsMaterialResource"/>.</summary>
    public static bool IsMaterialResource(IfcEntity entity)
        => IfcClasses.IsMaterialResource(entity.GetEntityName());

    /// <summary>Gives each layer of an IfcMaterialLayerSet, and each constituent of an
    /// IfcMaterialConstituentSet, its set and its 1-based position in it, so a query can list a
    /// wall type's layers in order with their thicknesses.</summary>
    private void AddMaterialSetMembers()
    {
        foreach (var set in BosEntities)
        {
            if (IfcMaterialSets.Of(set.GetEntityName()) is not { } materialSet)
                continue;
            var membersAttribute = materialSet.MembersAttribute;
            if (set.Attributes.Count <= membersAttribute || !set.GetAttribute(membersAttribute).IsList)
                continue;

            var setEi = GetBosEntityIndexFromIfc(set.Id);
            var members = set.GetIdList(membersAttribute);
            for (var i = 0; i < members.Length; i++)
            {
                var memberEi = GetBosEntityIndexFromIfc(members[i]);
                if (memberEi == InvalidEntityIndex)
                    continue;
                var group = GetEntity(members[i]).GetEntityName();
                BimDataBuilder.AddParameter(memberEi, setEi, materialSet.SetParameter, "", group);
                BimDataBuilder.AddParameter(memberEi, i + 1, materialSet.IndexParameter, "", group);
            }
        }
    }

    /// <summary>Kept for callers; see <see cref="IfcParameterNames.Attribute"/>.</summary>
    public static string ToIfcStdPropName(string name)
        => IfcParameterNames.Attribute(name);

    private void ProcessAttributeAsProp(IfcEntity entity, IfcAttribute attribute, int attributeIndex, EntityIndex bosId)
    {
        var name = attribute.Name;
        if (IfcParameterNames.NotParameters.Contains(name))
            return;
        var val = entity.GetAttribute(attributeIndex);
        if (val.IsUnassignedOrRedeclared)
            return;

        var ifcPropName = IfcParameterNames.Attribute(name);
        var entityName = entity.GetEntityName();
        if (val.IsId)
        {
            var refId = GetBosEntityIndexFromIfc(val.AsId());
            if (refId < 0)
                return;
            BimDataBuilder.AddParameter(bosId, refId, ifcPropName, "", entityName);
        }
        else if (val.IsEntity)
        {
            return;
        }
        else if (val.IsNumber)
        {
            var num = val.AsNumber();
            BimDataBuilder.AddParameter(bosId, num, ifcPropName, "", entityName);
        }
        else if (val.IsString)
        {
            var str = val.AsString().DecodeIfc();
            if (str == null)
                return;
            BimDataBuilder.AddParameter(bosId, str, ifcPropName, "", entityName);
        }
        else
        {
            var str = val.ToString()?.DecodeIfc();
            if (str == null)
                return;
            BimDataBuilder.AddParameter(bosId, str, ifcPropName, "", entityName);
        }
    }

    public void SaveToBos(FilePath output, ILogger? logger = null)
    {
        // Writing output 

        logger?.Log($"Creating Zip Archive");
        var fs = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        var parquetCompressionMethod = CompressionMethod.Brotli;
        var parquetCompressionLevel = CompressionLevel.Optimal;
        var zipCompressionLevel = CompressionLevel.Fastest;

        logger?.Log($"Writing non-geometry data to Zip file {output}");
        DataSet.WriteParquetToZip(
            zip,
            parquetCompressionMethod,
            parquetCompressionLevel,
            zipCompressionLevel);

        logger?.Log($"Writing geometry data to Zip file {output}");
        BimGeometry.WriteParquetToZip(
            zip,
            parquetCompressionMethod,
            parquetCompressionLevel,
            zipCompressionLevel);

        logger?.Log($"Completed");
    }
}