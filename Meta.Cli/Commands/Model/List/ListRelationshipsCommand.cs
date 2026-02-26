internal sealed partial class CliRuntime
{
    async Task<int> ListRelationshipsAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 3)
        {
            return PrintUsageError("Usage: list relationships <Entity> [--workspace <path>]");
        }
    
        var entityName = commandArgs[2];
        var options = ParseWorkspaceOnlyOptions(commandArgs, startIndex: 3);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
        PrintContractCompatibilityWarning(workspace.WorkspaceConfig);
        var entity = workspace.Model.FindEntity(entityName);
        if (entity == null)
        {
            return PrintDataError("E_ENTITY_NOT_FOUND", $"Entity '{entityName}' does not exist.");
        }
    
        var refs = entity.Relationships
            .OrderBy(relationship => relationship.GetColumnName(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => relationship.Entity, StringComparer.OrdinalIgnoreCase)
            .Select(relationship => new
            {
                Name = relationship.GetColumnName(),
                Target = relationship.Entity,
            })
            .ToList();

        presenter.WriteInfo($"Relationships: {entity.Name} ({refs.Count})");
        presenter.WriteInfo("Required: (n/a)");
        presenter.WriteTable(
            new[] { "Name", "Target" },
            refs.Select(relationship => (IReadOnlyList<string>)new[]
            {
                relationship.Name,
                relationship.Target,
            }).ToList());
    
        return 0;
    }
    
    Task<int> ListTasksAsync(string[] commandArgs)
    {
        var options = ParseWorkspaceOnlyOptions(commandArgs, startIndex: 2);
        if (!options.Ok)
        {
            return Task.FromResult(PrintArgumentError(options.ErrorMessage));
        }
    
        var filesystemContext = ResolveWorkspaceFilesystemContext(options.WorkspacePath);
        var tasksRoot = Path.Combine(filesystemContext.MetadataRootPath, "tasks");
        var taskFiles = Directory.Exists(tasksRoot)
            ? Directory.GetFiles(tasksRoot, "*.task", SearchOption.TopDirectoryOnly)
                .Select(file => Path.GetFileNameWithoutExtension(file) ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        presenter.WriteInfo($"Tasks ({taskFiles.Count}):");
        if (taskFiles.Count == 0)
        {
            presenter.WriteInfo("  (none)");
            return Task.FromResult(0);
        }
    
        foreach (var task in taskFiles)
        {
            presenter.WriteInfo($"  {task}");
        }
    
        return Task.FromResult(0);
    }
}

