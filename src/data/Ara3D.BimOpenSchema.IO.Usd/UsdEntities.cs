namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes one prim per BOS entity, in entity order. An entity with geometry is an Xform of kind
/// component holding its instances; an entity without geometry (a storey, a space with no
/// shape, a type, a property set) is a Scope, which has no transform and is not drawn. Both
/// carry the entity's identity and parameters as bim: properties and its relations as bim:
/// relationships. Instances whose entity is missing go under /Model/Unassigned. Then
/// /Model/Descriptors declares each parameter property that was written. The
/// <see cref="BosScene"/> groups instances, parameters, and relations by entity once, so the
/// pass is linear in entities, instances, parameters, and relations.
/// </summary>
internal sealed class UsdEntities
{
    private readonly UsdaWriter _w;
    private readonly BosScene _scene;
    private readonly IBimData _data;
    private readonly string[] _primNames;
    private readonly ParameterAttribute?[] _attributes;
    private readonly UsdRelations _relations;

    // _writtenFor[d] == entity + 1 when descriptor d already has a property on that entity's
    // prim; a second value for the same descriptor would be a duplicate property in USD.
    private readonly int[] _writtenFor;

    // _described[d] is true once descriptor d has a value on some entity, so /Model/Descriptors declares it.
    private readonly bool[] _described;

    private int _elements, _instances, _unassigned, _attributeCount, _withoutValue, _duplicates;

    private UsdEntities(UsdaWriter w, BosScene scene)
    {
        _w = w;
        _scene = scene;
        _data = scene.Data;
        _primNames = EntityPrimNames.ForEntities(scene);
        _attributes = ParameterAttribute.ForDescriptors(scene);
        _relations = new UsdRelations(scene, _primNames);
        _writtenFor = new int[_data.Descriptors.Length];
        _described = new bool[_data.Descriptors.Length];
    }

    /// <summary>Writes every entity prim, the Unassigned prim (when needed), and the
    /// Descriptors scope as children of the open root prim, and returns the counts the
    /// summary reports.</summary>
    public static UsdExportSummary Write(UsdaWriter w, BosScene scene, int materials, int prototypes)
    {
        var e = new UsdEntities(w, scene);
        e.WriteAll();
        var described = e._attributes.Where((a, d) => a is not null && e._described[d]).Select(a => a!);
        var descriptors = ParameterAttribute.WriteDescriptors(w, described);
        return new UsdExportSummary(materials, prototypes, descriptors, scene.EntityCount, e._elements,
            e._instances, e._unassigned, scene.InstancesWithBadMesh, e._attributeCount, e._relations.Targets,
            e._relations.LeftOut, e._withoutValue, e._duplicates, scene.InstancesWithBadTransform);
    }

    private void WriteAll()
    {
        for (var e = 0; e < _scene.EntityCount; e++)
            WriteEntity(e, _scene.InstancesOf(e));

        var unassigned = _scene.UnassignedInstances;
        if (unassigned.Length > 0)
        {
            _w.Line().Text("def Xform \"").Text(UsdNames.Unassigned).Text('"').End().Open();
            foreach (var i in unassigned)
                UsdGeometry.WriteInstance(_w, _scene, i);
            _w.Close();
            _instances += unassigned.Length;
            _unassigned = unassigned.Length;
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
        WriteString(UsdNames.GlobalIdAttribute, _scene.GlobalId(entityIndex));
        WriteString(UsdNames.NameAttribute, _scene.Name(entityIndex));
        WriteString(UsdNames.CategoryAttribute, _scene.Name((int)entity.Category));
        WriteString(UsdNames.TypeAttribute, _scene.Name((int)entity.Type));
        WriteString(UsdNames.DocumentAttribute, _scene.DocumentTitle(entity.Document));
        foreach (var p in _scene.ParametersOf(entityIndex))
            WriteParameter(entityIndex, _data.Parameters[p]);
        _relations.Write(_w, entityIndex);

        foreach (var i in instances)
            UsdGeometry.WriteInstance(_w, _scene, i);
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
        if (!_scene.HasValue(p))
        {
            _withoutValue++;
            return;
        }
        _writtenFor[(int)p.Descriptor] = entityIndex + 1;
        _described[(int)p.Descriptor] = true;
        _attributeCount++;

        _w.Line().Text("custom ").Text(a.UsdType).Text(' ').Text(a.Name).Text(" = ");
        WriteValue(a.Type, p);
        _w.End();
    }

    /// <summary>Writes a value that <see cref="BosScene.HasValue"/> has found present.</summary>
    private void WriteValue(ParameterType type, Parameter p)
    {
        switch (type)
        {
            case ParameterType.Int:
                _w.Int(_scene.IntValue(p)!.Value);
                break;
            case ParameterType.Number:
                _w.Float(_scene.NumberValue(p)!.Value);
                break;
            case ParameterType.String:
                _w.Quoted(_scene.StringValue(p)!);
                break;
            case ParameterType.Entity:
                _w.PathRef(UsdNames.EntityPathPrefix, _primNames[_scene.EntityValue(p)!.Value]);
                break;
            case ParameterType.Point:
                var pt = _scene.PointValue(p)!.Value;
                _w.Float3(pt.X, pt.Y, pt.Z);
                break;
        }
    }
}
