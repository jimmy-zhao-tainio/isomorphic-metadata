using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meta.Adapters;
using Meta.Core.Services;

namespace Meta.Core.Tests;

public sealed class ModelRefactorServiceTests
{
    [Fact]
    public async Task RefactorPropertyToRelationship_RewritesLandingRowsInMemory()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(
            FindRepositoryRoot(),
            "Samples",
            "Demos",
            "SuggestDemo",
            "Workspace"));

        var result = services.ModelRefactorService.RefactorPropertyToRelationship(
            workspace,
            new PropertyToRelationshipRefactorOptions(
                SourceEntityName: "Order",
                SourcePropertyName: "WarehouseId",
                TargetEntityName: "Warehouse",
                LookupPropertyName: "Id",
                Role: string.Empty,
                DropSourceProperty: true));

        Assert.Equal(5, result.RowsRewritten);
        Assert.True(result.PropertyDropped);

        var orderEntity = workspace.Model.FindEntity("Order");
        Assert.NotNull(orderEntity);
        Assert.DoesNotContain(orderEntity!.Properties, item => string.Equals(item.Name, "WarehouseId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(orderEntity.Relationships, item => string.Equals(item.Entity, "Warehouse", StringComparison.OrdinalIgnoreCase));

        var orderRows = workspace.Instance.GetOrCreateEntityRecords("Order");
        Assert.All(orderRows, row =>
        {
            Assert.False(row.Values.ContainsKey("WarehouseId"));
            Assert.True(row.RelationshipIds.TryGetValue("WarehouseId", out var fkValue));
            Assert.False(string.IsNullOrWhiteSpace(fkValue));
        });
    }

    [Fact]
    public async Task RelationshipToProperty_RoundTripsPropertyShapeInMemory()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(
            FindRepositoryRoot(),
            "Samples",
            "Demos",
            "SuggestDemo",
            "Workspace"));

        services.ModelRefactorService.RefactorPropertyToRelationship(
            workspace,
            new PropertyToRelationshipRefactorOptions(
                SourceEntityName: "Order",
                SourcePropertyName: "WarehouseId",
                TargetEntityName: "Warehouse",
                LookupPropertyName: "Id",
                Role: string.Empty,
                DropSourceProperty: true));

        var result = services.ModelRefactorService.RefactorRelationshipToProperty(
            workspace,
            new RelationshipToPropertyRefactorOptions(
                SourceEntityName: "Order",
                TargetEntityName: "Warehouse",
                Role: string.Empty,
                PropertyName: string.Empty));

        Assert.Equal(5, result.RowsRewritten);
        Assert.Equal("WarehouseId", result.PropertyName);

        var orderEntity = workspace.Model.FindEntity("Order");
        Assert.NotNull(orderEntity);
        Assert.Contains(orderEntity!.Properties, item => string.Equals(item.Name, "WarehouseId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orderEntity.Relationships, item => string.Equals(item.Entity, "Warehouse", StringComparison.OrdinalIgnoreCase));

        var orderRows = workspace.Instance.GetOrCreateEntityRecords("Order");
        Assert.All(orderRows, row =>
        {
            Assert.True(row.Values.TryGetValue("WarehouseId", out var propertyValue));
            Assert.False(string.IsNullOrWhiteSpace(propertyValue));
            Assert.False(row.RelationshipIds.ContainsKey("WarehouseId"));
        });
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Metadata.Framework.sln")))
            {
                return directory;
            }

            var parent = Directory.GetParent(directory);
            if (parent == null)
            {
                break;
            }

            directory = parent.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }
}
