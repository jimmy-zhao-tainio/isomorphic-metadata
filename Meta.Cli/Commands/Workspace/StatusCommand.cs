internal sealed partial class CliRuntime
{
    async Task<int> StatusWorkspaceAsync(string[] commandArgs)
    {
        var options = ParseWorkspaceOnlyOptions(commandArgs, startIndex: 1);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
        var dataSizes = CalculateWorkspaceDataSizes(workspace);
        PrintContractCompatibilityWarning(workspace.Manifest);
        if (globalJson)
        {
            WriteJson(new
            {
                command = "status",
                status = "ok",
                workspace = workspace.WorkspaceRootPath,
                metadataRoot = workspace.MetadataRootPath,
                model = workspace.Model.Name,
                entities = workspace.Model.Entities.Count,
                rows = workspace.Instance.RecordsByEntity.Values.Sum(rows => rows.Count),
                modelBytes = dataSizes.ModelBytes,
                modelSize = FormatByteSize(dataSizes.ModelBytes),
                instanceBytes = dataSizes.InstanceBytes,
                instanceSize = FormatByteSize(dataSizes.InstanceBytes),
                workspaceFingerprint = services.WorkspaceService.CalculateHash(workspace),
            });
        }
        else
        {
            PrintWorkspaceSummary(workspace);
        }
    
        return 0;
    }
}
