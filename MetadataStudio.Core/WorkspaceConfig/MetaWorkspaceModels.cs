using MetadataStudio.Core.Domain;

namespace MetadataStudio.Core.WorkspaceConfig;

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

        var workspacePath = new EntityDefinition
        {
            Name = "WorkspacePath",
        };
        workspacePath.Properties.Add(new PropertyDefinition { Name = "Key" });
        workspacePath.Properties.Add(new PropertyDefinition { Name = "Path" });
        workspacePath.Relationships.Add(new RelationshipDefinition { Entity = "Workspace" });

        var generatorSetting = new EntityDefinition
        {
            Name = "GeneratorSetting",
        };
        generatorSetting.Properties.Add(new PropertyDefinition { Name = "Key" });
        generatorSetting.Properties.Add(new PropertyDefinition { Name = "Value" });
        generatorSetting.Relationships.Add(new RelationshipDefinition { Entity = "Workspace" });

        model.Entities.Add(workspace);
        model.Entities.Add(workspacePath);
        model.Entities.Add(generatorSetting);
        return model;
    }
}
