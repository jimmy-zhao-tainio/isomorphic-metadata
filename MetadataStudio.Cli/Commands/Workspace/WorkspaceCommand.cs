internal sealed partial class CliRuntime
{
    async Task<int> WorkspaceAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 2)
        {
            return PrintUsageError("Usage: workspace <diff|merge> ...");
        }

        var mode = commandArgs[1].Trim().ToLowerInvariant();
        return mode switch
        {
            "diff" => await InstanceDiffAsync(commandArgs).ConfigureAwait(false),
            "merge" => await InstanceMergeAsync(commandArgs).ConfigureAwait(false),
            _ => PrintCommandUnknownError($"workspace {mode}"),
        };
    }
}
