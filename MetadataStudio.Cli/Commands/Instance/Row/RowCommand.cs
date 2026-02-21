internal sealed partial class CliRuntime
{
    async Task<int> RowAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 2)
        {
            return PrintUsageError("Usage: row <update|relationship> ...");
        }
    
        var mode = commandArgs[1].Trim().ToLowerInvariant();
        return mode switch
        {
            "update" => await RowUpdateAsync(commandArgs).ConfigureAwait(false),
            "relationship" => await RowRelationshipAsync(commandArgs).ConfigureAwait(false),
            _ => UnknownRowCommand(mode),
        };
    }
    
    int UnknownRowCommand(string mode)
    {
        return PrintCommandUnknownError($"row {mode}");
    }
}
