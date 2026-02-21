internal sealed partial class CliRuntime
{
    async Task<int> ListAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 2)
        {
            return PrintUsageError("Usage: list <entities|properties|relationships|tasks> ...");
        }
    
        var mode = commandArgs[1].Trim().ToLowerInvariant();
        return mode switch
        {
            "entities" => await ListEntitiesAsync(commandArgs).ConfigureAwait(false),
            "properties" => await ListPropertiesAsync(commandArgs).ConfigureAwait(false),
            "relationships" => await ListRelationshipsAsync(commandArgs).ConfigureAwait(false),
            "tasks" => await ListTasksAsync(commandArgs).ConfigureAwait(false),
            _ => PrintCommandUnknownError($"list {mode}"),
        };
    }
}
