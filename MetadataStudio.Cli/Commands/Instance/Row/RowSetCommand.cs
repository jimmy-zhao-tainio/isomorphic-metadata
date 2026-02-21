internal sealed partial class CliRuntime
{
    async Task<int> RowUpdateAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 6)
        {
            return PrintUsageError("Usage: row update <Entity> <Id> --set Field=Value [--set Field=Value ...] [--workspace <path>]");
        }
    
        var entityName = commandArgs[2];
        var id = commandArgs[3];
        if (ContainsLegacyRowReferenceSyntax(id))
        {
            return PrintArgumentError($"Error: unsupported row reference '{id}'. Use <Entity> <Id>.");
        }
    
        var options = ParseMutatingEntityOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        if (ContainsIdSetAssignment(options.SetValues))
        {
            return PrintArgumentError("Error: do not use --set Id. Row id must be positional <Id>.");
        }
    
        if (options.SetValues.Count == 0)
        {
            return PrintArgumentError("Error: row update requires at least one --set Field=Value.");
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var entity = RequireEntity(workspace, entityName);
            ResolveRowById(workspace, entityName, id);
            var patches = new List<RowPatch>
            {
                BuildRowPatchForUpdate(entity, id, options.SetValues),
            };
            var operation = new WorkspaceOp
            {
                Type = WorkspaceOpTypes.BulkUpsertRows,
                EntityName = entityName,
                RowPatches = patches,
            };
    
            BulkRelationshipResolver.ResolveRelationshipIds(workspace, operation);
            return await ExecuteOperationsAgainstLoadedWorkspaceAsync(
                    workspace,
                    new[] { operation },
                    commandName: "row.update",
                    successMessage: $"updated {BuildEntityRowAddress(entityName, id)}")
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}
