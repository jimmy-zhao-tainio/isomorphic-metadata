internal sealed partial class CliRuntime
{
    async Task<int> GenerateAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 2)
        {
            return PrintUsageError("Usage: generate <sql|csharp|ssdt> --out <dir> [--workspace <path>]");
        }
    
        var mode = commandArgs[1].Trim().ToLowerInvariant();
        var options = ParseGenerateOptions(commandArgs, startIndex: 2);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            return PrintArgumentError("Error: generate requires --out <dir>.");
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var diagnostics = services.ValidationService.Validate(workspace);
            workspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                return PrintOperationValidationFailure("generate", Array.Empty<WorkspaceOp>(), diagnostics);
            }

            GenerationManifest manifest;
            switch (mode)
            {
                case "sql":
                    manifest = GenerationService.GenerateSql(workspace, options.OutputDirectory);
                    if (globalJson)
                    {
                        WriteJson(new { command = "generate", mode = "sql", files = manifest.FileHashes.Count, hash = manifest.CombinedHash });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "generated sql",
                            ("Out", Path.GetFullPath(options.OutputDirectory)),
                            ("Files", manifest.FileHashes.Count.ToString(CultureInfo.InvariantCulture)),
                            ("Hash", manifest.CombinedHash));
                    }
    
                    return 0;
                case "csharp":
                    manifest = GenerationService.GenerateCSharp(workspace, options.OutputDirectory);
                    if (globalJson)
                    {
                        WriteJson(new { command = "generate", mode = "csharp", files = manifest.FileHashes.Count, hash = manifest.CombinedHash });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "generated csharp",
                            ("Out", Path.GetFullPath(options.OutputDirectory)),
                            ("Files", manifest.FileHashes.Count.ToString(CultureInfo.InvariantCulture)),
                            ("Hash", manifest.CombinedHash));
                    }
    
                    return 0;
                case "ssdt":
                    manifest = GenerationService.GenerateSsdt(workspace, options.OutputDirectory);
                    if (globalJson)
                    {
                        WriteJson(new { command = "generate", mode = "ssdt", files = manifest.FileHashes.Count, hash = manifest.CombinedHash });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "generated ssdt",
                            ("Out", Path.GetFullPath(options.OutputDirectory)),
                            ("Files", manifest.FileHashes.Count.ToString(CultureInfo.InvariantCulture)),
                            ("Hash", manifest.CombinedHash));
                    }
    
                    return 0;
                default:
                    return PrintArgumentError($"Error: unknown generate mode '{mode}'.");
            }
        }
        catch (Exception exception)
        {
            return PrintGenerationError("E_GENERATION", exception.Message);
        }
    }
}
