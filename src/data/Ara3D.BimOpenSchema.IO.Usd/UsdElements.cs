namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes one Xform prim per BOS entity that has geometry, in entity order: its identity and
/// parameters as bim: attributes, then its instances. Instances whose entity is missing go
/// under /Model/Unassigned. Then /Model/Descriptors declares each parameter attribute that
/// was written. Parameters are grouped by entity once, so the pass is linear in entities,
/// instances, and parameters.
/// </summary>
internal sealed class UsdElements
{
    private readonly UsdaWriter _w;
    private readonly IBimData _data;
    private readonly BimGeometry _g;
    private readonly ParameterAttribute?[] _attributes;
    private readonly RowGroups _parametersByEntity;

    // _writtenFor[d] == entity + 1 when descriptor d already has an attribute on that entity's
    // prim; a second value for the same descriptor would be a duplicate attribute in USD.
    private readonly int[] _writtenFor;

    // _described[d] is true once descriptor d has a value on some element, so /Model/Descriptors declares it.
    private readonly bool[] _described;
    private readonly HashSet<string> _primNames = new(StringComparer.Ordinal);

    private int _elements, _instances, _unassigned, _withoutMesh, _attributeCount, _withoutValue, _duplicates;

    private UsdElements(UsdaWriter w, IBimData data, BimGeometry g)
    {
        _w = w;
        _data = data;
        _g = g;
        _attributes = ParameterAttribute.ForDescriptors(data);
        _parametersByEntity = new RowGroups(data.Entities.Length, data.Parameters.Length, p => (int)data.Parameters[p].Entity);
        _writtenFor = new int[data.Descriptors.Length];
        _described = new bool[data.Descriptors.Length];
    }

    /// <summary>Writes every element prim, the Unassigned prim (when needed), and the
    /// Descriptors scope as children of the open root prim, and returns the counts the
    /// summary reports.</summary>
    public static UsdExportSummary Write(UsdaWriter w, IBimData data, BimGeometry g, int materials, int prototypes)
    {
        var e = new UsdElements(w, data, g);
        e.WriteAll();
        var described = e._attributes.Where((a, d) => a is not null && e._described[d]).Select(a => a!);
        var descriptors = ParameterAttribute.WriteDescriptors(w, described);
        return new UsdExportSummary(materials, prototypes, descriptors, e._elements, e._instances, e._unassigned,
            e._withoutMesh, e._attributeCount, e._withoutValue, e._duplicates);
    }

    private void WriteAll()
    {
        var instanceCount = _g.GetNumInstances();
        var entityCount = _data.Entities.Length;
        var byEntity = new RowGroups(entityCount, instanceCount,
            i => UsdGeometry.HasMesh(_g, i) ? _g.InstanceEntityIndex[i] : -1);

        for (var e = 0; e < entityCount; e++)
        {
            var instances = byEntity.Rows(e);
            if (instances.Length > 0)
                WriteElement(e, instances);
        }

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

    private void WriteElement(int entityIndex, ReadOnlySpan<int> instances)
    {
        var entity = _data.Entities[entityIndex];
        _w.Line().Text("def Xform \"").Text(PrimName(entityIndex, entity)).Text('"').OpenMetadata();
        _w.Line("kind = \"component\"");
        _w.CloseMetadata().Open();

        _w.Line().Text("custom int ").Text(UsdNames.EntityIndexAttribute).Text(" = ").Int(entityIndex).End();
        _w.Line().Text("custom int64 ").Text(UsdNames.LocalIdAttribute).Text(" = ").Int(entity.LocalId).End();
        _attributeCount += 2;
        WriteString(UsdNames.GlobalIdAttribute, BosValues.NonEmptyString(_data, entity.GlobalId));
        WriteString(UsdNames.NameAttribute, BosValues.NonEmptyString(_data, entity.Name));
        WriteString(UsdNames.CategoryAttribute, BosValues.EntityName(_data, entity.Category));
        WriteString(UsdNames.TypeAttribute, BosValues.EntityName(_data, entity.Type));
        WriteString(UsdNames.DocumentAttribute, BosValues.DocumentTitle(_data, entity.Document));
        foreach (var p in _parametersByEntity.Rows(entityIndex))
            WriteParameter(entityIndex, _data.Parameters[p]);

        foreach (var i in instances)
            UsdGeometry.WriteInstance(_w, _g, i);
        _w.Close();

        _elements++;
        _instances += instances.Length;
    }

    /// <summary>E_{GlobalId} made an identifier, or E{entity index} when the GlobalId is
    /// missing or its identifier is taken (duplicate GlobalIds occur in federated models).
    /// The two forms cannot collide: the second character is "_" in one and a digit in the other.</summary>
    private string PrimName(int entityIndex, Entity entity)
    {
        if (BosValues.NonEmptyString(_data, entity.GlobalId) is { } globalId)
        {
            var name = UsdNames.ToIdentifier(UsdNames.ElementPrefix + "_" + globalId);
            if (_primNames.Add(name))
                return name;
        }
        return UsdNames.ElementPrefix + entityIndex;
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
        _attributeCount++;

        _described[(int)p.Descriptor] = true;

        _w.Line().Text("custom ").Text(a.UsdType).Text(' ').Text(a.Name).Text(" = ");
        WriteValue(a.Type, p.Value);
        _w.End();
    }

    private bool HasValue(ParameterType type, int value) => type switch
    {
        ParameterType.Int => true,
        ParameterType.Number => BosValues.Number(_data, (NumberIndex)value).HasValue,
        ParameterType.String => BosValues.String(_data, (StringIndex)value) is not null,
        ParameterType.Entity => BosValues.EntityName(_data, (EntityIndex)value) is not null,
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
                _w.Quoted(BosValues.EntityName(_data, (EntityIndex)value)!);
                break;
            case ParameterType.Point:
                var pt = _data.Points[value];
                _w.Float3(pt.X, pt.Y, pt.Z);
                break;
        }
    }
}
