using System;
using System.IO;
using System.Threading.Tasks;
using Meta.Adapters;

namespace Meta.Core.Tests;

public sealed class AdapterExportServiceTests
{
    [Fact]
    public async Task ExportSql_WritesSchemaAndDataFiles()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(FindRepositoryRoot(), "Samples"));
        var outputRoot = Path.Combine(Path.GetTempPath(), "metadata-adapter-tests", Guid.NewGuid().ToString("N"));
        var schemaPath = Path.Combine(outputRoot, "schema", "model.sql");
        var dataPath = Path.Combine(outputRoot, "data", "instance.sql");

        try
        {
            await services.ExportService.ExportSqlAsync(workspace, schemaPath, dataPath);

            Assert.True(File.Exists(schemaPath));
            Assert.True(File.Exists(dataPath));

            var schema = await File.ReadAllTextAsync(schemaPath);
            var data = await File.ReadAllTextAsync(dataPath);
            Assert.Contains("CREATE TABLE", schema, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("INSERT INTO", data, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectoryIfExists(outputRoot);
        }
    }

    [Fact]
    public async Task ExportCSharp_WritesModelAndEntityFiles()
    {
        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService.LoadAsync(Path.Combine(FindRepositoryRoot(), "Samples"));
        var outputRoot = Path.Combine(Path.GetTempPath(), "metadata-adapter-tests", Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(outputRoot, "generated");

        try
        {
            await services.ExportService.ExportCSharpAsync(workspace, outputDirectory);

            var modelPath = Path.Combine(outputDirectory, workspace.Model.Name + ".cs");
            var entityPath = Path.Combine(outputDirectory, "Cube.cs");
            Assert.True(File.Exists(modelPath));
            Assert.True(File.Exists(entityPath));

            var modelText = await File.ReadAllTextAsync(modelPath);
            Assert.Contains($"namespace {workspace.Model.Name}", modelText, StringComparison.Ordinal);
            Assert.Contains($"public static class {workspace.Model.Name}", modelText, StringComparison.Ordinal);
            Assert.Contains("public static IReadOnlyList<Measure> Measures", modelText, StringComparison.Ordinal);
            Assert.Contains("private static readonly EnterpriseBIPlatformInstance _builtIn", modelText, StringComparison.Ordinal);
            Assert.Contains("MeasureName = \"number_of_things\"", modelText, StringComparison.Ordinal);

            var entityText = await File.ReadAllTextAsync(entityPath);
            Assert.Contains("public string CubeName { get; internal set; }", entityText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectoryIfExists(outputRoot);
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
