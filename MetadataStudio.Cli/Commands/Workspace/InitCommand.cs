internal sealed partial class CliRuntime
{
    async Task<int> InitWorkspaceAsync(string[] commandArgs)
    {
        if (commandArgs.Length > 2)
        {
            return PrintUsageError("Usage: init [<path>]");
        }
    
        var workspacePath = commandArgs.Length == 2 ? commandArgs[1] : ".";
        var workspaceRoot = Path.GetFullPath(workspacePath);
        var metadataRoot = Path.Combine(workspaceRoot, "metadata");
    
        if (WorkspaceLooksInitialized(workspaceRoot, metadataRoot))
        {
            if (globalJson)
            {
                WriteJson(new { command = "init", status = "exists", workspace = workspaceRoot });
            }
            else
            {
                presenter.WriteOk(
                    "workspace already initialized",
                    ("Path", workspaceRoot));
            }
    
            return 0;
        }
    
        var workspace = new Workspace
        {
            WorkspaceRootPath = workspaceRoot,
            MetadataRootPath = metadataRoot,
            Manifest = WorkspaceManifest.CreateDefault(),
            Model = new ModelDefinition
            {
                Name = "MetadataModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "MetadataModel",
            },
            IsDirty = true,
        };
    
        await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);
        if (globalJson)
        {
            WriteJson(new { command = "init", status = "initialized", workspace = workspaceRoot });
        }
        else
        {
            presenter.WriteOk(
                "workspace initialized",
                ("Path", workspaceRoot));
        }
    
        return 0;
    }
}
