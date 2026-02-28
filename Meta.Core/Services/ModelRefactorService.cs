using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Meta.Core.Domain;

namespace Meta.Core.Services;

public readonly record struct PropertyToRelationshipRefactorOptions(
    string SourceEntityName,
    string SourcePropertyName,
    string TargetEntityName,
    string LookupPropertyName,
    string Role,
    bool DropSourceProperty);

public readonly record struct PropertyToRelationshipRefactorResult(
    int RowsRewritten,
    bool PropertyDropped,
    string SourceAddress,
    string TargetEntityName,
    string LookupAddress,
    string Role);

public readonly record struct RelationshipToPropertyRefactorOptions(
    string SourceEntityName,
    string TargetEntityName,
    string Role,
    string PropertyName);

public readonly record struct RelationshipToPropertyRefactorResult(
    int RowsRewritten,
    string SourceEntityName,
    string TargetEntityName,
    string Role,
    string PropertyName);

public sealed class ModelRefactorService : IModelRefactorService
{
    private static readonly Regex IdentifierPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public PropertyToRelationshipRefactorResult RefactorPropertyToRelationship(
        Workspace workspace,
        PropertyToRelationshipRefactorOptions options)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var sourceEntity = RequireEntity(workspace.Model, options.SourceEntityName);
        var targetEntity = RequireEntity(workspace.Model, options.TargetEntityName);
        var sourceProperty = sourceEntity.Properties.FirstOrDefault(item =>
            string.Equals(item.Name, options.SourcePropertyName, StringComparison.OrdinalIgnoreCase));
        if (sourceProperty == null)
        {
            throw new InvalidOperationException(
                $"Property '{options.SourceEntityName}.{options.SourcePropertyName}' was not found.");
        }

        var usesImplicitTargetId = string.Equals(options.LookupPropertyName, "Id", StringComparison.OrdinalIgnoreCase);
        var targetLookupProperty = targetEntity.Properties.FirstOrDefault(item =>
            string.Equals(item.Name, options.LookupPropertyName, StringComparison.OrdinalIgnoreCase));
        if (!usesImplicitTargetId && targetLookupProperty == null)
        {
            throw new InvalidOperationException(
                $"Property '{options.TargetEntityName}.{options.LookupPropertyName}' does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(options.Role) && !IdentifierPattern.IsMatch(options.Role))
        {
            throw new InvalidOperationException(
                $"Role '{options.Role}' is invalid. Use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        var relationshipAssessment = ModelSuggestService.AnalyzeLookupRelationship(
            workspace,
            sourceEntity.Name,
            sourceProperty.Name,
            targetEntity.Name,
            usesImplicitTargetId ? "Id" : targetLookupProperty!.Name,
            options.Role,
            options.DropSourceProperty);
        if (relationshipAssessment.Status != LookupCandidateStatus.Eligible)
        {
            var blockerMessage = string.Join(" ", relationshipAssessment.Blockers);
            if (relationshipAssessment.UnmatchedDistinctValueCount > 0)
            {
                blockerMessage += " Unmatched value sample: " +
                                  string.Join(", ", relationshipAssessment.UnmatchedDistinctValuesSample) + ".";
            }

            throw new InvalidOperationException(
                $"Cannot refactor '{sourceEntity.Name}.{sourceProperty.Name}' to relationship '{sourceEntity.Name}->{targetEntity.Name}': {blockerMessage}");
        }

        var relationship = new GenericRelationship
        {
            Entity = targetEntity.Name,
            Role = options.Role,
        };
        var relationshipUsageName = relationship.GetColumnName();
        var targetLookupMap = BuildTargetLookupMap(
            workspace.Instance.GetOrCreateEntityRecords(targetEntity.Name)
                .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList(),
            targetEntity.Name,
            usesImplicitTargetId ? "Id" : targetLookupProperty!.Name);
        var sourceRows = workspace.Instance.GetOrCreateEntityRecords(sourceEntity.Name)
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var sourceRow in sourceRows)
        {
            if (!sourceRow.Values.TryGetValue(sourceProperty.Name, out var sourceLookupValue) ||
                string.IsNullOrEmpty(sourceLookupValue))
            {
                throw new InvalidOperationException(
                    $"Source contains null/blank; required relationship cannot be created. ({sourceEntity.Name}.{sourceProperty.Name}, Id={sourceRow.Id})");
            }

            if (!targetLookupMap.TryGetValue(sourceLookupValue, out var targetId))
            {
                throw new InvalidOperationException(
                    $"Source values not fully resolvable against target key. Unmatched value: {sourceLookupValue}.");
            }

            sourceRow.RelationshipIds[relationshipUsageName] = targetId;
            if (options.DropSourceProperty)
            {
                sourceRow.Values.Remove(sourceProperty.Name);
            }
        }

        sourceEntity.Relationships.Add(new GenericRelationship
        {
            Entity = targetEntity.Name,
            Role = options.Role,
        });

        if (options.DropSourceProperty)
        {
            sourceEntity.Properties.Remove(sourceProperty);
        }

        workspace.IsDirty = true;

        return new PropertyToRelationshipRefactorResult(
            RowsRewritten: sourceRows.Count,
            PropertyDropped: options.DropSourceProperty,
            SourceAddress: sourceEntity.Name + "." + sourceProperty.Name,
            TargetEntityName: targetEntity.Name,
            LookupAddress: targetEntity.Name + "." + (usesImplicitTargetId ? "Id" : targetLookupProperty!.Name),
            Role: options.Role);
    }

    public RelationshipToPropertyRefactorResult RefactorRelationshipToProperty(
        Workspace workspace,
        RelationshipToPropertyRefactorOptions options)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var sourceEntity = RequireEntity(workspace.Model, options.SourceEntityName);
        RequireEntity(workspace.Model, options.TargetEntityName);

        var relationship = sourceEntity.Relationships.FirstOrDefault(item =>
            string.Equals(item.Entity, options.TargetEntityName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Role ?? string.Empty, options.Role ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (relationship == null)
        {
            throw new InvalidOperationException(
                $"Relationship '{options.SourceEntityName}->{options.TargetEntityName}' was not found.");
        }

        var relationshipFieldName = relationship.GetColumnName();
        var propertyName = string.IsNullOrWhiteSpace(options.PropertyName)
            ? relationshipFieldName
            : options.PropertyName.Trim();
        if (!IdentifierPattern.IsMatch(propertyName))
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' is invalid. Use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        var propertyExists = sourceEntity.Properties.Any(item =>
            string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (propertyExists)
        {
            throw new InvalidOperationException(
                $"Property '{sourceEntity.Name}.{propertyName}' already exists.");
        }

        var sourceRows = workspace.Instance.GetOrCreateEntityRecords(sourceEntity.Name)
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var sourceRow in sourceRows)
        {
            if (!sourceRow.RelationshipIds.TryGetValue(relationshipFieldName, out var fkValue) ||
                string.IsNullOrWhiteSpace(fkValue))
            {
                throw new InvalidOperationException(
                    $"Relationship '{sourceEntity.Name}.{relationshipFieldName}' is missing required value. (Id={sourceRow.Id})");
            }

            if (!string.Equals(propertyName, relationshipFieldName, StringComparison.OrdinalIgnoreCase) &&
                (sourceRow.Values.ContainsKey(propertyName) || sourceRow.RelationshipIds.ContainsKey(propertyName)))
            {
                throw new InvalidOperationException(
                    $"Cannot demote relationship '{sourceEntity.Name}.{relationshipFieldName}' to property '{propertyName}' because row '{sourceRow.Id}' already contains '{propertyName}'.");
            }
        }

        sourceEntity.Relationships.Remove(relationship);
        sourceEntity.Properties.Add(new GenericProperty
        {
            Name = propertyName,
            DataType = "string",
            IsNullable = false,
        });

        foreach (var sourceRow in sourceRows)
        {
            var fkValue = sourceRow.RelationshipIds[relationshipFieldName];
            sourceRow.RelationshipIds.Remove(relationshipFieldName);
            sourceRow.Values[propertyName] = fkValue;
        }

        workspace.IsDirty = true;

        return new RelationshipToPropertyRefactorResult(
            RowsRewritten: sourceRows.Count,
            SourceEntityName: sourceEntity.Name,
            TargetEntityName: options.TargetEntityName,
            Role: relationship.Role,
            PropertyName: propertyName);
    }

    private static GenericEntity RequireEntity(GenericModel model, string entityName)
    {
        var entity = model.FindEntity(entityName);
        if (entity != null)
        {
            return entity;
        }

        throw new InvalidOperationException($"Entity '{entityName}' does not exist.");
    }

    private static Dictionary<string, string> BuildTargetLookupMap(
        IReadOnlyList<GenericRecord> entityRows,
        string targetEntityName,
        string targetLookupPropertyName)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var targetRow in entityRows)
        {
            var targetLookupValue = string.Equals(targetLookupPropertyName, "Id", StringComparison.OrdinalIgnoreCase)
                ? targetRow.Id
                : targetRow.Values.TryGetValue(targetLookupPropertyName, out var scalarLookupValue)
                    ? scalarLookupValue
                    : null;
            if (string.IsNullOrEmpty(targetLookupValue))
            {
                throw new InvalidOperationException(
                    $"Target lookup key has null/blank values. ({targetEntityName}.{targetLookupPropertyName}, Id={targetRow.Id})");
            }

            if (!map.TryAdd(targetLookupValue, targetRow.Id))
            {
                throw new InvalidOperationException(
                    $"Target lookup key is not unique. Duplicate value '{targetLookupValue}'.");
            }
        }

        return map;
    }
}
