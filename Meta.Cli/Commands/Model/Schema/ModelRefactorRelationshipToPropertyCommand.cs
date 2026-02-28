using Meta.Core.Domain;
using Meta.Core.Operations;

internal sealed partial class CliRuntime
{
    async Task<int> ModelRefactorRelationshipToPropertyAsync(string[] commandArgs)
    {
        var options = ParseModelRefactorRelationshipToPropertyOptions(commandArgs, startIndex: 3);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }

        var refactorOptions = options.Options;

        Workspace? workspace = null;
        WorkspaceSnapshot? before = null;
        try
        {
            workspace = await LoadWorkspaceForCommandAsync(refactorOptions.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.WorkspaceConfig);
            before = WorkspaceSnapshotCloner.Capture(workspace);

            var result = ApplyRelationshipToPropertyRefactor(workspace, refactorOptions);
            ApplyImplicitNormalization(workspace);

            var diagnostics = services.ValidationService.Validate(workspace);
            workspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
                return PrintOperationValidationFailure(
                    "model refactor relationship-to-property",
                    Array.Empty<WorkspaceOp>(),
                    diagnostics);
            }

            await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);

            presenter.WriteOk(
                "refactor relationship-to-property",
                ("Workspace", Path.GetFullPath(workspace.WorkspaceRootPath)),
                ("Model", workspace.Model.Name),
                ("Source", result.SourceEntityName),
                ("Target", result.TargetEntityName),
                ("Role", string.IsNullOrWhiteSpace(result.Role) ? "(none)" : result.Role),
                ("Property", result.PropertyName));
            presenter.WriteInfo($"Rows rewritten: {result.RowsRewritten}");
            presenter.WriteInfo("Relationship removed: yes");
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            if (workspace != null && before != null)
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
            }

            return PrintDataError("E_OPERATION", exception.Message);
        }
        catch
        {
            if (workspace != null && before != null)
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
            }

            throw;
        }
    }

    RelationshipToPropertyRefactorResult ApplyRelationshipToPropertyRefactor(
        Workspace workspace,
        RelationshipToPropertyRefactorOptions options)
    {
        var sourceEntity = RequireEntity(workspace, options.SourceEntityName);
        RequireEntity(workspace, options.TargetEntityName);

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
        if (!ModelNamePattern.IsMatch(propertyName))
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

            if (!string.Equals(propertyName, relationshipFieldName, StringComparison.OrdinalIgnoreCase))
            {
                if (sourceRow.Values.ContainsKey(propertyName) || sourceRow.RelationshipIds.ContainsKey(propertyName))
                {
                    throw new InvalidOperationException(
                        $"Cannot demote relationship '{sourceEntity.Name}.{relationshipFieldName}' to property '{propertyName}' because row '{sourceRow.Id}' already contains '{propertyName}'.");
                }
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

        return new RelationshipToPropertyRefactorResult(
            RowsRewritten: sourceRows.Count,
            SourceEntityName: sourceEntity.Name,
            TargetEntityName: options.TargetEntityName,
            Role: relationship.Role,
            PropertyName: propertyName);
    }

    (bool Ok, RelationshipToPropertyRefactorOptions Options, string ErrorMessage)
        ParseModelRefactorRelationshipToPropertyOptions(string[] commandArgs, int startIndex)
    {
        var workspacePath = DefaultWorkspacePath();
        var source = string.Empty;
        var target = string.Empty;
        var role = string.Empty;
        var propertyName = string.Empty;

        for (var i = startIndex; i < commandArgs.Length; i++)
        {
            var arg = commandArgs[i];
            if (string.Equals(arg, "--workspace", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --workspace requires a path.");
                }

                workspacePath = commandArgs[++i];
                continue;
            }

            if (string.Equals(arg, "--source", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --source requires <Entity>.");
                }

                source = commandArgs[++i].Trim();
                continue;
            }

            if (string.Equals(arg, "--target", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --target requires <Entity>.");
                }

                target = commandArgs[++i].Trim();
                continue;
            }

            if (string.Equals(arg, "--role", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --role requires <Role>.");
                }

                role = commandArgs[++i].Trim();
                continue;
            }

            if (string.Equals(arg, "--property", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --property requires <PropertyName>.");
                }

                propertyName = commandArgs[++i].Trim();
                continue;
            }

            return (false, default, $"Error: unknown option '{arg}'.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return (false, default, "Error: --source <Entity> is required.");
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return (false, default, "Error: --target <Entity> is required.");
        }

        if (!string.IsNullOrWhiteSpace(role) && !ModelNamePattern.IsMatch(role))
        {
            return (false, default, "Error: --role must use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        if (!string.IsNullOrWhiteSpace(propertyName) && !ModelNamePattern.IsMatch(propertyName))
        {
            return (false, default, "Error: --property must use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        var options = new RelationshipToPropertyRefactorOptions(
            WorkspacePath: workspacePath,
            SourceEntityName: source,
            TargetEntityName: target,
            Role: role,
            PropertyName: propertyName);

        return (true, options, string.Empty);
    }

    readonly record struct RelationshipToPropertyRefactorOptions(
        string WorkspacePath,
        string SourceEntityName,
        string TargetEntityName,
        string Role,
        string PropertyName);

    readonly record struct RelationshipToPropertyRefactorResult(
        int RowsRewritten,
        string SourceEntityName,
        string TargetEntityName,
        string Role,
        string PropertyName);
}
