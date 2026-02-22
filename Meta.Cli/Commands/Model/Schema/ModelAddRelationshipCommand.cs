internal sealed partial class CliRuntime
{
    async Task<int> ModelAddRelationshipAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 4)
        {
            return PrintUsageError(
                "Usage: model add-relationship <FromEntity> <ToEntity> [--workspace <path>]");
        }
    
        var fromEntity = commandArgs[2];
        var toEntity = commandArgs[3];
        var options = ParseMutatingCommonOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        var operation = new WorkspaceOp
        {
            Type = WorkspaceOpTypes.AddRelationship,
            EntityName = fromEntity,
            RelatedEntity = toEntity,
        };
    
        return await ExecuteOperationAsync(
                options.WorkspacePath,
                operation,
                "model add-relationship",
                "relationship added",
                ("From", fromEntity),
                ("To", toEntity))
            .ConfigureAwait(false);
    }
}
