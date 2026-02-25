using Meta.Core.Domain;
using Meta.Core.WorkspaceConfig;

namespace MetaSchema.Core;

public static class MetaSchemaWorkspaceFactory
{
    public static Workspace CreateEmptyWorkspace(string workspaceRootPath, GenericModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentNullException.ThrowIfNull(model);

        var rootPath = Path.GetFullPath(workspaceRootPath);
        var metadataRootPath = Path.Combine(rootPath, "metadata");

        return new Workspace
        {
            WorkspaceRootPath = rootPath,
            MetadataRootPath = metadataRootPath,
            WorkspaceConfig = MetaWorkspaceModel.CreateDefault(),
            Model = model,
            Instance = new GenericInstance
            {
                ModelName = model.Name,
            },
            IsDirty = true,
        };
    }
}

