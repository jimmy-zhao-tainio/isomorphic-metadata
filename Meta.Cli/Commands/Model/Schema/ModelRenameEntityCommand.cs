internal sealed partial class CliRuntime
{
    async Task<int> ModelRenameEntityAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 4)
        {
            return PrintUsageError(
                "Usage: model rename-entity <Old> <New> [--workspace <path>]");
        }
    
        var oldEntityName = commandArgs[2];
        var newEntityName = commandArgs[3];
        var options = ParseMutatingCommonOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        var operation = new WorkspaceOp
        {
            Type = WorkspaceOpTypes.RenameEntity,
            EntityName = oldEntityName,
            NewEntityName = newEntityName,
        };
    
        return await ExecuteOperationAsync(
                options.WorkspacePath,
                operation,
                "model rename-entity",
                "entity renamed",
                ("From", oldEntityName),
                ("To", newEntityName))
            .ConfigureAwait(false);
    }
}
