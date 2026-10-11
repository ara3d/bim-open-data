namespace Ara3D.Ifc.Mesher.Tests;

/// <summary>Links a test method to a geometry-creation entity covered by the coverage gate.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class GeometryCoverageAttribute(string entityName) : Attribute
{
    public string EntityName { get; } = entityName;
}
