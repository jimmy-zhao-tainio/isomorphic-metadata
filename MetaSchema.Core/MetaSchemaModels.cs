using MetadataStudio.Core.Domain;

namespace MetaSchema.Core;

public static class MetaSchemaModels
{
    public const string SchemaCatalogModelName = "SchemaCatalog";
    public const string TypeConversionCatalogModelName = "TypeConversionCatalog";

    public static ModelDefinition CreateSchemaCatalogModel()
    {
        var model = new ModelDefinition
        {
            Name = SchemaCatalogModelName,
        };

        var system = new EntityDefinition
        {
            Name = "System",
        };
        system.Properties.Add(new PropertyDefinition { Name = "Name" });
        system.Properties.Add(new PropertyDefinition { Name = "Description", IsNullable = true });

        var schema = new EntityDefinition
        {
            Name = "Schema",
        };
        schema.Properties.Add(new PropertyDefinition { Name = "Name" });
        schema.Relationships.Add(new RelationshipDefinition { Entity = "System" });

        var table = new EntityDefinition
        {
            Name = "Table",
        };
        table.Properties.Add(new PropertyDefinition { Name = "Name" });
        table.Properties.Add(new PropertyDefinition { Name = "ObjectType", IsNullable = true });
        table.Relationships.Add(new RelationshipDefinition { Entity = "Schema" });

        var fieldType = new EntityDefinition
        {
            Name = "FieldType",
        };
        fieldType.Properties.Add(new PropertyDefinition { Name = "Name" });
        fieldType.Properties.Add(new PropertyDefinition { Name = "Family", IsNullable = true });
        fieldType.Properties.Add(new PropertyDefinition { Name = "IsNative", IsNullable = true });

        var field = new EntityDefinition
        {
            Name = "Field",
        };
        field.Properties.Add(new PropertyDefinition { Name = "Name" });
        field.Properties.Add(new PropertyDefinition { Name = "Ordinal", IsNullable = true });
        field.Properties.Add(new PropertyDefinition { Name = "IsNullable", IsNullable = true });
        field.Properties.Add(new PropertyDefinition { Name = "Length", IsNullable = true });
        field.Properties.Add(new PropertyDefinition { Name = "NumericPrecision", IsNullable = true });
        field.Properties.Add(new PropertyDefinition { Name = "Scale", IsNullable = true });
        field.Relationships.Add(new RelationshipDefinition { Entity = "Table" });
        field.Relationships.Add(new RelationshipDefinition { Entity = "FieldType" });

        model.Entities.Add(system);
        model.Entities.Add(schema);
        model.Entities.Add(table);
        model.Entities.Add(fieldType);
        model.Entities.Add(field);

        return model;
    }

    public static ModelDefinition CreateTypeConversionCatalogModel()
    {
        var model = new ModelDefinition
        {
            Name = TypeConversionCatalogModelName,
        };

        var typeSystem = new EntityDefinition
        {
            Name = "TypeSystem",
        };
        typeSystem.Properties.Add(new PropertyDefinition { Name = "Name" });

        var dataType = new EntityDefinition
        {
            Name = "DataType",
        };
        dataType.Properties.Add(new PropertyDefinition { Name = "Name" });
        dataType.Properties.Add(new PropertyDefinition { Name = "Category", IsNullable = true });
        dataType.Relationships.Add(new RelationshipDefinition { Entity = "TypeSystem" });

        var facet = new EntityDefinition
        {
            Name = "Facet",
        };
        facet.Properties.Add(new PropertyDefinition { Name = "Name" });
        facet.Properties.Add(new PropertyDefinition { Name = "ValueKind" });

        var dataTypeFacet = new EntityDefinition
        {
            Name = "DataTypeFacet",
        };
        dataTypeFacet.Properties.Add(new PropertyDefinition { Name = "IsSupported", DataType = "bool" });
        dataTypeFacet.Properties.Add(new PropertyDefinition { Name = "IsRequired", DataType = "bool" });
        dataTypeFacet.Properties.Add(new PropertyDefinition { Name = "DefaultInt", DataType = "int", IsNullable = true });
        dataTypeFacet.Properties.Add(new PropertyDefinition { Name = "DefaultBool", DataType = "bool", IsNullable = true });
        dataTypeFacet.Relationships.Add(new RelationshipDefinition { Entity = "DataType" });
        dataTypeFacet.Relationships.Add(new RelationshipDefinition { Entity = "Facet" });

        var typeSpec = new EntityDefinition
        {
            Name = "TypeSpec",
        };
        typeSpec.Properties.Add(new PropertyDefinition { Name = "Length", DataType = "int", IsNullable = true });
        typeSpec.Properties.Add(new PropertyDefinition { Name = "Precision", DataType = "int", IsNullable = true });
        typeSpec.Properties.Add(new PropertyDefinition { Name = "Scale", DataType = "int", IsNullable = true });
        typeSpec.Properties.Add(new PropertyDefinition { Name = "TimePrecision", DataType = "int", IsNullable = true });
        typeSpec.Properties.Add(new PropertyDefinition { Name = "IsUnicode", DataType = "bool", IsNullable = true });
        typeSpec.Properties.Add(new PropertyDefinition { Name = "IsFixedLength", DataType = "bool", IsNullable = true });
        typeSpec.Relationships.Add(new RelationshipDefinition { Entity = "DataType" });

        var setting = new EntityDefinition
        {
            Name = "Setting",
        };
        setting.Properties.Add(new PropertyDefinition { Name = "Name" });
        setting.Properties.Add(new PropertyDefinition { Name = "DefaultValue" });

        var conversionImplementation = new EntityDefinition
        {
            Name = "ConversionImplementation",
        };
        conversionImplementation.Properties.Add(new PropertyDefinition { Name = "Key" });
        conversionImplementation.Properties.Add(new PropertyDefinition { Name = "Kind" });
        conversionImplementation.Properties.Add(new PropertyDefinition { Name = "CSharpEntryPoint" });

        var typeMapping = new EntityDefinition
        {
            Name = "TypeMapping",
        };
        typeMapping.Properties.Add(new PropertyDefinition { Name = "Name" });
        typeMapping.Properties.Add(new PropertyDefinition { Name = "Priority", DataType = "int" });
        typeMapping.Properties.Add(new PropertyDefinition { Name = "Lossiness" });
        typeMapping.Properties.Add(new PropertyDefinition { Name = "IsImplicit", DataType = "bool" });
        typeMapping.Properties.Add(new PropertyDefinition { Name = "Notes", IsNullable = true });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "TypeSystem", Name = "SourceTypeSystem" });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "TypeSystem", Name = "TargetTypeSystem" });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "DataType", Name = "SourceDataType" });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "DataType", Name = "TargetDataType" });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "ConversionImplementation" });
        typeMapping.Relationships.Add(new RelationshipDefinition { Entity = "Setting" });

        var typeMappingCondition = new EntityDefinition
        {
            Name = "TypeMappingCondition",
        };
        typeMappingCondition.Properties.Add(new PropertyDefinition { Name = "Operator" });
        typeMappingCondition.Properties.Add(new PropertyDefinition { Name = "ValueInt", DataType = "int", IsNullable = true });
        typeMappingCondition.Relationships.Add(new RelationshipDefinition { Entity = "TypeMapping" });
        typeMappingCondition.Relationships.Add(new RelationshipDefinition { Entity = "Facet" });

        var typeMappingFacetTransform = new EntityDefinition
        {
            Name = "TypeMappingFacetTransform",
        };
        typeMappingFacetTransform.Properties.Add(new PropertyDefinition { Name = "Mode" });
        typeMappingFacetTransform.Properties.Add(new PropertyDefinition { Name = "SetInt", DataType = "int", IsNullable = true });
        typeMappingFacetTransform.Properties.Add(new PropertyDefinition { Name = "SetBool", DataType = "bool", IsNullable = true });
        typeMappingFacetTransform.Relationships.Add(new RelationshipDefinition { Entity = "TypeMapping" });
        typeMappingFacetTransform.Relationships.Add(new RelationshipDefinition { Entity = "Facet" });

        model.Entities.Add(typeSystem);
        model.Entities.Add(dataType);
        model.Entities.Add(facet);
        model.Entities.Add(dataTypeFacet);
        model.Entities.Add(typeSpec);
        model.Entities.Add(setting);
        model.Entities.Add(conversionImplementation);
        model.Entities.Add(typeMapping);
        model.Entities.Add(typeMappingCondition);
        model.Entities.Add(typeMappingFacetTransform);

        return model;
    }
}
