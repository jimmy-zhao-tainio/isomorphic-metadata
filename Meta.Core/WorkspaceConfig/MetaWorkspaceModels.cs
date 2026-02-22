using Meta.Core.Domain;

namespace Meta.Core.WorkspaceConfig;

public static class MetaWorkspaceModels
{
    public const string ModelName = "MetaWorkspace";
    public const string DefaultWorkspaceName = "Workspace";

    public static ModelDefinition CreateModel()
    {
        var model = new ModelDefinition
        {
            Name = ModelName,
        };

        var workspace = new EntityDefinition
        {
            Name = "Workspace",
        };
        workspace.Properties.Add(new PropertyDefinition { Name = "Name" });
        workspace.Properties.Add(new PropertyDefinition { Name = "FormatVersion" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "WorkspaceLayout" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "Encoding" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "Newlines" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "CanonicalOrder", Name = "EntitiesOrderId" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "CanonicalOrder", Name = "PropertiesOrderId" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "CanonicalOrder", Name = "RelationshipsOrderId" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "CanonicalOrder", Name = "RowsOrderId" });
        workspace.Relationships.Add(new RelationshipDefinition { Entity = "CanonicalOrder", Name = "AttributesOrderId" });

        var workspaceLayout = new EntityDefinition
        {
            Name = "WorkspaceLayout",
            Plural = "WorkspaceLayouts",
        };
        workspaceLayout.Properties.Add(new PropertyDefinition { Name = "ModelFilePath" });
        workspaceLayout.Properties.Add(new PropertyDefinition { Name = "InstanceDirPath" });

        var encoding = new EntityDefinition
        {
            Name = "Encoding",
            Plural = "Encodings",
        };
        encoding.Properties.Add(new PropertyDefinition { Name = "Name" });

        var newlines = new EntityDefinition
        {
            Name = "Newlines",
            Plural = "NewlinesValues",
        };
        newlines.Properties.Add(new PropertyDefinition { Name = "Name" });

        var canonicalOrder = new EntityDefinition
        {
            Name = "CanonicalOrder",
            Plural = "CanonicalOrders",
        };
        canonicalOrder.Properties.Add(new PropertyDefinition { Name = "Name" });

        var entityStorage = new EntityDefinition
        {
            Name = "EntityStorage",
            Plural = "EntityStorages",
        };
        entityStorage.Properties.Add(new PropertyDefinition { Name = "EntityName" });
        entityStorage.Properties.Add(new PropertyDefinition { Name = "StorageKind" });
        entityStorage.Properties.Add(new PropertyDefinition { Name = "DirectoryPath", IsNullable = true });
        entityStorage.Properties.Add(new PropertyDefinition { Name = "FilePath", IsNullable = true });
        entityStorage.Properties.Add(new PropertyDefinition { Name = "Pattern", IsNullable = true });
        entityStorage.Relationships.Add(new RelationshipDefinition { Entity = "Workspace" });

        model.Entities.Add(workspace);
        model.Entities.Add(workspaceLayout);
        model.Entities.Add(encoding);
        model.Entities.Add(newlines);
        model.Entities.Add(canonicalOrder);
        model.Entities.Add(entityStorage);
        return model;
    }
}
