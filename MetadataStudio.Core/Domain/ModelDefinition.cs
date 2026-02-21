using System;
using System.Collections.Generic;

namespace MetadataStudio.Core.Domain;

public sealed class ModelDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<EntityDefinition> Entities { get; } = new();

    public EntityDefinition? FindEntity(string entityName)
    {
        return Entities.Find(entity => string.Equals(entity.Name, entityName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<PropertyDefinition> Properties { get; } = new();
    public List<RelationshipDefinition> Relationships { get; } = new();
}

public sealed class PropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsNullable { get; set; }
}

public sealed class RelationshipDefinition
{
    public string Entity { get; set; } = string.Empty;
}
