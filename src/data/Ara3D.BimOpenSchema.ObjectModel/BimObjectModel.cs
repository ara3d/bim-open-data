using Ara3D.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ara3D.BimOpenSchema
{
    /// <summary>
    /// The object model is a convenient and denormalized representation of the data as objects.
    /// It is not canonical and is designed for common programming tasks.
    /// </summary>
    public class BimObjectModel
    {
        public IBimData Data;
        public IModel3D Model3D;

        private bool _parametersComputed;

        public List<DocumentModel> Documents { get; } = new();
        public List<EntityModel> Entities { get; } = new();
        public List<DescriptorModel> Descriptors { get; } = new();

        public HashSet<EntityIndex> TypeEntityIndices { get; } = new();
        public HashSet<EntityIndex> CategoryEntityIndices { get; } = new();

        public BimObjectModel(IBimData data, bool computeParametersAndRelations)
            : this(data, data.Geometry.ToModel3D(), computeParametersAndRelations)
        { }

        public BimObjectModel(IBimData data, IModel3D model, bool computeParametersAndRelations)
        {
            Data = data;
            Model3D = model;

            foreach (var d in data.Documents)
                Documents.Add(new DocumentModel
                {
                    Path = Get(d.Path),
                    Title = Data.Label(d.Title)
                });

            foreach (var e in Data.Entities)
            {
                if (e.Type >= 0)
                    TypeEntityIndices.Add(e.Type);
                if (e.Category >= 0)
                    CategoryEntityIndices.Add(e.Category);
            }

            Entities = data.
                EntityIndices().Select(Create).ToList();

            if (Entities.Count > 0)
            {
                var numElements = Model3D.Instances.Count;
                for (var i = 0; i < numElements; i++)
                {
                    var inst = Model3D.Instances[i];
                    var entityIndex = inst.EntityIndex;
                    Get((EntityIndex)entityIndex)?.AddInstance(inst);
                }
            }

            Descriptors.AddRange(Data.DescriptorIndices().Select(di => Create(di, Data.Get(di) ?? default)));

            if (computeParametersAndRelations)
                ComputeParametersAndRelations();
        }

        public void ComputeParametersAndRelations()
        {
            // This is probably a mistake
            Debug.Assert(!_parametersComputed);
            _parametersComputed = true;

            foreach (var p in Data.Parameters)
                if (Create(p) is { } pm)
                    AddParameter(pm);

            foreach (var r in Data.Relations)
            {
                var source = Get(r.EntityA);
                var target = Get(r.EntityB);
                if (source == null || target == null)
                    continue;
                source.OutgoingRelations.Add(new RelationModel(r.RelationType, target));
                target.IncomingRelations.Add(new RelationModel(r.RelationType, source));
            }
        }

        public EntityModel Create(EntityIndex ei)
            => new(this, ei, TypeEntityIndices.Contains(ei), CategoryEntityIndices.Contains(ei));

        public DescriptorModel Create(DescriptorIndex index, ParameterDescriptor desc) => new DescriptorModel
        {
            Index = index,
            Name = Data.Get(desc.Name),
            Units = Data.Get(desc.Units),
            Group = Data.Get(desc.Group),
            ParameterType = desc.Type
        };

        /// <summary>The entity, or null when the index is -1 (absent) or outside the table.</summary>
        public EntityModel? Get(EntityIndex ei) => (uint)ei < (uint)Entities.Count ? Entities[(int)ei] : null;
        public DescriptorModel? Get(DescriptorIndex di) => (uint)di < (uint)Descriptors.Count ? Descriptors[(int)di] : null;
        public Point? Get(PointIndex pi) => Data.Get(pi);
        public string? Get(StringIndex si) => Data.Get(si);
        public float? Get(NumberIndex ni) => Data.Get(ni);

        /// <summary>Adds the parameter to its entity. A descriptor without a name is kept in
        /// Parameters but has no key in ParameterValues.</summary>
        public void AddParameter(ParameterModel pm)
        {
            if (pm.Descriptor.Name is { } name)
                pm.Entity.ParameterValues[name] = pm.Value;
            pm.Entity.Parameters.Add(pm);
        }

        /// <summary>The value, or null when the descriptor or the value is absent.</summary>
        public object? GetParameterValue(Parameter p)
            => Get(p.Descriptor) is { } d ? GetParameterValue(d.ParameterType, p.Value) : null;

        public object? GetParameterValue(ParameterType pt, int value)
        {
            return pt switch
            {
                ParameterType.Int => value,
                ParameterType.String => Get((StringIndex)value),
                ParameterType.Number => Get((NumberIndex)value),
                ParameterType.Entity => Get((EntityIndex)value),
                ParameterType.Point => Get((PointIndex)value)?.ToString(),
                _ => null
            };
        }


        /// <summary>The parameter's model, or null when its entity or descriptor is absent.</summary>
        public ParameterModel? Create(Parameter p)
            => Get(p.Entity) is { } e && Get(p.Descriptor) is { } d
                ? new(GetParameterValue(d.ParameterType, p.Value), e, d)
                : null;
    }

    public class EntityModel
    {
        public EntityModel(BimObjectModel model, EntityIndex ei, bool isType, bool isCategory)
        {
            Model = model;
            Index = ei;
            IsType = isType;
            IsCategory = isCategory;
        }

        // Stored data
        public BimObjectModel Model { get;  }
        public EntityIndex Index { get; }

        public bool IsType { get; }
        public bool IsCategory { get; }
        public bool IsTypeInstance => HasType;
        public bool IsNotTypeOrCategory => !IsType && !IsCategory;

        public List<InstanceStruct> Instances { get; } = new();

        // Always accessible data
        public IBimData Data => Model.Data;
        public Entity Entity => Model.Data.Get(Index) ?? default;
        public DocumentModel? Document => Model.Documents.ElementAtOrDefault((int)Entity.Document);
        public string? DocumentTitle => Document?.Title;
        public long LocalId => Entity.LocalId;
        /// <summary>Null when absent; "" stored by files written before -1 meant absent also reads as null.</summary>
        public string? GlobalId => Data.Label(Entity.GlobalId);
        public string? Category => GetEntityModel(Entity.Category)?.Name;
        public string? BuiltInCategory => GetEntityModel(Entity.Category)?.GetParameterAsString(CommonRevitParameters.CategoryBuiltInType);
        /// <summary>Null when absent; "" stored by files written before -1 meant absent also reads as null.</summary>
        public string? Name => Data.Label(Entity.Name);
        public bool HasGeometry => Instances.Count > 0;
        public bool HasType => Entity.Type >= 0;

        public EntityModel? GetEntityModel(EntityIndex ei)
            => Model.Get(ei);

        // Commonly present data stored in parameters
        public string? CategoryType => GetEntityModel(Entity.Category)?.GetParameterAsString(CommonRevitParameters.CategoryCategoryType);
        public string? ClassName => GetParameterAsString(CommonRevitParameters.ObjectTypeName);
        public string? LevelName => GetParameterAsEntity(CommonRevitParameters.ElementLevel)?.Name;
        public string? GroupName => GetParameterAsEntity(CommonRevitParameters.ElementGroup)?.Name;
        public string? AssemblyName => GetParameterAsEntity(CommonRevitParameters.ElementAssemblyInstance)?.Index.ToString();
        public int? WorksetId => GetParameterAsInt(CommonRevitParameters.ElementWorksetId);
        /// <summary>The level's elevation, or null when the element has no level or the level no elevation.</summary>
        public float? Elevation => GetParameterAsEntity(CommonRevitParameters.ElementLevel)?.GetParameterAsNumber(CommonRevitParameters.LevelElevation);
        public string? TypeName => Type?.Name;
        public EntityModel? Type => GetEntityModel(Entity.Type);

        // Family instance parameters
        public string? RoomName => GetParameterAsEntity(CommonRevitParameters.FISpace)?.Name;

        /// <summary>Values by descriptor name; a null value is a parameter whose value is absent.</summary>
        public Dictionary<string, object?> ParameterValues { get; } = new();
        public List<RelationModel> OutgoingRelations { get; } = new();
        public List<RelationModel> IncomingRelations { get; } = new();
        public List<ParameterModel> Parameters { get; } = new();

        public override string ToString()
            => $"{Name}(#{LocalId})";

        // Each getter returns null when the parameter is missing, its value is absent,
        // or its value has another type.

        public int? GetParameterAsInt(string name)
            => ParameterValues.GetValueOrDefault(name) as int?;

        public string? GetParameterAsString(string name)
            => ParameterValues.GetValueOrDefault(name) as string;

        public float? GetParameterAsNumber(string name)
            => ParameterValues.GetValueOrDefault(name) as float?;

        public EntityModel? GetParameterAsEntity(string name)
            => ParameterValues.GetValueOrDefault(name) as EntityModel;

        public void AddInstance(InstanceStruct inst)
        {
            Instances.Add(inst);
        }
    }

    public class RelationModel
    {
        public RelationType RelationType { get; init; }
        public EntityModel Target { get; init; }

        public RelationModel(RelationType type, EntityModel target)
        {
            RelationType = type;
            Target = target;
        }
    }

    public class ParameterModel
    {
        /// <summary>Null when the value is absent.</summary>
        public object? Value { get; }
        public EntityModel Entity { get; }
        public DescriptorModel Descriptor { get; }

        public ParameterModel(object? value, EntityModel entity, DescriptorModel descriptor)
        {
            Value = value;
            Entity = entity;
            Descriptor = descriptor;
        }

        public override string ToString()
            => $"{Descriptor.Name} ({Descriptor.ParameterType}) = {Value}";
    }

    public class DocumentModel
    {
        public string? Path { get; init; }
        /// <summary>Null when absent or "" (files written before -1 meant absent).</summary>
        public string? Title { get; init; }
    }

    public class DescriptorModel
    {
        public DescriptorIndex Index { get; init; }
        public string? Name { get; init; }
        public string? Units { get; init; }
        public string? Group { get; init; }
        public ParameterType ParameterType { get; init; }
    }
}
