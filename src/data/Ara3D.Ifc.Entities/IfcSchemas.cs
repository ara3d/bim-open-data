using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.IfcLoader;

/// <summary>Which IFC schema a STEP document's header names.</summary>
public static class IfcSchemas
{
    /// <summary>IFC4X3, IFC4, else IFC2X3.</summary>
    public static IfcSchema Of(StepDocument document)
    {
        var fileSchema = document.Header.FileSchema;
        if (fileSchema.StartsWith("IFC4X3", StringComparison.InvariantCultureIgnoreCase))
            return IfcSchema.GetSchema(IfcSchemaEnum.Ifc4x3);
        if (fileSchema.StartsWith("IFC4", StringComparison.InvariantCultureIgnoreCase))
            return IfcSchema.GetSchema(IfcSchemaEnum.Ifc4);
        return IfcSchema.GetSchema(IfcSchemaEnum.Ifc2x3);
    }
}
