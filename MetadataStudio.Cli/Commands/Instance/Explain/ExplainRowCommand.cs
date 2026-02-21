internal sealed partial class CliRuntime
{
    async Task<int> ViewRowAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 4)
        {
            return PrintUsageError("Usage: view row <Entity> <Id> [--workspace <path>]");
        }
    
        var entityName = commandArgs[2];
        var id = commandArgs[3];
        if (ContainsLegacyRowReferenceSyntax(id))
        {
            return PrintArgumentError($"Error: unsupported row reference '{id}'. Use <Entity> <Id>.");
        }
        var options = ParseWorkspaceOnlyOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
        PrintContractCompatibilityWarning(workspace.Manifest);
        RequireEntity(workspace, entityName);
        var row = ResolveRowById(workspace, entityName, id);
    
        if (globalJson)
        {
            WriteJson(new
            {
                command = "view.row",
                entity = entityName,
                id = row.Id,
                values = row.Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase),
                relationships = row.RelationshipIds.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase),
            });
            return 0;
        }
    
        PrintSelectedRecord(entityName, row);
        return 0;
    }
}
