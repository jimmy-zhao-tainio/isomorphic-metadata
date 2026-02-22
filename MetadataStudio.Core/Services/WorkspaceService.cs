using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MetadataStudio.Core.Domain;
using MetadataStudio.Core.WorkspaceConfig;

namespace MetadataStudio.Core.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private const int SupportedContractMajorVersion = 1;
    private const int SupportedContractMinorVersion = 0;
    private const string MetadataDirectoryName = "metadata";
    private const string WorkspaceXmlFileName = "workspace.xml";
    private const string ModelFileName = "model.xml";
    private const string InstanceDirectoryName = "instance";
    private const string InstanceFileName = "instance.xml";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public Task<Workspace> LoadAsync(
        string workspaceRootPath,
        bool searchUpward = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(workspaceRootPath))
        {
            throw new ArgumentException("Workspace path must not be empty.", nameof(workspaceRootPath));
        }

        var absoluteInputPath = Path.GetFullPath(workspaceRootPath);
        var paths = searchUpward
            ? DiscoverWorkspacePaths(absoluteInputPath)
            : ResolveWorkspacePathsFromRoot(absoluteInputPath);
        var manifest = ReadManifest(paths.WorkspaceRootPath, paths.MetadataRootPath);

        var modelPath = ResolveModelPath(paths.WorkspaceRootPath, paths.MetadataRootPath, manifest);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new FileNotFoundException(
                $"Could not find {ModelFileName} in '{paths.MetadataRootPath}'.");
        }

        var model = ReadModel(modelPath);
        var instance = ReadInstance(paths.WorkspaceRootPath, paths.MetadataRootPath, manifest, model);

        var workspace = new Workspace
        {
            WorkspaceRootPath = paths.WorkspaceRootPath,
            MetadataRootPath = paths.MetadataRootPath,
            Manifest = manifest,
            Model = model,
            Instance = instance,
            IsDirty = false,
        };

        return Task.FromResult(workspace);
    }

    public Task SaveAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        return SaveAsync(workspace, expectedFingerprint: null, cancellationToken);
    }

    public async Task SaveAsync(
        Workspace workspace,
        string? expectedFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (string.IsNullOrWhiteSpace(workspace.WorkspaceRootPath))
        {
            throw new InvalidOperationException("WorkspaceRootPath is required before save.");
        }

        if (workspace.Model == null)
        {
            throw new InvalidOperationException("Workspace model must be initialized.");
        }

        if (workspace.Instance == null)
        {
            throw new InvalidOperationException("Workspace instance must be initialized.");
        }

        var diagnostics = new ValidationService().Validate(workspace);
        workspace.Diagnostics = diagnostics;
        if (diagnostics.HasErrors)
        {
            var preview = diagnostics.Issues
                .Where(issue => issue.Severity == IssueSeverity.Error)
                .Take(5)
                .Select(issue => $"{issue.Code} {issue.Location} - {issue.Message}");
            throw new InvalidOperationException(
                "Workspace validation failed before save: " + string.Join(" | ", preview));
        }

        var workspaceRoot = Path.GetFullPath(workspace.WorkspaceRootPath);
        var manifest = NormalizeManifest(workspace.Manifest);
        var metadataRootPath = Path.Combine(workspaceRoot, MetadataDirectoryName);
        using var writeLock = WorkspaceWriteLock.Acquire(workspaceRoot);

        if (!string.IsNullOrWhiteSpace(expectedFingerprint))
        {
            var currentFingerprint = await TryCalculateCurrentFingerprintAsync(workspaceRoot, cancellationToken).ConfigureAwait(false);
            var normalizedCurrent = currentFingerprint ?? string.Empty;
            if (!string.Equals(normalizedCurrent, expectedFingerprint.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkspaceConflictException(
                    $"Workspace fingerprint mismatch. Expected '{expectedFingerprint}', found '{normalizedCurrent}'.",
                    expectedFingerprint.Trim(),
                    normalizedCurrent);
            }
        }

        var modelPath = ResolvePathFromWorkspaceRoot(workspaceRoot, manifest.ModelFile);
        var instanceDirectoryPath = ResolvePathFromWorkspaceRoot(workspaceRoot, manifest.InstanceDir);
        EnsurePathUnderMetadataRoot(modelPath, metadataRootPath, nameof(manifest.ModelFile));
        EnsurePathUnderMetadataRoot(instanceDirectoryPath, metadataRootPath, nameof(manifest.InstanceDir));

        var stagingMetadataRootPath = Path.Combine(
            workspaceRoot,
            MetadataDirectoryName + ".__staging." + Guid.NewGuid().ToString("N"));
        var backupMetadataRootPath = Path.Combine(
            workspaceRoot,
            MetadataDirectoryName + ".__backup." + Guid.NewGuid().ToString("N"));

        var stagingModelPath = MapPathToStagingMetadataRoot(metadataRootPath, stagingMetadataRootPath, modelPath);
        var stagingInstanceDirectoryPath =
            MapPathToStagingMetadataRoot(metadataRootPath, stagingMetadataRootPath, instanceDirectoryPath);

        Directory.CreateDirectory(stagingMetadataRootPath);
        try
        {
            WriteManifestToFile(manifest, Path.Combine(stagingMetadataRootPath, WorkspaceXmlFileName));

            WriteXmlToFile(BuildModelDocument(workspace.Model), stagingModelPath, indented: true);
            WriteInstanceShards(workspace, stagingInstanceDirectoryPath);
            DeleteIfExists(Path.Combine(stagingMetadataRootPath, InstanceFileName));

            SwapMetadataDirectories(metadataRootPath, stagingMetadataRootPath, backupMetadataRootPath);
        }
        finally
        {
            DeleteDirectoryIfExists(stagingMetadataRootPath);
            DeleteDirectoryIfExists(backupMetadataRootPath);
        }

        workspace.WorkspaceRootPath = workspaceRoot;
        workspace.MetadataRootPath = metadataRootPath;
        workspace.Manifest = manifest;
        workspace.IsDirty = false;
    }

    private async Task<string?> TryCalculateCurrentFingerprintAsync(
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentWorkspace = await LoadAsync(workspaceRootPath, searchUpward: false, cancellationToken).ConfigureAwait(false);
            return CalculateHash(currentWorkspace);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public string CalculateHash(Workspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        using var sha256 = SHA256.Create();
        var manifestCanonical = SerializeManifest(NormalizeManifest(workspace.Manifest));
        var modelCanonical = SerializeXml(BuildModelDocument(workspace.Model), indented: false);
        var shardCanonicalPayload = BuildShardCanonicalPayload(workspace);
        var payload = manifestCanonical + "\n---\n" + modelCanonical + "\n---\n" + shardCanonicalPayload;
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static WorkspacePaths DiscoverWorkspacePaths(string inputPath)
    {
        var initialDirectory = Directory.Exists(inputPath)
            ? inputPath
            : Path.GetDirectoryName(inputPath) ?? inputPath;
        var current = Path.GetFullPath(initialDirectory);

        while (!string.IsNullOrWhiteSpace(current))
        {
            var metadataRootPath = Path.Combine(current, MetadataDirectoryName);
            var workspaceXmlPath = Path.Combine(metadataRootPath, WorkspaceXmlFileName);

            if (File.Exists(workspaceXmlPath))
            {
                return new WorkspacePaths(current, metadataRootPath);
            }

            if (Directory.Exists(metadataRootPath) && IsMetadataDirectoryCandidate(metadataRootPath))
            {
                return new WorkspacePaths(current, metadataRootPath);
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }

        var fallbackRoot = Path.GetFullPath(initialDirectory);
        var fallbackMetadata = ResolveMetadataRoot(fallbackRoot);
        return new WorkspacePaths(fallbackRoot, fallbackMetadata);
    }

    private static WorkspacePaths ResolveWorkspacePathsFromRoot(string inputPath)
    {
        var rootPath = Path.GetFullPath(inputPath);
        if (string.Equals(
                Path.GetFileName(rootPath),
                MetadataDirectoryName,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            var parent = Directory.GetParent(rootPath)?.FullName ?? rootPath;
            return new WorkspacePaths(parent, rootPath);
        }

        var metadataRootPath = ResolveMetadataRoot(rootPath);
        return new WorkspacePaths(rootPath, metadataRootPath);
    }

    private static bool IsMetadataDirectoryCandidate(string metadataRootPath)
    {
        return File.Exists(Path.Combine(metadataRootPath, ModelFileName)) ||
               File.Exists(Path.Combine(metadataRootPath, InstanceFileName)) ||
               Directory.Exists(Path.Combine(metadataRootPath, InstanceDirectoryName));
    }

    private static string ResolveMetadataRoot(string workspaceRootPath)
    {
        var hasDirectCanonical = File.Exists(Path.Combine(workspaceRootPath, ModelFileName)) &&
                                 File.Exists(Path.Combine(workspaceRootPath, InstanceFileName));
        if (hasDirectCanonical)
        {
            return workspaceRootPath;
        }

        var metadataRootPath = Path.Combine(workspaceRootPath, MetadataDirectoryName);
        return Directory.Exists(metadataRootPath) ? metadataRootPath : workspaceRootPath;
    }

    private static WorkspaceManifest ReadManifest(string workspaceRootPath, string metadataRootPath)
    {
        var workspaceXmlPath = Path.Combine(metadataRootPath, WorkspaceXmlFileName);
        if (File.Exists(workspaceXmlPath))
        {
            var snapshot = MetaWorkspaceModel.LoadFromWorkspaceXmlFile(workspaceXmlPath);
            var manifestFromXml = MetaWorkspaceManifestAdapter.ToWorkspaceManifest(snapshot, workspaceXmlPath);
            var normalizedFromXml = NormalizeManifest(manifestFromXml);
            ValidateContractVersion(normalizedFromXml, workspaceXmlPath);
            return normalizedFromXml;
        }

        var workspaceJsonPath = Path.Combine(metadataRootPath, "workspace.json");
        if (File.Exists(workspaceJsonPath))
        {
            throw new InvalidDataException(
                $"Workspace manifest '{workspaceXmlPath}' is required. Legacy '{workspaceJsonPath}' is not supported.");
        }

        return WorkspaceManifest.CreateDefault();
    }

    private static WorkspaceManifest NormalizeManifest(WorkspaceManifest? manifest)
    {
        var normalized = manifest ?? WorkspaceManifest.CreateDefault();
        normalized.ContractVersion = string.IsNullOrWhiteSpace(normalized.ContractVersion)
            ? "1.0"
            : normalized.ContractVersion.Trim();
        normalized.ModelFile = NormalizeRelativePath(normalized.ModelFile, "metadata/model.xml");
        normalized.InstanceDir = NormalizeRelativePath(normalized.InstanceDir, "metadata/instance");
        normalized.Encoding = string.IsNullOrWhiteSpace(normalized.Encoding)
            ? "utf-8-no-bom"
            : normalized.Encoding.Trim().ToLowerInvariant();
        normalized.Newlines = string.IsNullOrWhiteSpace(normalized.Newlines)
            ? "lf"
            : normalized.Newlines.Trim().ToLowerInvariant();
        normalized.CanonicalSort ??= new CanonicalSortManifest();
        normalized.EntityStorages ??= new List<EntityStorageManifest>();
        normalized.EntityStorages = normalized.EntityStorages
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.EntityName))
            .Select(item => new EntityStorageManifest
            {
                EntityName = item.EntityName.Trim(),
                StorageKind = string.IsNullOrWhiteSpace(item.StorageKind) ? "Sharded" : item.StorageKind.Trim(),
                DirectoryPath = NormalizeRelativePath(item.DirectoryPath, string.Empty),
                FilePath = NormalizeRelativePath(item.FilePath, string.Empty),
                Pattern = string.IsNullOrWhiteSpace(item.Pattern) ? string.Empty : item.Pattern.Trim(),
            })
            .OrderBy(item => item.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized;
    }

    private static string NormalizeRelativePath(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().Replace('\\', '/');
        normalized = normalized.TrimStart('/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static void ValidateContractVersion(WorkspaceManifest manifest, string workspaceFilePath)
    {
        if (!TryParseContractVersion(manifest.ContractVersion, out var major, out _))
        {
            throw new InvalidDataException(
                $"Workspace manifest '{workspaceFilePath}' has invalid contractVersion '{manifest.ContractVersion}'.");
        }

        if (major != SupportedContractMajorVersion)
        {
            throw new InvalidDataException(
                $"Unsupported contract major version '{major}' in '{workspaceFilePath}'. Tool supports '{SupportedContractMajorVersion}.{SupportedContractMinorVersion}'.");
        }
    }

    private static bool TryParseContractVersion(string? value, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major))
        {
            return false;
        }

        if (parts.Length == 2 &&
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor))
        {
            return false;
        }

        return major >= 0 && minor >= 0;
    }

    private static void WriteManifestToFile(WorkspaceManifest manifest, string workspaceFilePath)
    {
        var document = MetaWorkspaceModel.BuildDocument(MetaWorkspaceManifestAdapter.ToMetaWorkspaceData(manifest));
        WriteXmlToFile(document, workspaceFilePath, indented: true);
    }

    private static string ResolveModelPath(string workspaceRootPath, string metadataRootPath, WorkspaceManifest manifest)
    {
        var manifestModelPath = ResolvePathFromWorkspaceRoot(workspaceRootPath, manifest.ModelFile);
        var candidatePaths = new[]
        {
            manifestModelPath,
            Path.Combine(metadataRootPath, ModelFileName),
            Path.Combine(workspaceRootPath, ModelFileName),
        };

        return FirstExistingPath(candidatePaths);
    }

    private static InstanceStore ReadInstance(
        string workspaceRootPath,
        string metadataRootPath,
        WorkspaceManifest manifest,
        ModelDefinition model)
    {
        var shardDirectoryPath = ResolvePathFromWorkspaceRoot(workspaceRootPath, manifest.InstanceDir);
        var hasShardDirectory = Directory.Exists(shardDirectoryPath);
        if (Directory.Exists(shardDirectoryPath))
        {
            var shardFiles = Directory.GetFiles(shardDirectoryPath, "*.xml")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (shardFiles.Count > 0)
            {
                return ReadInstanceShards(shardFiles, model);
            }
        }

        var monolithicPath = FirstExistingPath(new[]
        {
            Path.Combine(metadataRootPath, InstanceFileName),
            Path.Combine(workspaceRootPath, InstanceFileName),
        });

        if (string.IsNullOrWhiteSpace(monolithicPath))
        {
            if (hasShardDirectory)
            {
                return new InstanceStore
                {
                    ModelName = model.Name ?? string.Empty,
                };
            }

            throw new FileNotFoundException(
                $"Could not find sharded instance files in '{shardDirectoryPath}' and could not find {InstanceFileName} in '{metadataRootPath}'.");
        }

        return ReadInstanceMonolithic(monolithicPath, model);
    }

    private static InstanceStore ReadInstanceMonolithic(
        string instancePath,
        ModelDefinition model)
    {
        var document = XDocument.Load(instancePath, LoadOptions.None);
        var instance = new InstanceStore
        {
            ModelName = model.Name ?? string.Empty,
        };
        MergeInstanceDocument(instance, document, model, sourceShardFileName: string.Empty);
        if (string.IsNullOrWhiteSpace(instance.ModelName))
        {
            instance.ModelName = model.Name ?? string.Empty;
        }

        return instance;
    }

    private static InstanceStore ReadInstanceShards(
        IReadOnlyCollection<string> shardFiles,
        ModelDefinition model)
    {
        var instance = new InstanceStore
        {
            ModelName = model.Name ?? string.Empty,
        };

        foreach (var shardPath in shardFiles)
        {
            var document = XDocument.Load(shardPath, LoadOptions.None);
            MergeInstanceDocument(
                instance,
                document,
                model,
                sourceShardFileName: Path.GetFileName(shardPath));
        }

        if (string.IsNullOrWhiteSpace(instance.ModelName))
        {
            instance.ModelName = model.Name ?? string.Empty;
        }

        return instance;
    }

    private static void MergeInstanceDocument(
        InstanceStore instance,
        XDocument document,
        ModelDefinition model,
        string sourceShardFileName)
    {
        var root = document.Root ?? throw new InvalidDataException("Instance XML has no root element.");
        if (string.IsNullOrWhiteSpace(instance.ModelName))
        {
            instance.ModelName = root.Name.LocalName;
        }

        var entityByContainer = BuildEntityByContainerLookup(model);

        foreach (var listElement in root.Elements())
        {
            var listName = listElement.Name.LocalName;
            if (!entityByContainer.TryGetValue(listName, out var modelEntity))
            {
                throw new InvalidDataException(
                    $"Instance XML list '{listName}' references unknown entity.");
            }

            var entityName = modelEntity.Name;

            var records = instance.GetOrCreateEntityRecords(entityName);
            foreach (var rowElement in listElement.Elements())
            {
                if (!string.Equals(rowElement.Name.LocalName, entityName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Instance XML list '{listName}' contains unexpected row element '{rowElement.Name.LocalName}'. Expected '{entityName}'.");
                }

                records.Add(ParseRecord(entityName, modelEntity, rowElement, sourceShardFileName));
            }
        }
    }

    private static InstanceRecord ParseRecord(
        string entityName,
        EntityDefinition modelEntity,
        XElement rowElement,
        string sourceShardFileName)
    {
        var id = (string?)rowElement.Attribute("Id");
        if (!IsPositiveIntegerIdentity(id))
        {
            throw new InvalidDataException(
                $"Entity '{entityName}' row is missing valid numeric Id.");
        }

        var record = new InstanceRecord
        {
            Id = id!,
            SourceShardFileName = NormalizeShardFileName(sourceShardFileName, entityName),
        };

        var propertyByName = modelEntity.Properties
            .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        var relationshipByColumn = modelEntity.Relationships
            .ToDictionary(relationship => relationship.GetColumnName(), StringComparer.OrdinalIgnoreCase);
        var relationshipByUsage = modelEntity.Relationships
            .ToDictionary(relationship => relationship.GetUsageName(), StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in rowElement.Attributes())
        {
            var attributeName = attribute.Name.LocalName;
            if (string.Equals(attributeName, "Id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (relationshipByColumn.TryGetValue(attributeName, out var relationship))
            {
                if (!IsPositiveIntegerIdentity(attribute.Value))
                {
                    throw new InvalidDataException(
                        $"Entity '{entityName}' row '{record.Id}' has invalid relationship '{relationship.GetColumnName()}' value '{attribute.Value}'.");
                }

                record.RelationshipIds[relationship.GetUsageName()] = attribute.Value;
                continue;
            }

            throw new InvalidDataException(
                $"Entity '{entityName}' row '{record.Id}' has unsupported attribute '{attributeName}'.");
        }

        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in rowElement.Elements())
        {
            var elementName = element.Name.LocalName;
            if (relationshipByUsage.TryGetValue(elementName, out var relationship))
            {
                throw new InvalidDataException(
                    $"Entity '{entityName}' row '{record.Id}' has relationship element '{relationship.GetUsageName()}'. Relationships must be attributes.");
            }

            if (!propertyByName.TryGetValue(elementName, out var property))
            {
                throw new InvalidDataException(
                    $"Entity '{entityName}' row '{record.Id}' has unknown property element '{elementName}'.");
            }

            if (!seenProperties.Add(property.Name))
            {
                throw new InvalidDataException(
                    $"Entity '{entityName}' row '{record.Id}' has duplicate property element '{property.Name}'.");
            }

            record.Values[property.Name] = element.Value;
        }

        foreach (var relationship in modelEntity.Relationships)
        {
            var relationshipUsage = relationship.GetUsageName();
            if (!record.RelationshipIds.TryGetValue(relationshipUsage, out var relationshipId) ||
                string.IsNullOrWhiteSpace(relationshipId))
            {
                throw new InvalidDataException(
                    $"Entity '{entityName}' row '{record.Id}' is missing required relationship '{relationship.GetColumnName()}'.");
            }
        }

        return record;
    }

    private static void WriteInstanceShards(Workspace workspace, string instanceDirectoryPath)
    {
        Directory.CreateDirectory(instanceDirectoryPath);

        var modelName = !string.IsNullOrWhiteSpace(workspace.Model.Name)
            ? workspace.Model.Name
            : workspace.Instance.ModelName;
        var rootName = string.IsNullOrWhiteSpace(modelName) ? "MetadataModel" : modelName;
        var shardPlans = BuildInstanceShardWritePlans(workspace, persistAssignments: true);

        var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shardPlan in shardPlans)
        {
            var shardPath = Path.Combine(instanceDirectoryPath, shardPlan.ShardFileName);
            var shardDocument = BuildInstanceShardDocument(workspace, rootName, shardPlan.EntityName, shardPlan.Records);
            WriteXmlToFile(shardDocument, shardPath, indented: true);
            expectedPaths.Add(Path.GetFullPath(shardPath));
        }

        foreach (var existingPath in Directory.GetFiles(instanceDirectoryPath, "*.xml"))
        {
            var absolutePath = Path.GetFullPath(existingPath);
            if (!expectedPaths.Contains(absolutePath))
            {
                File.Delete(existingPath);
            }
        }
    }

    private static XDocument BuildInstanceShardDocument(
        Workspace workspace,
        string rootName,
        string entityName,
        IReadOnlyCollection<InstanceRecord>? recordsOverride = null)
    {
        var modelEntity = workspace.Model.Entities.FirstOrDefault(entity =>
            string.Equals(entity.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (modelEntity == null)
        {
            throw new InvalidOperationException($"Cannot write instance shard for unknown entity '{entityName}'.");
        }

        var propertyByName = modelEntity.Properties
            .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        var orderedPropertyNames = modelEntity.Properties
            .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orderedRelationships = modelEntity.Relationships
            .OrderBy(relationship => relationship.GetColumnName(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => relationship.GetUsageName(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var root = new XElement(rootName);
        var listElement = new XElement(modelEntity.GetPluralName());
        root.Add(listElement);

        var records = recordsOverride ??
                      (workspace.Instance.RecordsByEntity.TryGetValue(entityName, out var entityRecords)
                          ? entityRecords
                          : new List<InstanceRecord>());

        foreach (var record in records.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsPositiveIntegerIdentity(record.Id))
            {
                throw new InvalidOperationException(
                    $"Cannot write entity '{entityName}' row with invalid Id '{record.Id}'.");
            }

            var recordElement = new XElement(entityName, new XAttribute("Id", record.Id));

            foreach (var unknownPropertyName in record.Values.Keys
                         .Where(key => !propertyByName.ContainsKey(key))
                         .OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Entity '{entityName}' row '{record.Id}' contains unknown property '{unknownPropertyName}'.");
            }

            foreach (var relationship in orderedRelationships)
            {
                var relationshipName = relationship.GetUsageName();
                var relationshipColumn = relationship.GetColumnName();
                if (!record.RelationshipIds.TryGetValue(relationshipName, out var relationshipId) ||
                    string.IsNullOrWhiteSpace(relationshipId))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{record.Id}' is missing required relationship '{relationshipColumn}'.");
                }

                if (!IsPositiveIntegerIdentity(relationshipId))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{record.Id}' has invalid relationship '{relationshipColumn}' value '{relationshipId}'.");
                }

                recordElement.Add(new XAttribute(relationshipColumn, relationshipId));
            }

            var knownRelationshipNames = orderedRelationships
                .Select(relationship => relationship.GetUsageName())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var unknownRelationshipName in record.RelationshipIds.Keys
                         .Where(key => !knownRelationshipNames.Contains(key))
                         .OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Entity '{entityName}' row '{record.Id}' contains unknown relationship '{unknownRelationshipName}'.");
            }

            foreach (var propertyName in orderedPropertyNames)
            {
                if (!record.Values.TryGetValue(propertyName, out var value) || value == null)
                {
                    continue;
                }

                recordElement.Add(new XElement(propertyName, value));
            }

            listElement.Add(recordElement);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static string BuildShardCanonicalPayload(Workspace workspace)
    {
        var modelName = !string.IsNullOrWhiteSpace(workspace.Model.Name)
            ? workspace.Model.Name
            : workspace.Instance.ModelName;
        var rootName = string.IsNullOrWhiteSpace(modelName) ? "MetadataModel" : modelName;
        var parts = new List<string>();
        foreach (var shardPlan in BuildInstanceShardWritePlans(workspace, persistAssignments: false))
        {
            var shardDocument = BuildInstanceShardDocument(
                workspace,
                rootName,
                shardPlan.EntityName,
                shardPlan.Records);
            var shardCanonical = SerializeXml(shardDocument, indented: false);
            parts.Add(shardPlan.ShardFileName + "\n" + shardCanonical);
        }

        return string.Join("\n---\n", parts);
    }

    private static IReadOnlyList<InstanceShardWritePlan> BuildInstanceShardWritePlans(
        Workspace workspace,
        bool persistAssignments)
    {
        var plans = new List<InstanceShardWritePlan>();
        foreach (var entityName in GetOrderedEntityNames(workspace))
        {
            plans.AddRange(BuildEntityShardWritePlans(workspace, entityName));
        }

        plans = plans
            .OrderBy(plan => plan.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.ShardFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            plan.ShardFileName = MakeUniqueShardFileName(plan.EntityName, plan.ShardFileName, usedFileNames);
            if (persistAssignments)
            {
                foreach (var record in plan.Records)
                {
                    record.SourceShardFileName = plan.ShardFileName;
                }
            }
        }

        return plans;
    }

    private static IReadOnlyList<InstanceShardWritePlan> BuildEntityShardWritePlans(Workspace workspace, string entityName)
    {
        var records = workspace.Instance.RecordsByEntity.TryGetValue(entityName, out var entityRecords)
            ? entityRecords
            : new List<InstanceRecord>();
        var orderedRecords = records
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaultShardFileName = NormalizeShardFileName(null, entityName);
        var assignedNames = orderedRecords
            .Select(record => NormalizeLoadedShardFileName(record.SourceShardFileName, entityName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (assignedNames.Count == 0)
        {
            assignedNames.Add(defaultShardFileName);
        }

        var primaryShardFileName = assignedNames[0];
        var recordsByShard = new Dictionary<string, List<InstanceRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in orderedRecords)
        {
            var shardFileName = NormalizeLoadedShardFileName(record.SourceShardFileName, entityName);
            if (string.IsNullOrWhiteSpace(shardFileName))
            {
                shardFileName = primaryShardFileName;
            }

            if (!recordsByShard.TryGetValue(shardFileName, out var shardRecords))
            {
                shardRecords = new List<InstanceRecord>();
                recordsByShard[shardFileName] = shardRecords;
            }

            record.SourceShardFileName = shardFileName;
            shardRecords.Add(record);
        }

        if (recordsByShard.Count == 0)
        {
            recordsByShard[defaultShardFileName] = new List<InstanceRecord>();
        }

        return recordsByShard
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new InstanceShardWritePlan(entityName, item.Key, item.Value))
            .ToList();
    }

    private static string NormalizeLoadedShardFileName(string? shardFileName, string entityName)
    {
        var trimmed = (shardFileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return NormalizeShardFileName(trimmed, entityName);
    }

    private static string NormalizeShardFileName(string? shardFileName, string entityName)
    {
        var trimmed = (shardFileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return entityName + ".xml";
        }

        var leafName = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(leafName))
        {
            return entityName + ".xml";
        }

        if (!leafName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return leafName + ".xml";
        }

        return leafName;
    }

    private static string MakeUniqueShardFileName(
        string entityName,
        string candidate,
        ISet<string> usedFileNames)
    {
        var normalized = NormalizeShardFileName(candidate, entityName);
        if (usedFileNames.Add(normalized))
        {
            return normalized;
        }

        var baseName = Path.GetFileNameWithoutExtension(normalized);
        var extension = Path.GetExtension(normalized);
        var disambiguatedBase = entityName + "." + baseName;
        var disambiguated = disambiguatedBase + extension;
        var suffix = 2;
        while (!usedFileNames.Add(disambiguated))
        {
            disambiguated = disambiguatedBase + "." + suffix.ToString(CultureInfo.InvariantCulture) + extension;
            suffix++;
        }

        return disambiguated;
    }

    private sealed class InstanceShardWritePlan
    {
        public InstanceShardWritePlan(string entityName, string shardFileName, List<InstanceRecord> records)
        {
            EntityName = entityName;
            ShardFileName = shardFileName;
            Records = records;
        }

        public string EntityName { get; }
        public string ShardFileName { get; set; }
        public List<InstanceRecord> Records { get; }
    }

    private static IReadOnlyList<string> GetOrderedEntityNames(Workspace workspace)
    {
        var entityNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in workspace.Model.Entities)
        {
            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                entityNames.Add(entity.Name);
            }
        }

        foreach (var entityName in workspace.Instance.RecordsByEntity.Keys)
        {
            if (!string.IsNullOrWhiteSpace(entityName))
            {
                entityNames.Add(entityName);
            }
        }

        return entityNames.ToList();
    }

    private static bool IsPositiveIntegerIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length == 0 || text[0] == '-')
        {
            return false;
        }

        var hasNonZeroDigit = false;
        foreach (var ch in text)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }

            if (ch != '0')
            {
                hasNonZeroDigit = true;
            }
        }

        return hasNonZeroDigit;
    }

    private static void EnsurePathUnderMetadataRoot(string path, string metadataRootPath, string manifestFieldName)
    {
        if (!IsPathWithinRoot(path, metadataRootPath))
        {
            throw new InvalidDataException(
                $"Workspace manifest '{manifestFieldName}' must resolve under '{MetadataDirectoryName}/'. Resolved path '{path}' is outside '{metadataRootPath}'.");
        }
    }

    private static string MapPathToStagingMetadataRoot(
        string metadataRootPath,
        string stagingMetadataRootPath,
        string resolvedFinalPath)
    {
        var relative = Path.GetRelativePath(metadataRootPath, resolvedFinalPath);
        return Path.GetFullPath(Path.Combine(stagingMetadataRootPath, relative));
    }

    private static void SwapMetadataDirectories(
        string metadataRootPath,
        string stagingMetadataRootPath,
        string backupMetadataRootPath)
    {
        var hadExistingMetadata = Directory.Exists(metadataRootPath);
        if (hadExistingMetadata)
        {
            Directory.Move(metadataRootPath, backupMetadataRootPath);
        }

        try
        {
            Directory.Move(stagingMetadataRootPath, metadataRootPath);
            DeleteDirectoryIfExists(backupMetadataRootPath);
        }
        catch
        {
            if (!Directory.Exists(metadataRootPath) && Directory.Exists(backupMetadataRootPath))
            {
                Directory.Move(backupMetadataRootPath, metadataRootPath);
            }

            throw;
        }
    }

    private static string SerializeManifest(WorkspaceManifest manifest)
    {
        return SerializeXml(
            MetaWorkspaceModel.BuildDocument(MetaWorkspaceManifestAdapter.ToMetaWorkspaceData(manifest)),
            indented: false);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string ResolvePathFromWorkspaceRoot(string workspaceRootPath, string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException(
                $"Workspace manifest path '{path}' must be relative to the workspace root.");
        }

        var resolvedPath = Path.GetFullPath(Path.Combine(workspaceRootPath, normalized));
        var workspaceRoot = Path.GetFullPath(workspaceRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsPathWithinRoot(resolvedPath, workspaceRoot))
        {
            throw new InvalidDataException(
                $"Workspace manifest path '{path}' resolves outside workspace root '{workspaceRoot}'.");
        }

        return resolvedPath;
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(path, root, comparison))
        {
            return true;
        }

        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, comparison);
    }

    private static string FirstExistingPath(IEnumerable<string> candidatePaths)
    {
        return candidatePaths.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static ModelDefinition ReadModel(string modelPath)
    {
        var document = XDocument.Load(modelPath, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("Model XML has no root element.");
        if (!string.Equals(root.Name.LocalName, "Model", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Model XML root must be <Model>.");
        }

        var model = new ModelDefinition
        {
            Name = (string?)root.Attribute("name") ?? string.Empty,
        };

        var entitiesElement = root.Element("Entities");
        if (entitiesElement == null)
        {
            return model;
        }

        foreach (var entityElement in entitiesElement.Elements("Entity"))
        {
            var displayKeyAttribute = entityElement.Attribute("displayKey");
            if (displayKeyAttribute != null)
            {
                throw new InvalidDataException(
                    $"Model entity '{(string?)entityElement.Attribute("name") ?? string.Empty}' uses unsupported attribute 'displayKey'.");
            }

            var entity = new EntityDefinition
            {
                Name = (string?)entityElement.Attribute("name") ?? string.Empty,
                Plural = ((string?)entityElement.Attribute("plural") ?? string.Empty).Trim(),
            };

            var propertiesElement = entityElement.Element("Properties");
            if (propertiesElement != null)
            {
                foreach (var propertyElement in propertiesElement.Elements("Property"))
                {
                    var propertyName = ((string?)propertyElement.Attribute("name") ?? string.Empty).Trim();
                    if (string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Entity '{entity.Name}' must not define explicit property 'Id'. It is implicit.");
                    }

                    if (propertyElement.Attribute("isNullable") != null)
                    {
                        throw new InvalidDataException(
                            $"Property '{entity.Name}.{propertyName}' uses unsupported attribute 'isNullable'. Use 'isRequired'.");
                    }

                    var property = new PropertyDefinition
                    {
                        Name = propertyName,
                        DataType = ParseDataType((string?)propertyElement.Attribute("dataType")),
                        IsNullable = !ParseRequired((string?)propertyElement.Attribute("isRequired")),
                    };
                    entity.Properties.Add(property);
                }
            }

            var relationshipsElement = entityElement.Element("Relationships");
            if (relationshipsElement != null)
            {
                foreach (var relationshipElement in relationshipsElement.Elements("Relationship"))
                {
                    entity.Relationships.Add(new RelationshipDefinition
                    {
                        Entity = ((string?)relationshipElement.Attribute("entity") ?? string.Empty).Trim(),
                        Name = ((string?)relationshipElement.Attribute("name") ?? string.Empty).Trim(),
                        Column = ((string?)relationshipElement.Attribute("column") ?? string.Empty).Trim(),
                    });
                }
            }

            model.Entities.Add(entity);
        }

        return model;
    }

    private static string ParseDataType(string? dataTypeValue)
    {
        if (string.IsNullOrWhiteSpace(dataTypeValue))
        {
            return "string";
        }

        var trimmed = dataTypeValue.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "string" : trimmed;
    }

    private static bool ParseRequired(string? isRequiredValue)
    {
        if (string.IsNullOrWhiteSpace(isRequiredValue))
        {
            return true;
        }

        if (bool.TryParse(isRequiredValue, out var parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"Invalid boolean value '{isRequiredValue}' for attribute 'isRequired'.");
    }

    private static XDocument BuildModelDocument(ModelDefinition model)
    {
        var root = new XElement("Model", new XAttribute("name", model.Name ?? string.Empty));
        var entitiesElement = new XElement("Entities");
        root.Add(entitiesElement);

        foreach (var entity in model.Entities.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entityElement = new XElement("Entity", new XAttribute("name", entity.Name ?? string.Empty));
            var defaultPlural = (entity.Name ?? string.Empty) + "s";
            if (!string.IsNullOrWhiteSpace(entity.Plural) &&
                !string.Equals(entity.Plural, defaultPlural, StringComparison.Ordinal))
            {
                entityElement.Add(new XAttribute("plural", entity.Plural));
            }

            var nonIdProperties = OrderProperties(entity.Properties)
                .Where(item => !string.Equals(item.Name, "Id", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (nonIdProperties.Count > 0)
            {
                var propertiesElement = new XElement("Properties");
                foreach (var property in nonIdProperties)
                {
                    var propertyElement = new XElement("Property",
                        new XAttribute("name", property.Name ?? string.Empty));
                    var dataType = string.IsNullOrWhiteSpace(property.DataType) ? "string" : property.DataType;
                    if (!string.Equals(dataType, "string", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyElement.Add(new XAttribute("dataType", dataType));
                    }

                    if (property.IsNullable)
                    {
                        propertyElement.Add(new XAttribute("isRequired", "false"));
                    }

                    propertiesElement.Add(propertyElement);
                }

                entityElement.Add(propertiesElement);
            }

            if (entity.Relationships.Count > 0)
            {
                var relationshipsElement = new XElement("Relationships");
                foreach (var relationship in entity.Relationships
                             .OrderBy(item => item.GetUsageName(), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(item => item.Entity, StringComparer.OrdinalIgnoreCase))
                {
                    var relationshipElement = new XElement("Relationship",
                        new XAttribute("entity", relationship.Entity ?? string.Empty));
                    var usageName = relationship.GetUsageName();
                    if (!string.Equals(usageName, relationship.Entity, StringComparison.Ordinal))
                    {
                        relationshipElement.Add(new XAttribute("name", usageName));
                    }

                    var columnName = relationship.GetColumnName();
                    var defaultColumnName = usageName + "Id";
                    if (!string.Equals(columnName, defaultColumnName, StringComparison.Ordinal))
                    {
                        relationshipElement.Add(new XAttribute("column", columnName));
                    }

                    relationshipsElement.Add(relationshipElement);
                }

                entityElement.Add(relationshipsElement);
            }

            entitiesElement.Add(entityElement);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static IEnumerable<PropertyDefinition> OrderProperties(IReadOnlyCollection<PropertyDefinition> properties)
    {
        return properties
            .OrderBy(item => string.Equals(item.Name, "Id", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, EntityDefinition> BuildEntityByContainerLookup(ModelDefinition model)
    {
        var lookup = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in model.Entities.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var containerName = entity.GetPluralName();
            if (!lookup.TryAdd(containerName, entity))
            {
                throw new InvalidDataException(
                    $"Model has duplicate instance container name '{containerName}' for multiple entities.");
            }
        }

        return lookup;
    }

    private static void WriteXmlToFile(XDocument document, string path, bool indented)
    {
        var xml = SerializeXml(document, indented);
        WriteTextAtomic(path, xml);
    }

    private static string SerializeXml(XDocument document, bool indented)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            Indent = indented,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };

        using var stringWriter = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);
        document.Save(xmlWriter);
        xmlWriter.Flush();
        return stringWriter.ToString();
    }

    private static void WriteTextAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, content, Utf8NoBom);

        try
        {
            if (File.Exists(path))
            {
                var backupPath = path + ".bak";
                try
                {
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                    DeleteIfExists(backupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(path);
                    File.Move(tempPath, path);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            DeleteIfExists(tempPath);
        }
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public Utf8StringWriter()
            : base(CultureInfo.InvariantCulture)
        {
        }

        public override Encoding Encoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private sealed class WorkspacePaths
    {
        public WorkspacePaths(string workspaceRootPath, string metadataRootPath)
        {
            WorkspaceRootPath = workspaceRootPath;
            MetadataRootPath = metadataRootPath;
        }

        public string WorkspaceRootPath { get; }
        public string MetadataRootPath { get; }
    }
}
