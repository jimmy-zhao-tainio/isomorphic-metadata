using Meta.Core.Domain;

namespace MetaSchema.Core;

public static class MetaSchemaCatalogWorkspaces
{
    public static Workspace CreateEmptySchemaCatalogWorkspace(string workspaceRootPath)
    {
        return MetaSchemaWorkspaceFactory.CreateEmptyWorkspace(
            workspaceRootPath,
            MetaSchemaModels.CreateSchemaCatalogModel());
    }

    public static Workspace CreateEmptyTypeConversionCatalogWorkspace(string workspaceRootPath)
    {
        return MetaSchemaWorkspaceFactory.CreateEmptyWorkspace(
            workspaceRootPath,
            MetaSchemaModels.CreateTypeConversionCatalogModel());
    }

    public static Workspace CreateSeedTypeConversionCatalogWorkspace(string workspaceRootPath)
    {
        return TypeConversionCatalogSeed.CreateWorkspace(workspaceRootPath);
    }
}
