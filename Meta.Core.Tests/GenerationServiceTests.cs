using System;
using System.IO;
using Meta.Adapters;
using Meta.Core.Services;

namespace Meta.Core.Tests;

public sealed class GenerationServiceTests
{
    [Fact]
    public async Task GenerateSql_IsDeterministicAcrossRuns()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(FindRepositoryRoot(), "Samples"));
        var outputA = Path.Combine(Path.GetTempPath(), "metadata-gen-tests", Guid.NewGuid().ToString("N"), "a");
        var outputB = Path.Combine(Path.GetTempPath(), "metadata-gen-tests", Guid.NewGuid().ToString("N"), "b");

        try
        {
            var manifestA = GenerationService.GenerateSql(workspace, outputA);
            var manifestB = GenerationService.GenerateSql(workspace, outputB);

            Assert.True(GenerationService.AreEquivalent(manifestA, manifestB, out var message), message);
            Assert.True(File.Exists(Path.Combine(outputA, "schema.sql")));
            Assert.True(File.Exists(Path.Combine(outputA, "data.sql")));
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputA)!);
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputB)!);
        }
    }

    [Fact]
    public async Task GenerateSsdt_WritesExpectedFiles()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(FindRepositoryRoot(), "Samples"));
        var output = Path.Combine(Path.GetTempPath(), "metadata-gen-tests", Guid.NewGuid().ToString("N"), "ssdt");

        try
        {
            var manifest = GenerationService.GenerateSsdt(workspace, output);

            Assert.True(File.Exists(Path.Combine(output, "Schema.sql")));
            Assert.True(File.Exists(Path.Combine(output, "Data.sql")));
            Assert.True(File.Exists(Path.Combine(output, "PostDeploy.sql")));
            Assert.True(File.Exists(Path.Combine(output, "Metadata.sqlproj")));
            Assert.Equal(4, manifest.FileHashes.Count);
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(output)!);
        }
    }

    [Fact]
    public async Task GenerateCSharp_IsDeterministicAcrossRuns()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(FindRepositoryRoot(), "Samples"));
        var outputA = Path.Combine(Path.GetTempPath(), "metadata-gen-tests", Guid.NewGuid().ToString("N"), "a");
        var outputB = Path.Combine(Path.GetTempPath(), "metadata-gen-tests", Guid.NewGuid().ToString("N"), "b");

        try
        {
            var manifestA = GenerationService.GenerateCSharp(workspace, outputA);
            var manifestB = GenerationService.GenerateCSharp(workspace, outputB);

            Assert.True(GenerationService.AreEquivalent(manifestA, manifestB, out var message), message);
            Assert.True(File.Exists(Path.Combine(outputA, workspace.Model.Name + ".cs")));
            Assert.True(File.Exists(Path.Combine(outputA, "Cube.cs")));
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputA)!);
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputB)!);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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
