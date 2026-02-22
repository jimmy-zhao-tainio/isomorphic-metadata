using Meta.Core.Domain;

namespace MetaSchema.Core;

public static class MetaSchemaWorkspaceFactory
{
    public static Workspace CreateEmptyWorkspace(string workspaceRootPath, ModelDefinition model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentNullException.ThrowIfNull(model);

        var rootPath = Path.GetFullPath(workspaceRootPath);
        var metadataRootPath = Path.Combine(rootPath, "metadata");

        return new Workspace
        {
            WorkspaceRootPath = rootPath,
            MetadataRootPath = metadataRootPath,
            Manifest = WorkspaceManifest.CreateDefault(),
            Model = model,
            Instance = new InstanceStore
            {
                ModelName = model.Name,
            },
            IsDirty = true,
        };
    }
}
