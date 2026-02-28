using System.Text.RegularExpressions;
using Meta.Core.Operations;

internal sealed partial class CliRuntime
{
    private static readonly Regex ModelNamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    async Task<int> ModelRefactorPropertyToRelationshipAsync(string[] commandArgs)
    {
        var options = ParseModelRefactorPropertyToRelationshipOptions(commandArgs, startIndex: 3);
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

            var result = ApplyPropertyToRelationshipRefactor(workspace, refactorOptions);
            ApplyImplicitNormalization(workspace);

            var diagnostics = services.ValidationService.Validate(workspace);
            workspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
                return PrintOperationValidationFailure(
                    "model refactor property-to-relationship",
                    Array.Empty<WorkspaceOp>(),
                    diagnostics);
            }

            await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);

            presenter.WriteOk(
                "refactor property-to-relationship",
                ("Workspace", Path.GetFullPath(workspace.WorkspaceRootPath)),
                ("Model", workspace.Model.Name),
                ("Source", result.SourceAddress),
                ("Target", result.TargetEntityName),
                ("Lookup", result.LookupAddress),
                ("Role", string.IsNullOrWhiteSpace(result.Role) ? "(none)" : result.Role),
                ("Drop source property", refactorOptions.DropSourceProperty ? "yes" : "no"));
            presenter.WriteInfo($"Rows rewritten: {result.RowsRewritten}");
            presenter.WriteInfo($"Property dropped: {(result.PropertyDropped ? "yes" : "no")}");
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

    PropertyToRelationshipRefactorResult ApplyPropertyToRelationshipRefactor(
        Workspace workspace,
        PropertyToRelationshipRefactorOptions options)
    {
        var sourceEntity = RequireEntity(workspace, options.SourceEntityName);
        var targetEntity = RequireEntity(workspace, options.TargetEntityName);
        var sourceProperty = sourceEntity.Properties.FirstOrDefault(item =>
            string.Equals(item.Name, options.SourcePropertyName, StringComparison.OrdinalIgnoreCase));
        if (sourceProperty == null)
        {
            throw new InvalidOperationException(
                $"Property '{options.SourceEntityName}.{options.SourcePropertyName}' does not exist.");
        }

        var targetLookupProperty = targetEntity.Properties.FirstOrDefault(item =>
            string.Equals(item.Name, options.LookupPropertyName, StringComparison.OrdinalIgnoreCase));
        if (targetLookupProperty == null)
        {
            throw new InvalidOperationException(
                $"Property '{options.TargetEntityName}.{options.LookupPropertyName}' does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(options.Role) && !ModelNamePattern.IsMatch(options.Role))
        {
            throw new InvalidOperationException(
                $"Role '{options.Role}' is invalid. Use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        var relationship = new GenericRelationship
        {
            Entity = targetEntity.Name,
            Role = options.Role,
        };
        var relationshipUsageName = relationship.GetColumnName();

        var sameEdgeExists = sourceEntity.Relationships.Any(item =>
            string.Equals(item.Entity, relationship.Entity, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.GetRoleOrDefault(), relationship.GetRoleOrDefault(), StringComparison.OrdinalIgnoreCase));
        if (sameEdgeExists)
        {
            throw new InvalidOperationException(
                $"Relationship '{sourceEntity.Name}.{relationshipUsageName}' already exists.");
        }

        var usageCollision = sourceEntity.Relationships.Any(item =>
            string.Equals(item.GetColumnName(), relationshipUsageName, StringComparison.OrdinalIgnoreCase));
        if (usageCollision)
        {
            throw new InvalidOperationException(
                $"Relationship name '{sourceEntity.Name}.{relationshipUsageName}' already exists.");
        }

        var propertyCollision = sourceEntity.Properties.Any(item =>
            string.Equals(item.Name, relationshipUsageName, StringComparison.OrdinalIgnoreCase));
        if (propertyCollision)
        {
            throw new InvalidOperationException(
                $"Cannot add relationship '{sourceEntity.Name}.{relationshipUsageName}' because property '{sourceEntity.Name}.{relationshipUsageName}' already exists.");
        }

        var relationshipAssessment = ModelSuggestService.AnalyzeLookupRelationship(
            workspace,
            sourceEntity.Name,
            sourceProperty.Name,
            targetEntity.Name,
            targetLookupProperty.Name,
            options.Role);
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

        var targetLookupMap = BuildTargetLookupMap(workspace, targetEntity.Name, targetLookupProperty.Name);
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

        return new PropertyToRelationshipRefactorResult(
            RowsRewritten: sourceRows.Count,
            PropertyDropped: options.DropSourceProperty,
            SourceAddress: sourceEntity.Name + "." + sourceProperty.Name,
            TargetEntityName: targetEntity.Name,
            LookupAddress: targetEntity.Name + "." + targetLookupProperty.Name,
            Role: options.Role);
    }

    Dictionary<string, string> BuildTargetLookupMap(
        Workspace workspace,
        string targetEntityName,
        string targetLookupPropertyName)
    {
        var targetRows = workspace.Instance.GetOrCreateEntityRecords(targetEntityName)
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        return BuildTargetLookupMap(targetRows, targetEntityName, targetLookupPropertyName);
    }

    static Dictionary<string, string> BuildTargetLookupMap(
        IReadOnlyList<GenericRecord> entityRows,
        string targetEntityName,
        string targetLookupPropertyName)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var targetRow in entityRows)
        {
            if (!targetRow.Values.TryGetValue(targetLookupPropertyName, out var targetLookupValue) ||
                string.IsNullOrEmpty(targetLookupValue))
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

    (bool Ok, PropertyToRelationshipRefactorOptions Options, string ErrorMessage)
        ParseModelRefactorPropertyToRelationshipOptions(string[] commandArgs, int startIndex)
    {
        var workspacePath = DefaultWorkspacePath();
        var source = string.Empty;
        var target = string.Empty;
        var lookup = string.Empty;
        var role = string.Empty;
        var dropSourceProperty = false;

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
                    return (false, default, "Error: --source requires <Entity.Property>.");
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

            if (string.Equals(arg, "--lookup", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --lookup requires <Property>.");
                }

                lookup = commandArgs[++i].Trim();
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

            if (string.Equals(arg, "--drop-source-property", StringComparison.OrdinalIgnoreCase))
            {
                dropSourceProperty = true;
                continue;
            }

            return (false, default, $"Error: unknown option '{arg}'.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return (false, default, "Error: --source <Entity.Property> is required.");
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return (false, default, "Error: --target <Entity> is required.");
        }

        if (string.IsNullOrWhiteSpace(lookup))
        {
            return (false, default, "Error: --lookup <Property> is required.");
        }

        var separatorIndex = source.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == source.Length - 1 || source.IndexOf('.', separatorIndex + 1) >= 0)
        {
            return (false, default, "Error: --source must be in format <Entity.Property>.");
        }

        var sourceEntityName = source[..separatorIndex].Trim();
        var sourcePropertyName = source[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(sourceEntityName) || string.IsNullOrWhiteSpace(sourcePropertyName))
        {
            return (false, default, "Error: --source must be in format <Entity.Property>.");
        }

        var options = new PropertyToRelationshipRefactorOptions(
            WorkspacePath: workspacePath,
            SourceEntityName: sourceEntityName,
            SourcePropertyName: sourcePropertyName,
            TargetEntityName: target,
            LookupPropertyName: lookup,
            Role: role,
            DropSourceProperty: dropSourceProperty);

        return (true, options, string.Empty);
    }

    readonly record struct PropertyToRelationshipRefactorOptions(
        string WorkspacePath,
        string SourceEntityName,
        string SourcePropertyName,
        string TargetEntityName,
        string LookupPropertyName,
        string Role,
        bool DropSourceProperty);

    readonly record struct PropertyToRelationshipRefactorResult(
        int RowsRewritten,
        bool PropertyDropped,
        string SourceAddress,
        string TargetEntityName,
        string LookupAddress,
        string Role);
}
