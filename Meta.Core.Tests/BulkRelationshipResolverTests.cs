using System;
using Meta.Core.Domain;
using Meta.Core.Operations;
using Meta.Core.Services;

namespace Meta.Core.Tests;

public sealed class BulkRelationshipResolverTests
{
    [Fact]
    public void ResolveRelationshipIds_ResolvesLiteralAndPrefixedIds()
    {
        var workspace = BuildWorkspace();
        var operation = new WorkspaceOp
        {
            Type = WorkspaceOpTypes.BulkUpsertRows,
            EntityName = "Measure",
            RowPatches =
            {
                new RowPatch
                {
                    Id = "1",
                    Values = { ["MeasureName"] = "Orders" },
                    RelationshipIds = { ["CubeId"] = "2" },
                },
                new RowPatch
                {
                    Id = "2",
                    Values = { ["MeasureName"] = "Revenue" },
                    RelationshipIds = { ["CubeId"] = "id:1" },
                },
            },
        };

        BulkRelationshipResolver.ResolveRelationshipIds(workspace, operation);

        Assert.Equal("2", operation.RowPatches[0].RelationshipIds["CubeId"]);
        Assert.Equal("1", operation.RowPatches[1].RelationshipIds["CubeId"]);
    }

    [Fact]
    public void ResolveRelationshipIds_RejectsLegacySymbolicRowReference()
    {
        var workspace = BuildWorkspace();
        var legacyRowReference = string.Concat("Cube", "#", "1");
        var operation = new WorkspaceOp
        {
            Type = WorkspaceOpTypes.BulkUpsertRows,
            EntityName = "Measure",
            RowPatches =
            {
                new RowPatch
                {
                    Id = "1",
                    RelationshipIds = { ["CubeId"] = legacyRowReference },
                },
            },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BulkRelationshipResolver.ResolveRelationshipIds(workspace, operation));
        Assert.Contains("unsupported symbolic row reference", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Workspace BuildWorkspace()
    {
        var workspace = new Workspace
        {
            WorkspaceRootPath = "memory",
            MetadataRootPath = "memory/metadata",
            Model = new ModelDefinition
            {
                Name = "TestModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "TestModel",
            },
        };

        var cube = new EntityDefinition
        {
            Name = "Cube",
        };
        cube.Properties.Add(new PropertyDefinition { Name = "CubeName", DataType = "string", IsNullable = false });
        workspace.Model.Entities.Add(cube);

        var measure = new EntityDefinition
        {
            Name = "Measure",
        };
        measure.Properties.Add(new PropertyDefinition { Name = "MeasureName", DataType = "string", IsNullable = false });
        measure.Relationships.Add(new RelationshipDefinition { Entity = "Cube" });
        workspace.Model.Entities.Add(measure);

        var cubeRows = workspace.Instance.GetOrCreateEntityRecords("Cube");
        cubeRows.Add(new InstanceRecord
        {
            Id = "1",
            Values =
            {
                ["CubeName"] = "Sales",
            },
        });
        cubeRows.Add(new InstanceRecord
        {
            Id = "2",
            Values =
            {
                ["CubeName"] = "Finance",
            },
        });

        return workspace;
    }
}
