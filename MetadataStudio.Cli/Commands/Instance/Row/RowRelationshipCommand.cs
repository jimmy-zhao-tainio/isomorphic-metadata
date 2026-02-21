internal sealed partial class CliRuntime
{
    async Task<int> RowRelationshipAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 3)
        {
            return PrintUsageError("Usage: row relationship <set|clear|list> ...");
        }
    
        var mode = commandArgs[2].Trim().ToLowerInvariant();
        return mode switch
        {
            "set" => await RowRelationshipSetAsync(commandArgs).ConfigureAwait(false),
            "clear" => await RowRelationshipClearAsync(commandArgs).ConfigureAwait(false),
            "list" => await RowRelationshipListAsync(commandArgs).ConfigureAwait(false),
            _ => PrintCommandUnknownError($"row relationship {mode}"),
        };
    }
}
