internal sealed partial class CliRuntime
{
    async Task<int> ViewAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 3)
        {
            return PrintUsageError("Usage: view <entity|row> ...");
        }
    
        var mode = commandArgs[1].Trim().ToLowerInvariant();
        return mode switch
        {
            "entity" => await ViewEntityAsync(commandArgs).ConfigureAwait(false),
            "row" => await ViewRowAsync(commandArgs).ConfigureAwait(false),
            _ => UnknownViewCommand(mode),
        };
    }
    
    int UnknownViewCommand(string mode)
    {
        return PrintCommandUnknownError($"view {mode}");
    }
}
