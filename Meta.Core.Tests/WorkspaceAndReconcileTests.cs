using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Meta.Adapters;
using Meta.Core.Domain;

namespace Meta.Core.Tests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task WorkspaceHash_IsStable_AfterRoundTripSaveLoad()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var original = await services.WorkspaceService.LoadAsync(samplesPath);
        var originalHash = services.WorkspaceService.CalculateHash(original);

        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(original, tempRoot);
            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot);
            var reloadedHash = services.WorkspaceService.CalculateHash(reloaded);

            Assert.Equal(originalHash, reloadedHash);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_WritesWorkspaceManifestAndShardedInstances()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var expectedRows = workspace.Instance.RecordsByEntity.Values.Sum(records => records.Count);

        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);

            var metadataRoot = Path.Combine(tempRoot, "metadata");
            var manifestPath = Path.Combine(metadataRoot, "workspace.xml");
            var modelPath = Path.Combine(metadataRoot, "model.xml");
            var instanceDir = Path.Combine(metadataRoot, "instance");
            var legacyInstancePath = Path.Combine(metadataRoot, "instance.xml");

            Assert.True(File.Exists(manifestPath), "workspace.xml should exist after save.");
            Assert.True(File.Exists(modelPath), "model.xml should exist after save.");
            Assert.True(Directory.Exists(instanceDir), "instance shard directory should exist after save.");
            Assert.True(Directory.GetFiles(instanceDir, "*.xml").Length > 0, "instance shard directory should contain XML files.");
            Assert.False(File.Exists(legacyInstancePath), "legacy monolithic instance.xml should not be emitted.");

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot);
            var reloadedRows = reloaded.Instance.RecordsByEntity.Values.Sum(records => records.Count);
            Assert.Equal(expectedRows, reloadedRows);
            Assert.Equal("1.0", reloaded.Manifest.ContractVersion);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_LegacyWorkspaceJson_DoesNotAutoMigrateToWorkspaceXml()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            var metadataRoot = Path.Combine(tempRoot, "metadata");
            var workspaceXmlPath = Path.Combine(metadataRoot, "workspace.xml");
            var workspaceJsonPath = Path.Combine(metadataRoot, "workspace.json");

            Assert.True(File.Exists(workspaceXmlPath));
            var legacyJson = JsonSerializer.Serialize(new WorkspaceManifest
            {
                ContractVersion = "1.0",
                ModelFile = "metadata/model.xml",
                InstanceDir = "metadata/instance",
                Encoding = "utf-8-no-bom",
                Newlines = "lf",
                CanonicalSort = new CanonicalSortManifest
                {
                    Entities = "name-ordinal",
                    Properties = "name-ordinal",
                    Relationships = "name-ordinal",
                    Rows = "id-ordinal",
                    Attributes = "id-first-then-name-ordinal",
                },
            });

            File.Delete(workspaceXmlPath);
            await File.WriteAllTextAsync(workspaceJsonPath, legacyJson);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false));
            Assert.Contains("workspace.xml", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("workspace.json", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(workspaceXmlPath), "workspace.xml should not be auto-created from legacy workspace.json.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_AndSave_PreservesSplitEntityShardLayout()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            SplitEntityShard(tempRoot, "Cube", "Cube.part-a.xml", "Cube.part-b.xml");

            var splitLoaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var cubeRows = splitLoaded.Instance.GetOrCreateEntityRecords("Cube");
            Assert.NotEmpty(cubeRows);
            Assert.All(
                cubeRows,
                row => Assert.Contains(
                    row.SourceShardFileName,
                    new[] { "Cube.part-a.xml", "Cube.part-b.xml" }));

            await services.WorkspaceService.SaveAsync(splitLoaded);

            var instanceDir = Path.Combine(tempRoot, "metadata", "instance");
            Assert.True(File.Exists(Path.Combine(instanceDir, "Cube.part-a.xml")));
            Assert.True(File.Exists(Path.Combine(instanceDir, "Cube.part-b.xml")));
            Assert.False(File.Exists(Path.Combine(instanceDir, "Cube.xml")));

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            Assert.Equal(
                splitLoaded.Instance.GetOrCreateEntityRecords("Cube").Count,
                reloaded.Instance.GetOrCreateEntityRecords("Cube").Count);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_NewRowsGoToPrimarySplitShardForEntity()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            SplitEntityShard(tempRoot, "Cube", "Cube.part-a.xml", "Cube.part-b.xml");

            var splitLoaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            splitLoaded.Instance.GetOrCreateEntityRecords("Cube").Add(new InstanceRecord
            {
                Id = "999",
                SourceShardFileName = string.Empty,
                Values =
                {
                    ["CubeName"] = "Split Layout Insert",
                    ["Purpose"] = "Test row",
                    ["RefreshMode"] = "Manual",
                },
            });

            await services.WorkspaceService.SaveAsync(splitLoaded);

            var primaryShard = XDocument.Load(Path.Combine(tempRoot, "metadata", "instance", "Cube.part-a.xml"));
            var secondaryShard = XDocument.Load(Path.Combine(tempRoot, "metadata", "instance", "Cube.part-b.xml"));

            Assert.NotNull(primaryShard
                .Descendants("Cube")
                .SingleOrDefault(element => string.Equals((string?)element.Attribute("Id"), "999", StringComparison.OrdinalIgnoreCase)));
            Assert.Null(secondaryShard
                .Descendants("Cube")
                .SingleOrDefault(element => string.Equals((string?)element.Attribute("Id"), "999", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_DiscoversWorkspaceRoot_FromNestedDirectory()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            var nestedPath = Path.Combine(tempRoot, "a", "b", "c");
            Directory.CreateDirectory(nestedPath);

            var discovered = await services.WorkspaceService.LoadAsync(nestedPath);
            Assert.Equal(Path.GetFullPath(tempRoot), Path.GetFullPath(discovered.WorkspaceRootPath));
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "metadata"), Path.GetFullPath(discovered.MetadataRootPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_Fails_ForUnsupportedContractMajorVersion()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            var manifestPath = Path.Combine(tempRoot, "metadata", "workspace.xml");
            var manifest = XDocument.Load(manifestPath);
            manifest
                .Descendants("FormatVersion")
                .Single()
                .Value = "2.0";
            manifest.Save(manifestPath);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await services.WorkspaceService.LoadAsync(tempRoot));
            Assert.Contains("Unsupported contract major version", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_AllowsNewerMinorContractVersion()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            var manifestPath = Path.Combine(tempRoot, "metadata", "workspace.xml");
            var manifest = XDocument.Load(manifestPath);
            manifest
                .Descendants("FormatVersion")
                .Single()
                .Value = "1.7";
            manifest.Save(manifestPath);

            var loaded = await services.WorkspaceService.LoadAsync(tempRoot);
            Assert.Equal("1.7", loaded.Manifest.ContractVersion);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_RejectsInvalidWorkspace()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new Meta.Core.Domain.Workspace
            {
                WorkspaceRootPath = tempRoot,
                MetadataRootPath = Path.Combine(tempRoot, "metadata"),
                Model = new Meta.Core.Domain.ModelDefinition
                {
                    Name = "MetadataModel",
                },
                Instance = new Meta.Core.Domain.InstanceStore
                {
                    ModelName = "MetadataModel",
                },
            };

            var invalidEntity = new Meta.Core.Domain.EntityDefinition
            {
                Name = "Bad Name",
            };
            workspace.Model.Entities.Add(invalidEntity);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await services.WorkspaceService.SaveAsync(workspace));

            Assert.Contains("validation failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_RejectsManifestPathsOutsideWorkspaceRoot()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await services.ExportService.ExportXmlAsync(workspace, tempRoot);
            var manifestPath = Path.Combine(tempRoot, "metadata", "workspace.xml");
            var manifest = XDocument.Load(manifestPath);
            var workspaceLayout = manifest.Descendants("WorkspaceLayout").Single();
            workspaceLayout.Element("ModelFilePath")!.Value = "../outside-model.xml";
            manifest.Save(manifestPath);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await services.WorkspaceService.LoadAsync(tempRoot));
            Assert.Contains("outside workspace root", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_RejectsManifestPathsOutsideWorkspaceRoot()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");

        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            workspace.WorkspaceRootPath = tempRoot;
            workspace.Manifest.InstanceDir = "../outside-instance";

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await services.WorkspaceService.SaveAsync(workspace));
            Assert.Contains("outside workspace root", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_RejectsRelationshipCycles()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new Meta.Core.Domain.Workspace
            {
                WorkspaceRootPath = tempRoot,
                MetadataRootPath = Path.Combine(tempRoot, "metadata"),
                Model = new Meta.Core.Domain.ModelDefinition
                {
                    Name = "MetadataModel",
                },
                Instance = new Meta.Core.Domain.InstanceStore
                {
                    ModelName = "MetadataModel",
                },
            };

            var entityA = new Meta.Core.Domain.EntityDefinition
            {
                Name = "EntityA",
            };
            entityA.Relationships.Add(new Meta.Core.Domain.RelationshipDefinition
            {
                Entity = "EntityB",
            });

            var entityB = new Meta.Core.Domain.EntityDefinition
            {
                Name = "EntityB",
            };
            entityB.Relationships.Add(new Meta.Core.Domain.RelationshipDefinition
            {
                Entity = "EntityA",
            });

            workspace.Model.Entities.Add(entityA);
            workspace.Model.Entities.Add(entityB);

            workspace.Instance.GetOrCreateEntityRecords("EntityA").Add(new Meta.Core.Domain.InstanceRecord
            {
                Id = "1",
            });
            workspace.Instance.GetOrCreateEntityRecords("EntityA")[0].RelationshipIds["EntityBId"] = "1";

            workspace.Instance.GetOrCreateEntityRecords("EntityB").Add(new Meta.Core.Domain.InstanceRecord
            {
                Id = "1",
            });
            workspace.Instance.GetOrCreateEntityRecords("EntityB")[0].RelationshipIds["EntityAId"] = "1";

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await services.WorkspaceService.SaveAsync(workspace));

            Assert.Contains("relationship.cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_CleansUpAtomicStagingDirectories()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");
        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            workspace.WorkspaceRootPath = tempRoot;
            workspace.MetadataRootPath = Path.Combine(tempRoot, "metadata");

            await services.WorkspaceService.SaveAsync(workspace);
            await services.WorkspaceService.SaveAsync(workspace);

            var leftovers = Directory.Exists(tempRoot)
                ? Directory.GetDirectories(tempRoot, "metadata.__*")
                : Array.Empty<string>();
            Assert.Empty(leftovers);
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "metadata")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_RejectsWhenWorkspaceLockIsActive()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");
        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            workspace.WorkspaceRootPath = tempRoot;
            workspace.MetadataRootPath = Path.Combine(tempRoot, "metadata");
            Directory.CreateDirectory(tempRoot);

            var process = System.Diagnostics.Process.GetCurrentProcess();
            var lockContent = JsonSerializer.Serialize(new
            {
                pid = Environment.ProcessId,
                machineName = Environment.MachineName,
                toolVersion = "test",
                processStartTimeUtc = process.StartTime.ToUniversalTime(),
                acquiredUtc = DateTime.UtcNow,
            });
            File.WriteAllText(Path.Combine(tempRoot, ".meta.lock"), lockContent);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await services.WorkspaceService.SaveAsync(workspace));
            Assert.Contains("locked", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Save_RemovesStaleWorkspaceLockAndContinues()
    {
        var services = new ServiceCollection();
        var repositoryRoot = FindRepositoryRoot();
        var samplesPath = Path.Combine(repositoryRoot, "Samples");
        var workspace = await services.WorkspaceService.LoadAsync(samplesPath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            workspace.WorkspaceRootPath = tempRoot;
            workspace.MetadataRootPath = Path.Combine(tempRoot, "metadata");
            Directory.CreateDirectory(tempRoot);

            var staleLockContent = JsonSerializer.Serialize(new
            {
                pid = 999999,
                machineName = Environment.MachineName,
                toolVersion = "test",
                processStartTimeUtc = DateTime.UtcNow.AddDays(-1),
                acquiredUtc = DateTime.UtcNow.AddDays(-1),
            });
            var lockPath = Path.Combine(tempRoot, ".meta.lock");
            File.WriteAllText(lockPath, staleLockContent);

            await services.WorkspaceService.SaveAsync(workspace);

            Assert.False(File.Exists(lockPath), "Stale lock should be removed after successful save.");
            Assert.True(File.Exists(Path.Combine(tempRoot, "metadata", "workspace.xml")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_NullableMissingProperty_StaysMissing()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithOptionalProperty(tempRoot, includeOptionalProp: false, optionalPropValue: null);
            await services.WorkspaceService.SaveAsync(workspace);

            var itemShardPath = Path.Combine(tempRoot, "metadata", "instance", "Item.xml");
            var shardXml = await File.ReadAllTextAsync(itemShardPath);
            Assert.DoesNotContain("<OptionalProp", shardXml, StringComparison.Ordinal);

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var row = reloaded.Instance.GetOrCreateEntityRecords("Item").Single();
            Assert.False(row.Values.ContainsKey("OptionalProp"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_ExplicitEmptyStringProperty_StaysPresent()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithOptionalProperty(tempRoot, includeOptionalProp: true, optionalPropValue: string.Empty);
            await services.WorkspaceService.SaveAsync(workspace);

            var itemShardPath = Path.Combine(tempRoot, "metadata", "instance", "Item.xml");
            var shardDoc = XDocument.Load(itemShardPath);
            Assert.Contains(
                shardDoc.Descendants("OptionalProp"),
                element => string.Equals(element.Value, string.Empty, StringComparison.Ordinal));

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var row = reloaded.Instance.GetOrCreateEntityRecords("Item").Single();
            Assert.True(row.Values.ContainsKey("OptionalProp"));
            Assert.Equal(string.Empty, row.Values["OptionalProp"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_NullPropertyValue_IsNotSerializedAsEmptyString()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithOptionalProperty(tempRoot, includeOptionalProp: true, optionalPropValue: null);
            await services.WorkspaceService.SaveAsync(workspace);

            var itemShardPath = Path.Combine(tempRoot, "metadata", "instance", "Item.xml");
            var shardXml = await File.ReadAllTextAsync(itemShardPath);
            Assert.DoesNotContain("<OptionalProp", shardXml, StringComparison.Ordinal);

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var row = reloaded.Instance.GetOrCreateEntityRecords("Item").Single();
            Assert.False(row.Values.ContainsKey("OptionalProp"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_RelationshipSerialization_DoesNotWriteNullOrBlankPlaceholders()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithRelationship(tempRoot);
            await services.WorkspaceService.SaveAsync(workspace);

            var childShardPath = Path.Combine(tempRoot, "metadata", "instance", "Child.xml");
            var childShardXml = await File.ReadAllTextAsync(childShardPath);
            Assert.Contains("ParentId=\"1\"", childShardXml, StringComparison.Ordinal);
            Assert.DoesNotContain("Ghost", childShardXml, StringComparison.Ordinal);
            Assert.DoesNotContain("BlankRel", childShardXml, StringComparison.Ordinal);
            Assert.DoesNotContain("Id=\"\" />", childShardXml, StringComparison.Ordinal);

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var child = reloaded.Instance.GetOrCreateEntityRecords("Child").Single();
            Assert.Equal("1", child.RelationshipIds["ParentId"]);
            Assert.False(child.RelationshipIds.ContainsKey("Ghost"));
            Assert.False(child.RelationshipIds.ContainsKey("BlankRel"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_DoesNotLeaveNullValuesInMemory()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithRelationship(tempRoot);
            var item = new InstanceRecord
            {
                Id = "1",
            };
            item.Values["OptionalProp"] = null!;
            workspace.Instance.GetOrCreateEntityRecords("Item").Add(item);
            await services.WorkspaceService.SaveAsync(workspace);

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            foreach (var entityRows in reloaded.Instance.RecordsByEntity.Values)
            {
                foreach (var row in entityRows)
                {
                    foreach (var value in row.Values.Values)
                    {
                        Assert.NotNull(value);
                    }

                    foreach (var relationshipTarget in row.RelationshipIds.Values)
                    {
                        Assert.False(string.IsNullOrWhiteSpace(relationshipTarget));
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoadSave_NullableMissingProperty_RemainsMissingWithoutDrift()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithOptionalProperty(tempRoot, includeOptionalProp: false, optionalPropValue: null);
            await services.WorkspaceService.SaveAsync(workspace);

            var itemShardPath = Path.Combine(tempRoot, "metadata", "instance", "Item.xml");
            var firstXml = await File.ReadAllTextAsync(itemShardPath);
            Assert.DoesNotContain("<OptionalProp", firstXml, StringComparison.Ordinal);

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            await services.WorkspaceService.SaveAsync(reloaded);

            var secondXml = await File.ReadAllTextAsync(itemShardPath);
            Assert.Equal(firstXml, secondXml);
            Assert.DoesNotContain("<OptionalProp", secondXml, StringComparison.Ordinal);

            var loadedAgain = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var row = loadedAgain.Instance.GetOrCreateEntityRecords("Item").Single();
            Assert.False(row.Values.ContainsKey("OptionalProp"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveLoadSave_ExplicitEmptyStringProperty_RemainsExplicitWithoutDrift()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithOptionalProperty(tempRoot, includeOptionalProp: true, optionalPropValue: string.Empty);
            await services.WorkspaceService.SaveAsync(workspace);

            var itemShardPath = Path.Combine(tempRoot, "metadata", "instance", "Item.xml");
            var firstDoc = XDocument.Load(itemShardPath);
            Assert.Contains(
                firstDoc.Descendants("OptionalProp"),
                element => string.Equals(element.Value, string.Empty, StringComparison.Ordinal));

            var reloaded = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            await services.WorkspaceService.SaveAsync(reloaded);

            var secondDoc = XDocument.Load(itemShardPath);
            Assert.Equal(firstDoc.ToString(SaveOptions.DisableFormatting), secondDoc.ToString(SaveOptions.DisableFormatting));
            Assert.Contains(
                secondDoc.Descendants("OptionalProp"),
                element => string.Equals(element.Value, string.Empty, StringComparison.Ordinal));

            var loadedAgain = await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false);
            var row = loadedAgain.Instance.GetOrCreateEntityRecords("Item").Single();
            Assert.True(row.Values.ContainsKey("OptionalProp"));
            Assert.Equal(string.Empty, row.Values["OptionalProp"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_BlankRelationshipAttribute_FailsHard()
    {
        var services = new ServiceCollection();
        var tempRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = BuildWorkspaceWithRelationship(tempRoot);
            await services.WorkspaceService.SaveAsync(workspace);

            var childShardPath = Path.Combine(tempRoot, "metadata", "instance", "Child.xml");
            var childShard = XDocument.Load(childShardPath);
            var childRow = childShard
                .Descendants("Child")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "1", StringComparison.OrdinalIgnoreCase));
            childRow.SetAttributeValue("ParentId", string.Empty);
            childShard.Save(childShardPath);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await services.WorkspaceService.LoadAsync(tempRoot, searchUpward: false));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static Workspace BuildWorkspaceWithOptionalProperty(
        string workspaceRoot,
        bool includeOptionalProp,
        string? optionalPropValue)
    {
        var workspace = new Workspace
        {
            WorkspaceRootPath = workspaceRoot,
            MetadataRootPath = Path.Combine(workspaceRoot, "metadata"),
            Manifest = WorkspaceManifest.CreateDefault(),
            Model = new ModelDefinition
            {
                Name = "RoundTripModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "RoundTripModel",
            },
            IsDirty = true,
        };

        var item = new EntityDefinition
        {
            Name = "Item",
        };
        item.Properties.Add(new PropertyDefinition
        {
            Name = "OptionalProp",
            DataType = "string",
            IsNullable = true,
        });
        workspace.Model.Entities.Add(item);

        var row = new InstanceRecord
        {
            Id = "1",
        };
        if (includeOptionalProp)
        {
            row.Values["OptionalProp"] = optionalPropValue!;
        }

        workspace.Instance.GetOrCreateEntityRecords("Item").Add(row);
        return workspace;
    }

    private static void SplitEntityShard(
        string workspaceRoot,
        string entityName,
        string firstShardFileName,
        string secondShardFileName)
    {
        var shardPath = Path.Combine(workspaceRoot, "metadata", "instance", entityName + ".xml");
        var shard = XDocument.Load(shardPath);
        var root = shard.Root ?? throw new InvalidDataException("Entity shard has no root.");
        var listElement = root.Elements().Single();
        var rows = listElement.Elements().ToList();
        Assert.True(rows.Count >= 2, $"Expected at least two rows in '{entityName}' shard for split test.");

        var midpoint = rows.Count / 2;
        if (midpoint == 0)
        {
            midpoint = 1;
        }

        WriteEntityShard(
            workspaceRoot,
            firstShardFileName,
            root.Name.LocalName,
            listElement.Name.LocalName,
            rows.Take(midpoint));
        WriteEntityShard(
            workspaceRoot,
            secondShardFileName,
            root.Name.LocalName,
            listElement.Name.LocalName,
            rows.Skip(midpoint));
        File.Delete(shardPath);
    }

    private static void WriteEntityShard(
        string workspaceRoot,
        string shardFileName,
        string rootName,
        string listName,
        IEnumerable<XElement> rows)
    {
        var rowCopies = rows.Select(row => new XElement(row)).ToList();
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                rootName,
                new XElement(listName, rowCopies)));
        document.Save(Path.Combine(workspaceRoot, "metadata", "instance", shardFileName));
    }

    private static Workspace BuildWorkspaceWithRelationship(string workspaceRoot)
    {
        var workspace = new Workspace
        {
            WorkspaceRootPath = workspaceRoot,
            MetadataRootPath = Path.Combine(workspaceRoot, "metadata"),
            Manifest = WorkspaceManifest.CreateDefault(),
            Model = new ModelDefinition
            {
                Name = "RoundTripModel",
            },
            Instance = new InstanceStore
            {
                ModelName = "RoundTripModel",
            },
            IsDirty = true,
        };

        var item = new EntityDefinition
        {
            Name = "Item",
        };
        item.Properties.Add(new PropertyDefinition
        {
            Name = "OptionalProp",
            DataType = "string",
            IsNullable = true,
        });
        workspace.Model.Entities.Add(item);

        var parent = new EntityDefinition
        {
            Name = "Parent",
        };
        workspace.Model.Entities.Add(parent);

        var child = new EntityDefinition
        {
            Name = "Child",
        };
        child.Relationships.Add(new RelationshipDefinition
        {
            Entity = "Parent",
        });
        workspace.Model.Entities.Add(child);

        var parentRow = new InstanceRecord
        {
            Id = "1",
        };
        workspace.Instance.GetOrCreateEntityRecords("Parent").Add(parentRow);

        var childRow = new InstanceRecord
        {
            Id = "1",
        };
        childRow.RelationshipIds["ParentId"] = "1";
        workspace.Instance.GetOrCreateEntityRecords("Child").Add(childRow);

        return workspace;
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
