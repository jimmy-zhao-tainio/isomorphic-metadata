using MetadataStudio.Core.Domain;

namespace MetadataStudio.Core.WorkspaceConfig;

internal static class MetaWorkspaceManifestAdapter
{
    private const string ModelFileKey = "ModelFile";
    private const string InstanceDirKey = "InstanceDir";
    private const string EncodingKey = "Encoding";
    private const string NewlinesKey = "Newlines";
    private const string CanonicalSortEntitiesKey = "CanonicalSort.Entities";
    private const string CanonicalSortPropertiesKey = "CanonicalSort.Properties";
    private const string CanonicalSortRelationshipsKey = "CanonicalSort.Relationships";
    private const string CanonicalSortRowsKey = "CanonicalSort.Rows";
    private const string CanonicalSortAttributesKey = "CanonicalSort.Attributes";

    public static MetaWorkspaceData ToMetaWorkspaceData(WorkspaceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var workspaceRow = new WorkspaceRow(
            Id: 1,
            Name: MetaWorkspaceModels.DefaultWorkspaceName,
            FormatVersion: manifest.ContractVersion);

        var workspacePaths = new[]
        {
            new WorkspacePathRow(1, workspaceRow.Id, ModelFileKey, manifest.ModelFile),
            new WorkspacePathRow(2, workspaceRow.Id, InstanceDirKey, manifest.InstanceDir),
        };

        var settings = new[]
        {
            new GeneratorSettingRow(1, workspaceRow.Id, EncodingKey, manifest.Encoding),
            new GeneratorSettingRow(2, workspaceRow.Id, NewlinesKey, manifest.Newlines),
            new GeneratorSettingRow(3, workspaceRow.Id, CanonicalSortEntitiesKey, manifest.CanonicalSort.Entities),
            new GeneratorSettingRow(4, workspaceRow.Id, CanonicalSortPropertiesKey, manifest.CanonicalSort.Properties),
            new GeneratorSettingRow(5, workspaceRow.Id, CanonicalSortRelationshipsKey, manifest.CanonicalSort.Relationships),
            new GeneratorSettingRow(6, workspaceRow.Id, CanonicalSortRowsKey, manifest.CanonicalSort.Rows),
            new GeneratorSettingRow(7, workspaceRow.Id, CanonicalSortAttributesKey, manifest.CanonicalSort.Attributes),
        };

        return new MetaWorkspaceData(
            new Workspaces(new[] { workspaceRow }),
            new WorkspacePaths(workspacePaths),
            new GeneratorSettings(settings));
    }

    public static WorkspaceManifest ToWorkspaceManifest(MetaWorkspaceData snapshot, string workspaceXmlPath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var workspaceRows = snapshot.Workspaces.ToList();
        if (workspaceRows.Count != 1)
        {
            throw new InvalidDataException(
                $"Workspace config '{workspaceXmlPath}' must contain exactly one Workspace row.");
        }

        var workspaceRow = workspaceRows[0];
        var pathRows = snapshot.WorkspacePaths
            .Where(row => row.WorkspaceId == workspaceRow.Id)
            .ToList();
        var settingRows = snapshot.GeneratorSettings
            .Where(row => row.WorkspaceId == workspaceRow.Id)
            .ToList();

        var paths = BuildKeyedValueMap(pathRows.Select(row => (row.Key, row.Path)), "WorkspacePath", workspaceXmlPath);
        var settings = BuildKeyedValueMap(settingRows.Select(row => (row.Key, row.Value)), "GeneratorSetting", workspaceXmlPath);

        var manifest = WorkspaceManifest.CreateDefault();
        manifest.ContractVersion = workspaceRow.FormatVersion;
        if (paths.TryGetValue(ModelFileKey, out var modelFile))
        {
            manifest.ModelFile = modelFile;
        }

        if (paths.TryGetValue(InstanceDirKey, out var instanceDir))
        {
            manifest.InstanceDir = instanceDir;
        }

        if (settings.TryGetValue(EncodingKey, out var encoding))
        {
            manifest.Encoding = encoding;
        }

        if (settings.TryGetValue(NewlinesKey, out var newlines))
        {
            manifest.Newlines = newlines;
        }

        if (settings.TryGetValue(CanonicalSortEntitiesKey, out var entities))
        {
            manifest.CanonicalSort.Entities = entities;
        }

        if (settings.TryGetValue(CanonicalSortPropertiesKey, out var properties))
        {
            manifest.CanonicalSort.Properties = properties;
        }

        if (settings.TryGetValue(CanonicalSortRelationshipsKey, out var relationships))
        {
            manifest.CanonicalSort.Relationships = relationships;
        }

        if (settings.TryGetValue(CanonicalSortRowsKey, out var rows))
        {
            manifest.CanonicalSort.Rows = rows;
        }

        if (settings.TryGetValue(CanonicalSortAttributesKey, out var attributes))
        {
            manifest.CanonicalSort.Attributes = attributes;
        }

        return manifest;
    }

    private static Dictionary<string, string> BuildKeyedValueMap(
        IEnumerable<(string Key, string Value)> pairs,
        string entityName,
        string sourcePath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException(
                    $"Workspace config '{sourcePath}' contains {entityName} row with empty Key.");
            }

            if (!map.TryAdd(key, value))
            {
                throw new InvalidDataException(
                    $"Workspace config '{sourcePath}' contains duplicate {entityName} Key '{key}'.");
            }
        }

        return map;
    }
}
