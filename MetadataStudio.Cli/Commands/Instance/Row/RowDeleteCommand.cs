internal sealed partial class CliRuntime
{
    async Task<int> DeleteAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 3)
        {
            return PrintUsageError("Usage: delete <Entity> <Id> [--workspace <path>]");
        }
    
        var entityName = commandArgs[1];
        var id = commandArgs[2];
        if (ContainsLegacyRowReferenceSyntax(id))
        {
            return PrintArgumentError($"Error: unsupported row reference '{id}'. Use <Entity> <Id>.");
        }
    
        var options = ParseMutatingCommonOptions(commandArgs, startIndex: 3);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            RequireEntity(workspace, entityName);
            ResolveRowById(workspace, entityName, id);
    
            var operation = new WorkspaceOp
            {
                Type = WorkspaceOpTypes.DeleteRows,
                EntityName = entityName,
                Ids = new List<string> { id },
            };
    
            return await ExecuteOperationsAgainstLoadedWorkspaceAsync(
                    workspace,
                    new[] { operation },
                    commandName: "delete",
                    successMessage: $"deleted {BuildEntityRowAddress(entityName, id)}")
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}
