using MetadataStudio.Core.Domain;

namespace MetadataStudio.Core.WorkspaceConfig;

internal static class MetaWorkspaceManifestAdapter
{
    public static MetaWorkspaceData ToMetaWorkspaceData(WorkspaceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.CanonicalSort ??= new CanonicalSortManifest();
        manifest.EntityStorages ??= new List<EntityStorageManifest>();

        var canonicalOrderRows = new List<CanonicalOrderRow>();
        var canonicalOrderIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int EnsureCanonicalOrderId(string value)
        {
            var key = (value ?? string.Empty).Trim();
            if (canonicalOrderIds.TryGetValue(key, out var existingId))
            {
                return existingId;
            }

            var id = canonicalOrderRows.Count + 1;
            canonicalOrderIds[key] = id;
            canonicalOrderRows.Add(new CanonicalOrderRow(
                Id: id,
                Name: key));
            return id;
        }

        var entitiesOrderId = EnsureCanonicalOrderId(manifest.CanonicalSort.Entities);
        var propertiesOrderId = EnsureCanonicalOrderId(manifest.CanonicalSort.Properties);
        var relationshipsOrderId = EnsureCanonicalOrderId(manifest.CanonicalSort.Relationships);
        var rowsOrderId = EnsureCanonicalOrderId(manifest.CanonicalSort.Rows);
        var attributesOrderId = EnsureCanonicalOrderId(manifest.CanonicalSort.Attributes);

        var workspaceRow = new WorkspaceRow(
            Id: 1,
            Name: MetaWorkspaceModels.DefaultWorkspaceName,
            FormatVersion: manifest.ContractVersion,
            WorkspaceLayoutId: 1,
            EncodingId: 1,
            NewlinesId: 1,
            EntitiesOrderId: entitiesOrderId,
            PropertiesOrderId: propertiesOrderId,
            RelationshipsOrderId: relationshipsOrderId,
            RowsOrderId: rowsOrderId,
            AttributesOrderId: attributesOrderId);

        var layoutRow = new WorkspaceLayoutRow(
            Id: workspaceRow.WorkspaceLayoutId,
            ModelFilePath: manifest.ModelFile,
            InstanceDirPath: manifest.InstanceDir);

        var encodingRow = new EncodingRow(
            Id: workspaceRow.EncodingId,
            Name: manifest.Encoding);

        var newlinesRow = new NewlinesRow(
            Id: workspaceRow.NewlinesId,
            Name: manifest.Newlines);

        var entityStorageRows = manifest.EntityStorages
            .Where(item => item != null)
            .Select((item, index) => new EntityStorageRow(
                Id: index + 1,
                WorkspaceId: workspaceRow.Id,
                EntityName: item.EntityName,
                StorageKind: item.StorageKind,
                DirectoryPath: item.DirectoryPath,
                FilePath: item.FilePath,
                Pattern: item.Pattern))
            .ToArray();

        return new MetaWorkspaceData(
            new Workspaces(new[] { workspaceRow }),
            new WorkspaceLayouts(new[] { layoutRow }),
            new Encodings(new[] { encodingRow }),
            new NewlinesValues(new[] { newlinesRow }),
            new CanonicalOrders(canonicalOrderRows),
            new EntityStorages(entityStorageRows));
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

        var layout = FindWorkspaceLayout(snapshot.WorkspaceLayouts, workspaceRow.WorkspaceLayoutId, workspaceXmlPath);
        var encoding = FindNamedRow(snapshot.Encodings, workspaceRow.EncodingId, "Encoding", workspaceXmlPath);
        var newlines = FindNamedRow(snapshot.NewlinesValues, workspaceRow.NewlinesId, "Newlines", workspaceXmlPath);
        var entitiesOrder = FindNamedRow(snapshot.CanonicalOrders, workspaceRow.EntitiesOrderId, "CanonicalOrder.EntitiesOrder", workspaceXmlPath);
        var propertiesOrder = FindNamedRow(snapshot.CanonicalOrders, workspaceRow.PropertiesOrderId, "CanonicalOrder.PropertiesOrder", workspaceXmlPath);
        var relationshipsOrder = FindNamedRow(snapshot.CanonicalOrders, workspaceRow.RelationshipsOrderId, "CanonicalOrder.RelationshipsOrder", workspaceXmlPath);
        var rowsOrder = FindNamedRow(snapshot.CanonicalOrders, workspaceRow.RowsOrderId, "CanonicalOrder.RowsOrder", workspaceXmlPath);
        var attributesOrder = FindNamedRow(snapshot.CanonicalOrders, workspaceRow.AttributesOrderId, "CanonicalOrder.AttributesOrder", workspaceXmlPath);

        var manifest = WorkspaceManifest.CreateDefault();
        manifest.ContractVersion = workspaceRow.FormatVersion;
        manifest.ModelFile = layout.ModelFilePath;
        manifest.InstanceDir = layout.InstanceDirPath;
        manifest.Encoding = encoding.Name;
        manifest.Newlines = newlines.Name;
        manifest.CanonicalSort.Entities = entitiesOrder.Name;
        manifest.CanonicalSort.Properties = propertiesOrder.Name;
        manifest.CanonicalSort.Relationships = relationshipsOrder.Name;
        manifest.CanonicalSort.Rows = rowsOrder.Name;
        manifest.CanonicalSort.Attributes = attributesOrder.Name;

        var entityStorages = snapshot.EntityStorages
            .Where(item => item.WorkspaceId == workspaceRow.Id)
            .OrderBy(item => item.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => new EntityStorageManifest
            {
                EntityName = item.EntityName,
                StorageKind = item.StorageKind,
                DirectoryPath = item.DirectoryPath,
                FilePath = item.FilePath,
                Pattern = item.Pattern,
            })
            .ToList();

        EnsureNoDuplicateEntityStorageRows(entityStorages, workspaceXmlPath);
        manifest.EntityStorages = entityStorages;

        return manifest;
    }

    private static WorkspaceLayoutRow FindWorkspaceLayout(
        IEnumerable<WorkspaceLayoutRow> rows,
        int expectedId,
        string sourcePath)
    {
        var matches = rows
            .Where(row => row.Id == expectedId)
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count == 0)
        {
            throw new InvalidDataException(
                $"Workspace config '{sourcePath}' must contain exactly one WorkspaceLayout row with Id '{expectedId}'.");
        }

        throw new InvalidDataException(
            $"Workspace config '{sourcePath}' contains duplicate WorkspaceLayout rows with Id '{expectedId}'.");
    }

    private static TRow FindNamedRow<TRow>(
        IEnumerable<TRow> rows,
        int expectedId,
        string rowName,
        string sourcePath)
        where TRow : INamedRow
    {
        var matches = rows
            .Where(row => row.Id == expectedId)
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count == 0)
        {
            throw new InvalidDataException(
                $"Workspace config '{sourcePath}' must contain exactly one {rowName} row with Id '{expectedId}'.");
        }

        throw new InvalidDataException(
            $"Workspace config '{sourcePath}' contains duplicate {rowName} rows with Id '{expectedId}'.");
    }

    private static void EnsureNoDuplicateEntityStorageRows(
        IReadOnlyCollection<EntityStorageManifest> rows,
        string sourcePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.EntityName))
            {
                throw new InvalidDataException(
                    $"Workspace config '{sourcePath}' contains EntityStorage row with empty EntityName.");
            }

            if (!seen.Add(row.EntityName.Trim()))
            {
                throw new InvalidDataException(
                    $"Workspace config '{sourcePath}' contains duplicate EntityStorage rows for entity '{row.EntityName}'.");
            }
        }
    }
}
