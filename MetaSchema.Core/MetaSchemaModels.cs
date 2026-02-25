using System.Xml.Linq;
using Meta.Core.Domain;
using Meta.Core.Serialization;

namespace MetaSchema.Core;

public static class MetaSchemaModels
{
    public const string SchemaCatalogModelName = "SchemaCatalog";
    public const string TypeConversionCatalogModelName = "TypeConversionCatalog";
    private const string SchemaCatalogModelResourceName = "MetaSchema.Core.Models.SchemaCatalog.model.xml";
    private const string TypeConversionCatalogModelResourceName = "MetaSchema.Core.Models.TypeConversionCatalog.model.xml";

    public static GenericModel CreateSchemaCatalogModel()
    {
        return LoadModel(SchemaCatalogModelResourceName, SchemaCatalogModelName);
    }

    public static GenericModel CreateTypeConversionCatalogModel()
    {
        return LoadModel(TypeConversionCatalogModelResourceName, TypeConversionCatalogModelName);
    }

    private static GenericModel LoadModel(string resourceName, string expectedModelName)
    {
        var assembly = typeof(MetaSchemaModels).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Could not load embedded sanctioned model resource '{resourceName}'.");
        var document = XDocument.Load(stream, LoadOptions.None);
        var model = ModelXmlCodec.Load(document);
        if (!string.Equals(model.Name, expectedModelName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Sanctioned model name '{model.Name}' from resource '{resourceName}' does not match expected '{expectedModelName}'.");
        }

        return model;
    }
}

