using System;
using System.Linq;
using Meta.Core.Domain;
using Meta.Core.Services;
using DomainWorkspace = Meta.Core.Domain.Workspace;

namespace Meta.Core.Tests;

public sealed class ModelSuggestServiceTests
{
    [Fact]
    public void Analyze_DemoProducesEligibleRelationshipSuggestions()
    {
        var workspace = BuildDemoWorkspace();

        var report = ModelSuggestService.Analyze(workspace);

        Assert.Contains(report.BusinessKeys, item => IsTarget(item, "Warehouse", "WarehouseCode"));
        Assert.Contains(report.BusinessKeys, item => IsTarget(item, "Product", "ProductCode"));
        Assert.Contains(report.BusinessKeys, item => IsTarget(item, "Supplier", "SupplierCode"));
        Assert.Contains(report.BusinessKeys, item => IsTarget(item, "Category", "CategoryCode"));

        AssertEligible(report, "Order", "WarehouseCode", "Warehouse", "WarehouseCode");
        AssertEligible(report, "Order", "ProductCode", "Product", "ProductCode");
        AssertEligible(report, "Order", "SupplierCode", "Supplier", "SupplierCode");
    }

    [Fact]
    public void Analyze_UnmatchedSourceValue_IsBlockedAndNotEligible()
    {
        var workspace = BuildDemoWorkspace();
        AddRow(workspace.Instance, "Order", "6",
            ("OrderNumber", "ORD-1006"),
            ("ProductCode", "PRD-404"),
            ("SupplierCode", "SUP-001"),
            ("WarehouseCode", "WH-001"),
            ("StatusText", "Held"));

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Order", "ProductCode", "Product", "ProductCode");
        var blocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Order", "ProductCode", "Product", "ProductCode");
        Assert.Equal(LookupCandidateStatus.Blocked, blocked.Status);
        Assert.Contains("Source values not fully resolvable against target key.", blocked.Blockers);
        Assert.Contains("PRD-404", blocked.UnmatchedDistinctValuesSample);
    }

    [Fact]
    public void Analyze_TargetDuplicateLookupKey_IsBlocked()
    {
        var workspace = BuildDemoWorkspace();
        AddRow(workspace.Instance, "Warehouse", "4", ("WarehouseName", "Seattle Duplicate"), ("WarehouseCode", "WH-001"));

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Order", "WarehouseCode", "Warehouse", "WarehouseCode");
        var blocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Order", "WarehouseCode", "Warehouse", "WarehouseCode");
        Assert.Equal(LookupCandidateStatus.Blocked, blocked.Status);
        Assert.Contains("Target lookup key is not unique.", blocked.Blockers);
    }

    [Fact]
    public void Analyze_SourceNullOrBlank_IsBlocked()
    {
        var workspace = BuildDemoWorkspace();
        AddRow(workspace.Instance, "Order", "6",
            ("OrderNumber", "ORD-1006"),
            ("ProductCode", string.Empty),
            ("SupplierCode", "SUP-001"),
            ("WarehouseCode", "WH-001"),
            ("StatusText", "Held"));

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Order", "ProductCode", "Product", "ProductCode");
        var blocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Order", "ProductCode", "Product", "ProductCode");
        Assert.Equal(LookupCandidateStatus.Blocked, blocked.Status);
        Assert.Contains("Source contains null/blank; required relationship cannot be created.", blocked.Blockers);
    }

    [Fact]
    public void Analyze_TargetNullOrBlank_IsBlocked()
    {
        var workspace = BuildDemoWorkspace();
        AddRow(workspace.Instance, "Product", "5", ("ProductName", "Unknown"), ("ProductCode", string.Empty), ("ProductGroup", "Cycles"));

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Order", "ProductCode", "Product", "ProductCode");
        var blocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Order", "ProductCode", "Product", "ProductCode");
        Assert.Equal(LookupCandidateStatus.Blocked, blocked.Status);
        Assert.Contains("Target lookup key has null/blank values.", blocked.Blockers);
    }

    [Fact]
    public void Analyze_ExistingRelationship_IsBlockedAndNotEligible()
    {
        var workspace = BuildDemoWorkspace();
        workspace.Model.FindEntity("Order")!.Relationships.Add(new GenericRelationship
        {
            Entity = "Warehouse",
        });

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Order", "WarehouseCode", "Warehouse", "WarehouseCode");
        var blocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Order", "WarehouseCode", "Warehouse", "WarehouseCode");
        Assert.Equal(LookupCandidateStatus.Blocked, blocked.Status);
        Assert.Contains("Relationship 'Order.WarehouseId' already exists.", blocked.Blockers);
    }

    [Fact]
    public void Analyze_SymmetricPeerKeys_AreBlockedAsAmbiguous()
    {
        var workspace = CreateWorkspaceSkeleton();
        AddEntity(workspace.Model, "Left", "Code");
        AddEntity(workspace.Model, "Right", "Code");

        AddRow(workspace.Instance, "Left", "1", ("Code", "A1"));
        AddRow(workspace.Instance, "Left", "2", ("Code", "A2"));
        AddRow(workspace.Instance, "Right", "1", ("Code", "A1"));
        AddRow(workspace.Instance, "Right", "2", ("Code", "A2"));

        var report = ModelSuggestService.Analyze(workspace);

        AssertNotEligible(report, "Left", "Code", "Right", "Code");
        AssertNotEligible(report, "Right", "Code", "Left", "Code");

        var leftBlocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Left", "Code", "Right", "Code");
        Assert.Equal(LookupCandidateStatus.Blocked, leftBlocked.Status);
        Assert.Contains("Source does not show reuse; lookup direction is ambiguous.", leftBlocked.Blockers);

        var rightBlocked = ModelSuggestService.AnalyzeLookupRelationship(workspace, "Right", "Code", "Left", "Code");
        Assert.Equal(LookupCandidateStatus.Blocked, rightBlocked.Status);
        Assert.Contains("Source does not show reuse; lookup direction is ambiguous.", rightBlocked.Blockers);
    }

    [Fact]
    public void Analyze_IsDeterministic_ForStableWorkspaceState()
    {
        var workspace = BuildDemoWorkspace();

        var first = ModelSuggestService.Analyze(workspace);
        var second = ModelSuggestService.Analyze(workspace);

        var firstEligible = first.EligibleRelationshipSuggestions
            .Select(ToProjection)
            .ToArray();
        var secondEligible = second.EligibleRelationshipSuggestions
            .Select(ToProjection)
            .ToArray();
        Assert.Equal(firstEligible, secondEligible);
    }

    private static string ToProjection(LookupRelationshipSuggestion suggestion)
    {
        return string.Join(
            "|",
            suggestion.Source.EntityName,
            suggestion.Source.PropertyName,
            suggestion.TargetLookup.EntityName,
            suggestion.TargetLookup.PropertyName,
            suggestion.Status.ToString(),
            suggestion.Score.ToString("0.000"),
            string.Join(";", suggestion.Blockers),
            string.Join(",", suggestion.UnmatchedDistinctValuesSample));
    }

    private static bool IsTarget(BusinessKeyCandidate candidate, string entityName, string propertyName)
    {
        return string.Equals(candidate.Target.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.Target.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertEligible(
        ModelSuggestReport report,
        string sourceEntity,
        string sourceProperty,
        string targetEntity,
        string targetProperty)
    {
        Assert.Contains(
            report.EligibleRelationshipSuggestions,
            item => Matches(item, sourceEntity, sourceProperty, targetEntity, targetProperty));
    }

    private static void AssertNotEligible(
        ModelSuggestReport report,
        string sourceEntity,
        string sourceProperty,
        string targetEntity,
        string targetProperty)
    {
        Assert.DoesNotContain(
            report.EligibleRelationshipSuggestions,
            item => Matches(item, sourceEntity, sourceProperty, targetEntity, targetProperty));
    }

    private static bool Matches(
        LookupRelationshipSuggestion suggestion,
        string sourceEntity,
        string sourceProperty,
        string targetEntity,
        string targetProperty)
    {
        return string.Equals(suggestion.Source.EntityName, sourceEntity, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(suggestion.Source.PropertyName, sourceProperty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(suggestion.TargetLookup.EntityName, targetEntity, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(suggestion.TargetLookup.PropertyName, targetProperty, StringComparison.OrdinalIgnoreCase);
    }

    private static DomainWorkspace BuildDemoWorkspace()
    {
        var workspace = CreateWorkspaceSkeleton();
        AddEntity(workspace.Model, "Product", "ProductName", "ProductCode", "ProductGroup");
        AddEntity(workspace.Model, "Supplier", "SupplierName", "SupplierCode");
        AddEntity(workspace.Model, "Category", "CategoryName", "CategoryCode");
        AddEntity(workspace.Model, "Warehouse", "WarehouseName", "WarehouseCode");
        AddEntity(workspace.Model, "Order", "OrderNumber", "ProductCode", "SupplierCode", "WarehouseCode", "StatusText");

        AddRow(workspace.Instance, "Product", "1", ("ProductName", "Road Bike"), ("ProductCode", "PRD-001"), ("ProductGroup", "Cycles"));
        AddRow(workspace.Instance, "Product", "2", ("ProductName", "Touring Bike"), ("ProductCode", "PRD-002"), ("ProductGroup", "Cycles"));
        AddRow(workspace.Instance, "Product", "3", ("ProductName", "Bottle Cage"), ("ProductCode", "PRD-003"), ("ProductGroup", "Accessories"));
        AddRow(workspace.Instance, "Product", "4", ("ProductName", "Water Bottle"), ("ProductCode", "PRD-004"), ("ProductGroup", "Accessories"));

        AddRow(workspace.Instance, "Supplier", "1", ("SupplierName", "Northwind Parts"), ("SupplierCode", "SUP-001"));
        AddRow(workspace.Instance, "Supplier", "2", ("SupplierName", "Contoso Gear"), ("SupplierCode", "SUP-002"));
        AddRow(workspace.Instance, "Supplier", "3", ("SupplierName", string.Empty), ("SupplierCode", "SUP-003"));
        AddRow(workspace.Instance, "Supplier", "4", ("SupplierName", "Adventure Works"), ("SupplierCode", "SUP-004"));

        AddRow(workspace.Instance, "Category", "1", ("CategoryName", "Cycles"), ("CategoryCode", "CAT-001"));
        AddRow(workspace.Instance, "Category", "2", ("CategoryName", "Accessories"), ("CategoryCode", "CAT-002"));
        AddRow(workspace.Instance, "Category", "3", ("CategoryName", "Maintenance"), ("CategoryCode", "CAT-003"));

        AddRow(workspace.Instance, "Warehouse", "1", ("WarehouseName", "Seattle Main"), ("WarehouseCode", "WH-001"));
        AddRow(workspace.Instance, "Warehouse", "2", ("WarehouseName", "Denver Hub"), ("WarehouseCode", "WH-002"));
        AddRow(workspace.Instance, "Warehouse", "3", ("WarehouseName", "Dallas Overflow"), ("WarehouseCode", "WH-003"));

        AddRow(workspace.Instance, "Order", "1", ("OrderNumber", "ORD-1001"), ("ProductCode", "PRD-001"), ("SupplierCode", "SUP-001"), ("WarehouseCode", "WH-001"), ("StatusText", "Released"));
        AddRow(workspace.Instance, "Order", "2", ("OrderNumber", "ORD-1002"), ("ProductCode", "PRD-002"), ("SupplierCode", "SUP-002"), ("WarehouseCode", "WH-001"), ("StatusText", "Released"));
        AddRow(workspace.Instance, "Order", "3", ("OrderNumber", "ORD-1003"), ("ProductCode", "PRD-003"), ("SupplierCode", "SUP-003"), ("WarehouseCode", "WH-002"), ("StatusText", "Held"));
        AddRow(workspace.Instance, "Order", "4", ("OrderNumber", "ORD-1004"), ("ProductCode", "PRD-004"), ("SupplierCode", "SUP-004"), ("WarehouseCode", "WH-003"), ("StatusText", "Closed"));
        AddRow(workspace.Instance, "Order", "5", ("OrderNumber", "ORD-1005"), ("ProductCode", "PRD-001"), ("SupplierCode", "SUP-002"), ("WarehouseCode", "WH-001"), ("StatusText", "Released"));

        return workspace;
    }

    private static DomainWorkspace CreateWorkspaceSkeleton()
    {
        return new DomainWorkspace
        {
            WorkspaceRootPath = "C:\\test\\workspace",
            MetadataRootPath = "C:\\test\\workspace\\metadata",
            Model = new GenericModel
            {
                Name = "SuggestModel",
            },
            Instance = new GenericInstance
            {
                ModelName = "SuggestModel",
            },
        };
    }

    private static void AddEntity(GenericModel model, string entityName, params string[] propertyNames)
    {
        var entity = new GenericEntity
        {
            Name = entityName,
        };

        foreach (var propertyName in propertyNames)
        {
            entity.Properties.Add(new GenericProperty
            {
                Name = propertyName,
                DataType = "string",
                IsNullable = false,
            });
        }

        model.Entities.Add(entity);
    }

    private static void AddRow(GenericInstance instance, string entityName, string id, params (string Key, string Value)[] values)
    {
        var row = new GenericRecord
        {
            Id = id,
        };

        foreach (var (key, value) in values)
        {
            row.Values[key] = value;
        }

        instance.GetOrCreateEntityRecords(entityName).Add(row);
    }
}
