internal sealed partial class CliRuntime
{
    async Task<int> InstanceRelationshipClearAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 6)
        {
            return PrintUsageError(
                "Usage: instance relationship clear <FromEntity> <FromId> --to-entity <ToEntity> [--workspace <path>]");
        }
    
        var fromEntityName = commandArgs[3];
        var fromId = commandArgs[4];
        if (ContainsLegacyInstanceReferenceSyntax(fromId))
        {
            return PrintArgumentError($"Error: unsupported instance reference '{fromId}'. Use <Entity> <Id>.");
        }
        var options = ParseInstanceRelationshipClearOptions(commandArgs, startIndex: 5);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        if (string.IsNullOrWhiteSpace(options.ToEntity))
        {
            return PrintArgumentError("Error: instance relationship clear requires --to-entity <ToEntity>.");
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var fromEntity = RequireEntity(workspace, fromEntityName);
            var fromRow = ResolveRowById(workspace, fromEntityName, fromId);
            var toEntityName = options.ToEntity;
            var relationship = ResolveRelationshipDefinition(fromEntity, toEntityName, out var isAmbiguous);
            if (isAmbiguous)
            {
                return PrintDataError(
                    "E_RELATIONSHIP_AMBIGUOUS",
                    $"Relationship selector '{toEntityName}' is ambiguous on entity '{fromEntityName}'. Use relationship role or column.");
            }

            if (relationship == null)
            {
                return PrintDataError(
                    "E_RELATIONSHIP_NOT_FOUND",
                    $"Relationship '{fromEntityName}->{toEntityName}' does not exist.");
            }

            var toRelationshipName = relationship.GetColumnName();

            return PrintFormattedError(
                "E_OPERATION",
                $"Cannot clear required relationship '{fromEntityName}->{toRelationshipName}'.",
                exitCode: 4,
                hints: new[]
                {
                    $"Instance: {BuildEntityInstanceAddress(fromEntityName, fromRow.Id)}",
                    $"Next: meta instance relationship set {fromEntityName} {QuoteInstanceId(fromRow.Id)} --to {toRelationshipName} <ToId>",
                });
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}

