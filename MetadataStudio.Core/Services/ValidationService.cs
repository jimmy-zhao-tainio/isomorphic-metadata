using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MetadataStudio.Core.Domain;

namespace MetadataStudio.Core.Services;

public sealed class ValidationService : IValidationService
{
    private static readonly Regex NamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> CSharpReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    };

    private static readonly HashSet<string> SqlReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "add", "all", "alter", "and", "any", "as", "asc", "authorization", "backup", "begin", "between",
        "break", "browse", "bulk", "by", "cascade", "case", "check", "checkpoint", "close", "clustered",
        "coalesce", "collate", "column", "commit", "compute", "constraint", "contains", "containstable",
        "continue", "convert", "create", "cross", "current", "current_date", "current_time",
        "current_timestamp", "current_user", "cursor", "database", "dbcc", "deallocate", "declare",
        "default", "delete", "deny", "desc", "disk", "distinct", "distributed", "double", "drop", "dump",
        "else", "end", "errlvl", "escape", "except", "exec", "execute", "exists", "exit", "external",
        "fetch", "file", "fillfactor", "for", "foreign", "freetext", "freetexttable", "from", "full",
        "function", "goto", "grant", "group", "having", "holdlock", "identity", "identity_insert",
        "identitycol", "if", "in", "index", "inner", "insert", "intersect", "into", "is", "join", "key",
        "kill", "left", "like", "lineno", "load", "merge", "national", "nocheck", "nonclustered", "not",
        "null", "nullif", "of", "off", "offsets", "on", "open", "opendatasource", "openquery",
        "openrowset", "openxml", "option", "or", "order", "outer", "over", "percent", "pivot", "plan",
        "precision", "primary", "print", "proc", "procedure", "public", "raiserror", "read", "readtext",
        "reconfigure", "references", "replication", "restore", "restrict", "return", "revert", "revoke",
        "right", "rollback", "rowcount", "rowguidcol", "rule", "save", "schema", "securityaudit",
        "select", "semantickeyphrasetable", "semanticsimilaritydetailstable", "semanticsimilaritytable",
        "session_user", "set", "setuser", "shutdown", "some", "statistics", "system_user", "table",
        "tablesample", "textsize", "then", "to", "top", "tran", "transaction", "trigger", "truncate",
        "try_convert", "tsequal", "union", "unique", "unpivot", "update", "updatetext", "use", "user",
        "values", "varying", "view", "waitfor", "when", "where", "while", "with", "writetext",
    };

    public WorkspaceDiagnostics Validate(Workspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        var diagnostics = new WorkspaceDiagnostics();
        ValidateModel(workspace.Model, diagnostics);
        ValidateInstance(workspace.Model, workspace.Instance, diagnostics);
        return diagnostics;
    }

    public WorkspaceDiagnostics ValidateIncremental(Workspace workspace, IReadOnlyCollection<string> touchedEntities)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (touchedEntities == null || touchedEntities.Count == 0)
        {
            return Validate(workspace);
        }

        var filter = new HashSet<string>(touchedEntities.Where(entity => !string.IsNullOrWhiteSpace(entity)),
            StringComparer.OrdinalIgnoreCase);

        var diagnostics = new WorkspaceDiagnostics();
        ValidateModel(workspace.Model, diagnostics, filter);
        ValidateInstance(workspace.Model, workspace.Instance, diagnostics, filter);
        return diagnostics;
    }

    private static void ValidateModel(
        ModelDefinition model,
        WorkspaceDiagnostics diagnostics,
        HashSet<string>? filter = null)
    {
        if (model == null)
        {
            diagnostics.Issues.Add(new DiagnosticIssue
            {
                Code = "model.null",
                Message = "Model is missing.",
                Severity = IssueSeverity.Error,
                Location = "model",
            });
            return;
        }

        if (!IsValidName(model.Name))
        {
            diagnostics.Issues.Add(new DiagnosticIssue
            {
                Code = "model.name.invalid",
                Message = $"Model name '{model.Name}' is invalid.",
                Severity = IssueSeverity.Error,
                Location = "model/@name",
            });
        }
        else
        {
            ValidateReservedName(model.Name, "model", "model/@name", diagnostics);
        }

        var entityNameMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in model.Entities)
        {
            if (!entityNameMap.Add(entity.Name))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "entity.duplicate",
                    Message = $"Entity '{entity.Name}' is duplicated.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}",
                });
            }

            if (filter != null && !filter.Contains(entity.Name))
            {
                continue;
            }

            if (!IsValidName(entity.Name))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "entity.name.invalid",
                    Message = $"Entity name '{entity.Name}' is invalid.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}",
                });
            }
            else
            {
                ValidateReservedName(entity.Name, $"entity '{entity.Name}'", $"model/entity/{entity.Name}", diagnostics);
            }

            if (string.Equals(model.Name, entity.Name, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "model.entity.collision",
                    Message = $"Model name '{model.Name}' collides with entity '{entity.Name}'.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}",
                });
            }

            ValidateEntityProperties(entity, diagnostics);
            ValidateEntityIdProperty(entity, diagnostics);
            ValidateEntityMemberNameCollisions(entity, diagnostics);
        }

        ValidateRelationships(model, diagnostics, filter);
        ValidateCycles(model, diagnostics, filter);
    }

    private static void ValidateEntityProperties(EntityDefinition entity, WorkspaceDiagnostics diagnostics)
    {
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entity.Properties)
        {
            if (!propertyNames.Add(property.Name))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "property.duplicate",
                    Message = $"Property '{entity.Name}.{property.Name}' is duplicated.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}/property/{property.Name}",
                });
            }

            if (!IsValidName(property.Name))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "property.name.invalid",
                    Message = $"Property name '{entity.Name}.{property.Name}' is invalid.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}/property/{property.Name}",
                });
            }
            else
            {
                ValidateReservedName(
                    property.Name,
                    $"property '{entity.Name}.{property.Name}'",
                    $"model/entity/{entity.Name}/property/{property.Name}",
                    diagnostics);
            }

            if (string.IsNullOrWhiteSpace(property.DataType))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "property.datatype.empty",
                    Message = $"Property '{entity.Name}.{property.Name}' has empty data type.",
                    Severity = IssueSeverity.Warning,
                    Location = $"model/entity/{entity.Name}/property/{property.Name}/@dataType",
                });
            }
        }
    }

    private static void ValidateEntityMemberNameCollisions(EntityDefinition entity, WorkspaceDiagnostics diagnostics)
    {
        var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entity.Properties)
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                continue;
            }

            memberNames.Add(property.Name);
        }

        foreach (var relationship in entity.Relationships)
        {
            if (string.IsNullOrWhiteSpace(relationship.Entity))
            {
                continue;
            }

            if (!memberNames.Add(relationship.Entity))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "entity.member.collision",
                    Message =
                        $"Entity '{entity.Name}' has a name collision between property/member '{relationship.Entity}' and relationship '{relationship.Entity}'.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity.Name}/relationship/{relationship.Entity}",
                });
            }
        }
    }

    private static void ValidateEntityIdProperty(EntityDefinition entity, WorkspaceDiagnostics diagnostics)
    {
        var explicitId = entity.Properties.FirstOrDefault(property =>
            string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase));
        if (explicitId == null)
        {
            return;
        }

        diagnostics.Issues.Add(new DiagnosticIssue
        {
            Code = "property.id.explicit",
            Message = $"Entity '{entity.Name}' must not declare property 'Id'. It is implicit.",
            Severity = IssueSeverity.Error,
            Location = $"model/entity/{entity.Name}/property/Id",
        });
    }

    private static void ValidateRelationships(
        ModelDefinition model,
        WorkspaceDiagnostics diagnostics,
        HashSet<string>? filter = null)
    {
        var entityNames = new HashSet<string>(model.Entities.Select(entity => entity.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var entity in model.Entities)
        {
            if (filter != null && !filter.Contains(entity.Name))
            {
                continue;
            }

            var relationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relationship in entity.Relationships)
            {
                if (!relationTargets.Add(relationship.Entity))
                {
                    diagnostics.Issues.Add(new DiagnosticIssue
                    {
                        Code = "relationship.duplicate",
                        Message = $"Relationship '{entity.Name}->{relationship.Entity}' is duplicated.",
                        Severity = IssueSeverity.Error,
                        Location = $"model/entity/{entity.Name}/relationship/{relationship.Entity}",
                    });
                }

                if (!entityNames.Contains(relationship.Entity))
                {
                    diagnostics.Issues.Add(new DiagnosticIssue
                    {
                        Code = "relationship.target.missing",
                        Message = $"Relationship target '{relationship.Entity}' in entity '{entity.Name}' does not exist.",
                        Severity = IssueSeverity.Error,
                        Location = $"model/entity/{entity.Name}/relationship/{relationship.Entity}",
                    });
                }
            }
        }
    }

    private static void ValidateCycles(ModelDefinition model, WorkspaceDiagnostics diagnostics, HashSet<string>? filter)
    {
        var graph = model.Entities.ToDictionary(
            entity => entity.Name,
            entity => entity.Relationships.Select(relationship => relationship.Entity).ToList(),
            StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in graph.Keys)
        {
            if (filter != null && !filter.Contains(entity))
            {
                continue;
            }

            if (DetectCycle(entity, graph, visited, stack))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "relationship.cycle",
                    Message = $"Cycle detected from entity '{entity}'.",
                    Severity = IssueSeverity.Error,
                    Location = $"model/entity/{entity}",
                });
            }
        }
    }

    private static bool DetectCycle(
        string entity,
        IReadOnlyDictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> stack)
    {
        if (stack.Contains(entity))
        {
            return true;
        }

        if (visited.Contains(entity))
        {
            return false;
        }

        visited.Add(entity);
        stack.Add(entity);
        if (graph.TryGetValue(entity, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (!graph.ContainsKey(neighbor))
                {
                    continue;
                }

                if (DetectCycle(neighbor, graph, visited, stack))
                {
                    return true;
                }
            }
        }

        stack.Remove(entity);
        return false;
    }

    private static void ValidateInstance(
        ModelDefinition model,
        InstanceStore instance,
        WorkspaceDiagnostics diagnostics,
        HashSet<string>? filter = null)
    {
        if (instance == null)
        {
            diagnostics.Issues.Add(new DiagnosticIssue
            {
                Code = "instance.null",
                Message = "Instance data is missing.",
                Severity = IssueSeverity.Error,
                Location = "instance",
            });
            return;
        }

        var modelByEntity = model.Entities.ToDictionary(entity => entity.Name, StringComparer.OrdinalIgnoreCase);
        var idsByEntity = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityRecords in instance.RecordsByEntity)
        {
            var entityName = entityRecords.Key;
            if (filter != null && !filter.Contains(entityName))
            {
                continue;
            }

            if (!modelByEntity.TryGetValue(entityName, out var modelEntity))
            {
                diagnostics.Issues.Add(new DiagnosticIssue
                {
                    Code = "instance.entity.unknown",
                    Message = $"Instance includes unknown entity '{entityName}'.",
                    Severity = IssueSeverity.Warning,
                    Location = $"instance/{entityName}",
                });
                continue;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            idsByEntity[entityName] = ids;

            foreach (var record in entityRecords.Value)
            {
                if (string.IsNullOrWhiteSpace(record.Id))
                {
                    diagnostics.Issues.Add(new DiagnosticIssue
                    {
                        Code = "instance.id.missing",
                        Message = $"Entity '{entityName}' has a record with missing Id.",
                        Severity = IssueSeverity.Error,
                        Location = $"instance/{entityName}",
                    });
                }
                else if (!IsPositiveIntegerIdentity(record.Id))
                {
                    diagnostics.Issues.Add(new DiagnosticIssue
                    {
                        Code = "instance.id.invalid",
                        Message = $"Entity '{entityName}' has non-numeric Id '{record.Id}'.",
                        Severity = IssueSeverity.Error,
                        Location = $"instance/{entityName}/{record.Id}",
                    });
                }
                else if (!ids.Add(record.Id))
                {
                    diagnostics.Issues.Add(new DiagnosticIssue
                    {
                        Code = "instance.id.duplicate",
                        Message = $"Entity '{entityName}' has duplicate Id '{record.Id}'.",
                        Severity = IssueSeverity.Error,
                        Location = $"instance/{entityName}/{record.Id}",
                    });
                }

                foreach (var requiredProperty in modelEntity.Properties
                             .Where(property =>
                                 !property.IsNullable &&
                                 !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)))
                {
                    var hasValue = record.Values.TryGetValue(requiredProperty.Name, out var value) && value != null;
                    if (!hasValue)
                    {
                        diagnostics.Issues.Add(new DiagnosticIssue
                        {
                            Code = "instance.required.missing",
                            Message = $"Entity '{entityName}' record '{record.Id}' is missing required value '{requiredProperty.Name}'.",
                            Severity = IssueSeverity.Error,
                            Location = $"instance/{entityName}/{record.Id}/{requiredProperty.Name}",
                        });

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(value) &&
                        !AllowsExplicitEmptyRequiredValue(model, modelEntity, requiredProperty))
                    {
                        diagnostics.Issues.Add(new DiagnosticIssue
                        {
                            Code = "instance.required.missing",
                            Message = $"Entity '{entityName}' record '{record.Id}' is missing required value '{requiredProperty.Name}'.",
                            Severity = IssueSeverity.Error,
                            Location = $"instance/{entityName}/{record.Id}/{requiredProperty.Name}",
                        });
                    }
                }
            }
        }

        foreach (var entityRecords in instance.RecordsByEntity)
        {
            var entityName = entityRecords.Key;
            if (filter != null && !filter.Contains(entityName))
            {
                continue;
            }

            if (!modelByEntity.TryGetValue(entityName, out var modelEntity))
            {
                continue;
            }

            foreach (var record in entityRecords.Value)
            {
                foreach (var relationship in modelEntity.Relationships)
                {
                    if (!record.RelationshipIds.TryGetValue(relationship.Entity, out var relatedId) ||
                        string.IsNullOrWhiteSpace(relatedId))
                    {
                        diagnostics.Issues.Add(new DiagnosticIssue
                        {
                            Code = "instance.relationship.missing",
                            Message = $"Entity '{entityName}' record '{record.Id}' is missing relationship '{relationship.Entity}'.",
                            Severity = IssueSeverity.Error,
                            Location = $"instance/{entityName}/{record.Id}/relationship/{relationship.Entity}",
                        });
                        continue;
                    }

                    if (!IsPositiveIntegerIdentity(relatedId))
                    {
                        diagnostics.Issues.Add(new DiagnosticIssue
                        {
                            Code = "instance.relationship.invalid",
                            Message =
                                $"Entity '{entityName}' record '{record.Id}' has invalid relationship '{relationship.Entity}' id '{relatedId}'.",
                            Severity = IssueSeverity.Error,
                            Location = $"instance/{entityName}/{record.Id}/relationship/{relationship.Entity}/{relatedId}",
                        });
                        continue;
                    }

                    if (!idsByEntity.TryGetValue(relationship.Entity, out var targetIds))
                    {
                        targetIds = new HashSet<string>(
                            instance.RecordsByEntity.TryGetValue(relationship.Entity, out var targetRecords)
                                ? targetRecords.Select(targetRecord => targetRecord.Id)
                                : Enumerable.Empty<string>(),
                            StringComparer.OrdinalIgnoreCase);
                        idsByEntity[relationship.Entity] = targetIds;
                    }

                    if (!targetIds.Contains(relatedId))
                    {
                        diagnostics.Issues.Add(new DiagnosticIssue
                        {
                            Code = "instance.relationship.orphan",
                            Message = $"Entity '{entityName}' record '{record.Id}' points to missing '{relationship.Entity}' id '{relatedId}'.",
                            Severity = IssueSeverity.Error,
                            Location = $"instance/{entityName}/{record.Id}/relationship/{relationship.Entity}/{relatedId}",
                        });
                    }
                }
            }
        }
    }

    private static bool IsPositiveIntegerIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length == 0 || text[0] == '-')
        {
            return false;
        }

        var hasNonZeroDigit = false;
        foreach (var ch in text)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }

            if (ch != '0')
            {
                hasNonZeroDigit = true;
            }
        }

        return hasNonZeroDigit;
    }

    private static bool AllowsExplicitEmptyRequiredValue(
        ModelDefinition model,
        EntityDefinition entity,
        PropertyDefinition property)
    {
        if (model == null || entity == null || property == null)
        {
            return false;
        }

        if (!model.Name.StartsWith("InstanceDiffModel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(property.Name, "Value", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(entity.Name, "ModelLeftPropertyInstance", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entity.Name, "ModelRightPropertyInstance", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && NamePattern.IsMatch(value);
    }

    private static void ValidateReservedName(
        string value,
        string subject,
        string location,
        WorkspaceDiagnostics diagnostics)
    {
        if (CSharpReservedWords.Contains(value))
        {
            diagnostics.Issues.Add(new DiagnosticIssue
            {
                Code = "name.reserved.csharp",
                Message = $"{subject} uses reserved C# keyword '{value}'.",
                Severity = IssueSeverity.Error,
                Location = location,
            });
        }

        if (SqlReservedWords.Contains(value))
        {
            diagnostics.Issues.Add(new DiagnosticIssue
            {
                Code = "name.reserved.sql",
                Message = $"{subject} uses reserved SQL keyword '{value}'.",
                Severity = IssueSeverity.Error,
                Location = location,
            });
        }
    }
}
