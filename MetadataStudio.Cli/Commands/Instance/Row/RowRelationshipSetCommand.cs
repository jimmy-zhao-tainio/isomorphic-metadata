internal sealed partial class CliRuntime
{
    async Task<int> RowRelationshipSetAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 7)
        {
            return PrintUsageError(
                "Usage: row relationship set <FromEntity> <FromId> --to <ToEntity> <ToId> [--workspace <path>]");
        }
    
        var fromEntityName = commandArgs[3];
        var fromId = commandArgs[4];
        if (ContainsLegacyRowReferenceSyntax(fromId))
        {
            return PrintArgumentError($"Error: unsupported row reference '{fromId}'. Use <Entity> <Id>.");
        }
        var options = ParseRowRelationshipSetOptions(commandArgs, startIndex: 5);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        if (string.IsNullOrWhiteSpace(options.ToEntity) || string.IsNullOrWhiteSpace(options.ToId))
        {
            return PrintArgumentError("Error: row relationship set requires --to <ToEntity> <ToId>.");
        }
        if (ContainsLegacyRowReferenceSyntax(options.ToId))
        {
            return PrintArgumentError($"Error: unsupported row reference '{options.ToId}'. Use <Entity> <Id>.");
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var fromEntity = RequireEntity(workspace, fromEntityName);
            var fromRow = ResolveRowById(workspace, fromEntityName, fromId);
    
            var toEntityName = options.ToEntity;
            var toId = options.ToId;
            var toRelationshipName = ResolveRelationshipName(fromEntity, toEntityName);
            if (string.IsNullOrWhiteSpace(toRelationshipName))
            {
                return PrintDataError(
                    "E_RELATIONSHIP_NOT_FOUND",
                    $"Relationship '{fromEntityName}->{toEntityName}' does not exist.");
            }
    
            RequireEntity(workspace, toEntityName);
            var targetExists = workspace.Instance.GetOrCreateEntityRecords(toEntityName)
                .Any(row => string.Equals(row.Id, toId, StringComparison.OrdinalIgnoreCase));
            if (!targetExists)
            {
                return PrintDataError(
                    "E_ROW_NOT_FOUND",
                    $"Row with Id '{toId}' does not exist in entity '{toEntityName}'.");
            }
    
            var operation = new WorkspaceOp
            {
                Type = WorkspaceOpTypes.BulkUpsertRows,
                EntityName = fromEntityName,
                RowPatches =
                {
                    BuildRelationshipUsageRewritePatch(fromRow, toRelationshipName, toId),
                },
            };
    
            return await ExecuteOperationsAgainstLoadedWorkspaceAsync(
                    workspace,
                    new[] { operation },
                    commandName: "row.relationship.set",
                    successMessage: "relationship usage updated",
                    successDetails: new[]
                    {
                        ("FromRow", BuildEntityRowAddress(fromEntityName, fromRow.Id)),
                        ("ToRow", BuildEntityRowAddress(toRelationshipName, toId)),
                    })
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}
