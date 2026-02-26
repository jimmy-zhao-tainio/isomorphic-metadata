internal sealed partial class CliRuntime
{
    async Task<int> ModelRefactorAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 3)
        {
            return PrintUsageError(
                "Usage: model refactor property-to-relationship --source <Entity.Property> --target <Entity> --lookup <Property> [--role <Role>] [--drop-source-property] [--workspace <path>]");
        }

        var mode = commandArgs[2].Trim().ToLowerInvariant();
        return mode switch
        {
            "property-to-relationship" => await ModelRefactorPropertyToRelationshipAsync(commandArgs).ConfigureAwait(false),
            _ => PrintCommandUnknownError($"model refactor {mode}"),
        };
    }
}
