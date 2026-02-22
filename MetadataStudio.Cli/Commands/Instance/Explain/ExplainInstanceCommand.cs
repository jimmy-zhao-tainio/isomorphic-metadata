internal sealed partial class CliRuntime
{
    async Task<int> ViewInstanceAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 4)
        {
            return PrintUsageError("Usage: view instance <Entity> <Id> [--workspace <path>]");
        }
    
        var entityName = commandArgs[2];
        var id = commandArgs[3];
        if (ContainsLegacyInstanceReferenceSyntax(id))
        {
            return PrintArgumentError($"Error: unsupported instance reference '{id}'. Use <Entity> <Id>.");
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
                command = "view.instance",
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

