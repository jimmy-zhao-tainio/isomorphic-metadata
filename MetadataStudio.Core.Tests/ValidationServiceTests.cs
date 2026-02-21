using System.Linq;
using MetadataStudio.Core.Domain;
using MetadataStudio.Core.Services;

namespace MetadataStudio.Core.Tests;

public sealed class ValidationServiceTests
{
    [Fact]
    public void Validate_ReservedKeywords_AreErrors()
    {
        var workspace = BuildWorkspace(
            modelName: "select",
            entityName: "class",
            propertyName: "from");

        var diagnostics = new ValidationService().Validate(workspace);

        Assert.Contains(diagnostics.Issues, issue => issue.Code == "name.reserved.csharp");
        Assert.Contains(diagnostics.Issues, issue => issue.Code == "name.reserved.sql");
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void Validate_ModelAndEntityNameCollision_IsError()
    {
        var workspace = BuildWorkspace(
            modelName: "Cube",
            entityName: "Cube",
            propertyName: "CubeName");

        var diagnostics = new ValidationService().Validate(workspace);

        Assert.Contains(diagnostics.Issues, issue => issue.Code == "model.entity.collision");
    }

    [Fact]
    public void Validate_PropertyRelationshipNameCollision_IsError()
    {
        var workspace = new Workspace
        {
            Model = new ModelDefinition
            {
                Name = "MetadataModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "MetadataModel",
            },
        };

        var cube = new EntityDefinition { Name = "Cube" };
        workspace.Model.Entities.Add(cube);

        var measure = new EntityDefinition { Name = "Measure" };
        measure.Properties.Add(new PropertyDefinition { Name = "Cube", DataType = "string", IsNullable = false });
        measure.Relationships.Add(new RelationshipDefinition { Entity = "Cube" });
        workspace.Model.Entities.Add(measure);

        var diagnostics = new ValidationService().Validate(workspace);

        Assert.Contains(diagnostics.Issues, issue => issue.Code == "entity.member.collision");
    }

    [Fact]
    public void Validate_RelationshipCycle_IsError()
    {
        var workspace = new Workspace
        {
            Model = new ModelDefinition
            {
                Name = "MetadataModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "MetadataModel",
            },
        };

        var entityA = new EntityDefinition { Name = "EntityA" };
        entityA.Relationships.Add(new RelationshipDefinition { Entity = "EntityB" });

        var entityB = new EntityDefinition { Name = "EntityB" };
        entityB.Relationships.Add(new RelationshipDefinition { Entity = "EntityA" });

        workspace.Model.Entities.Add(entityA);
        workspace.Model.Entities.Add(entityB);

        var diagnostics = new ValidationService().Validate(workspace);

        Assert.Contains(diagnostics.Issues, issue => issue.Code == "relationship.cycle");
        Assert.True(diagnostics.HasErrors);
    }

    private static Workspace BuildWorkspace(string modelName, string entityName, string propertyName)
    {
        var workspace = new Workspace
        {
            Model = new ModelDefinition
            {
                Name = modelName,
            },
            Instance = new InstanceStore
            {
                ModelName = modelName,
            },
        };

        var entity = new EntityDefinition
        {
            Name = entityName,
        };
        entity.Properties.Add(new PropertyDefinition
        {
            Name = propertyName,
            DataType = "string",
            IsNullable = false,
        });

        workspace.Model.Entities.Add(entity);
        return workspace;
    }
}
