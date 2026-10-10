namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes one prim per BOS entity, in entity order. An entity with geometry is an Xform of kind
/// component holding its instances; an entity without geometry (a storey, a space with no
/// shape, a type, a property set) is a Scope, which has no transform and is not drawn. Both
/// carry the entity's identity and parameters as bim: properties and its relations as bim:
/// relationships. Instances whose entity is missing go under /Model/Unassigned. Then
/// /Model/Descriptors declares each parameter property that was written. Parameters and
/// relations are grouped by entity once, so the pass is linear in entities, instances,
/// parameters, and relations.
/// </summary>
internal sealed class UsdEntities
{
    private readonly UsdaWriter _w;
    private readonly IBimData _data;
    private readonly BimGeometry _g;
    private readonly string[] _primNames;
    private readonly ParameterAttribute?[] _attributes;
    private readonly RowGroups _parametersByEntity;
    private readonly UsdRelations _relations;

    // _writtenFor[d] == entity + 1 when descriptor d already has a property on that entity's
    // prim; a second value for the same descriptor would be a duplicate property in USD.
    private readonly int[] _writtenFor;

    // _described[d] is true once descriptor d has a value on some entity, so /Model/Descriptors declares it.
    private readonly bool[] _described;

    private int _elements, _instances, _unassigned, _withoutMesh, _attributeCount, _withoutValue, _duplicates;

    private UsdEntities(UsdaWriter w, IBimData data, BimGeometry g)
    {
        _w = w;
        _data = data;
        _g = g;
        _primNames = EntityPrimNames.ForEntities(data);
        _attributes = ParameterAttribute.ForDescriptors(data);
        _parametersByEntity = new RowGroups(data.Entities.Length, data.Parameters.Length, p => (int)data.Parameters[p].Entity);
        _relations = new UsdRelations(data, _primNames);
        _writtenFor = new int[data.Descriptors.Length];
        _described = new bool[data.Descriptors.Length];
    }

    /// <summary>Writes every entity prim, the Unassigned prim (when needed), and the
    /// Descriptors scope as children of the open root prim, and returns the counts the
    /// summary reports.</summary>
    public static UsdExportSummary Write(UsdaWriter w, IBimData data, BimGeometry g, int materials, int prototypes)
    {
        var e = new UsdEntities(w, data, g);
        e.WriteAll();
        var described = e._attributes.Where((a, d) => a is not null && e._described[d]).Select(a => a!);
        var descriptors = ParameterAttribute.WriteDescriptors(w, described);
        return new UsdExportSummary(materials, prototypes, descriptors, data.Entities.Length, e._elements,
            e._instances, e._unassigned, e._withoutMesh, e._attributeCount, e._relations.Targets,
            e._relations.LeftOut, e._withoutValue, e._duplicates);
    }

    private void WriteAll()
    {
        var instanceCount = _g.GetNumInstances();
        var entityCount = _data.Entities.Length;
        var byEntity = new RowGroups(entityCount, instanceCount,
            i => UsdGeometry.HasMesh(_g, i) ? _g.InstanceEntityIndex[i] : -1);

        for (var e = 0; e < entityCount; e++)
            WriteEntity(e, byEntity.Rows(e));

        var unassigned = new List<int>();
        for (var i = 0; i < instanceCount; i++)
        {
            if (!UsdGeometry.HasMesh(_g, i))
                _withoutMesh++;
            else if ((uint)_g.InstanceEntityIndex[i] >= (uint)entityCount)
                unassigned.Add(i);
        }
        if (unassigned.Count > 0)
        {
            _w.Line().Text("def Xform \"").Text(UsdNames.Unassigned).Text('"').End().Open();
            foreach (var i in unassigned)
                UsdGeometry.WriteInstance(_w, _g, i);
            _w.Close();
            _instances += unassigned.Count;
            _unassigned = unassigned.Count;
        }
    }

    private void WriteEntity(int entityIndex, ReadOnlySpan<int> instances)
    {
        var entity = _data.Entities[entityIndex];
        var hasGeometry = instances.Length > 0;
        if (hasGeometry)
        {
            _w.Line().Text("def Xform \"").Text(_primNames[entityIndex]).Text('"').OpenMetadata();
            _w.Line("kind = \"component\"");
            _w.CloseMetadata().Open();
        }
        else
            _w.Line().Text("def Scope \"").Text(_primNames[entityIndex]).Text('"').End().Open();

        _w.Line().Text("custom int ").Text(UsdNames.EntityIndexAttribute).Text(" = ").Int(entityIndex).End();
        _attributeCount++;
        // BimDataBuilder.AddEntity() marks "no local id" with -1.
        if (entity.LocalId >= 0)
        {
            _w.Line().Text("custom int64 ").Text(UsdNames.LocalIdAttribute).Text(" = ").Int(entity.LocalId).End();
            _attributeCount++;
        }
        WriteString(UsdNames.GlobalIdAttribute, BosValues.NonEmptyString(_data, entity.GlobalId));
        WriteString(UsdNames.NameAttribute, BosValues.NonEmptyString(_data, entity.Name));
        WriteString(UsdNames.CategoryAttribute, BosValues.EntityName(_data, entity.Category));
        WriteString(UsdNames.TypeAttribute, BosValues.EntityName(_data, entity.Type));
        WriteString(UsdNames.DocumentAttribute, BosValues.DocumentTitle(_data, entity.Document));
        foreach (var p in _parametersByEntity.Rows(entityIndex))
            WriteParameter(entityIndex, _data.Parameters[p]);
        _relations.Write(_w, entityIndex);

        foreach (var i in instances)
            UsdGeometry.WriteInstance(_w, _g, i);
        _w.Close();

        if (hasGeometry)
            _elements++;
        _instances += instances.Length;
    }

    private void WriteString(string attribute, string? value)
    {
        if (value is null)
            return;
        _w.Line().Text("custom string ").Text(attribute).Text(" = ").Quoted(value).End();
        _attributeCount++;
    }

    private void WriteParameter(int entityIndex, Parameter p)
    {
        if ((uint)p.Descriptor >= (uint)_attributes.Length || _attributes[(int)p.Descriptor] is not { } a)
        {
            _withoutValue++;
            return;
        }
        if (_writtenFor[(int)p.Descriptor] == entityIndex + 1)
        {
            _duplicates++;
            return;
        }
        if (!HasValue(a.Type, p.Value))
        {
            _withoutValue++;
            return;
        }
        _writtenFor[(int)p.Descriptor] = entityIndex + 1;
        _described[(int)p.Descriptor] = true;
        _attributeCount++;

        _w.Line().Text("custom ").Text(a.UsdType).Text(' ').Text(a.Name).Text(" = ");
        WriteValue(a.Type, p.Value);
        _w.End();
    }

    private bool HasValue(ParameterType type, int value) => type switch
    {
        ParameterType.Int => true,
        ParameterType.Number => BosValues.Number(_data, (NumberIndex)value).HasValue,
        ParameterType.String => BosValues.String(_data, (StringIndex)value) is not null,
        ParameterType.Entity => BosValues.Entity(_data, (EntityIndex)value).HasValue,
        ParameterType.Point => BosValues.Point(_data, (PointIndex)value).HasValue,
        _ => false,
    };

    private void WriteValue(ParameterType type, int value)
    {
        switch (type)
        {
            case ParameterType.Int:
                _w.Int(value);
                break;
            case ParameterType.Number:
                _w.Float(_data.Numbers[value]);
                break;
            case ParameterType.String:
                _w.Quoted(_data.Strings[value]);
                break;
            case ParameterType.Entity:
                _w.PathRef(UsdNames.EntityPathPrefix, _primNames[value]);
                break;
            case ParameterType.Point:
                var pt = _data.Points[value];
                _w.Float3(pt.X, pt.Y, pt.Z);
                break;
        }
    }
}
