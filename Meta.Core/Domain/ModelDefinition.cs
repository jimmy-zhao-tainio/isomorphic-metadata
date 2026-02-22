using System;
using System.Collections.Generic;
using System.Linq;

namespace Meta.Core.Domain;

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
    public string Plural { get; set; } = string.Empty;
    public List<PropertyDefinition> Properties { get; } = new();
    public List<RelationshipDefinition> Relationships { get; } = new();

    public string GetPluralName()
    {
        return string.IsNullOrWhiteSpace(Plural) ? Name + "s" : Plural;
    }

    public RelationshipDefinition? FindRelationshipByUsageName(string usageName)
    {
        if (string.IsNullOrWhiteSpace(usageName))
        {
            return null;
        }

        return Relationships.FirstOrDefault(relationship =>
            string.Equals(relationship.GetName(), usageName, StringComparison.OrdinalIgnoreCase));
    }

    public RelationshipDefinition? FindRelationshipByName(string relationshipName)
    {
        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            return null;
        }

        return Relationships.FirstOrDefault(relationship =>
            string.Equals(relationship.GetName(), relationshipName, StringComparison.OrdinalIgnoreCase));
    }
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
    public string Name { get; set; } = string.Empty;

    public string GetName()
    {
        return string.IsNullOrWhiteSpace(Name) ? Entity + "Id" : Name;
    }

    public string GetNavigationName()
    {
        var relationshipName = GetName();
        if (relationshipName.EndsWith("Id", StringComparison.Ordinal) && relationshipName.Length > 2)
        {
            return relationshipName[..^2];
        }

        return relationshipName;
    }
}
