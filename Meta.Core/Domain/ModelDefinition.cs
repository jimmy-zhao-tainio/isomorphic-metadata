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

    public RelationshipDefinition? FindRelationshipByRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return Relationships.FirstOrDefault(relationship =>
            string.Equals(relationship.GetRoleOrDefault(), role, StringComparison.OrdinalIgnoreCase));
    }

    public RelationshipDefinition? FindRelationshipByColumnName(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        return Relationships.FirstOrDefault(relationship =>
            string.Equals(relationship.GetColumnName(), columnName, StringComparison.OrdinalIgnoreCase));
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
    public string Role { get; set; } = string.Empty;

    public string GetRoleOrDefault()
    {
        return string.IsNullOrWhiteSpace(Role) ? Entity : Role;
    }

    public string GetColumnName()
    {
        return GetRoleOrDefault() + "Id";
    }

    public string GetNavigationName()
    {
        return GetRoleOrDefault();
    }
}
