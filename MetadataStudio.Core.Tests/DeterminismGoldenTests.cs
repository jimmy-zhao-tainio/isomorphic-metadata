using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MetadataStudio.Adapters;
using MetadataStudio.Core.Domain;
using MetadataStudio.Core.Services;

namespace MetadataStudio.Core.Tests;

public sealed class DeterminismGoldenTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedXmlMetadataHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["instance/Cube.xml"] = "38e2870cc216605b3864a8937d2db7f203cf8efdfa244f1cc69f92edb8a64dc4",
            ["instance/Dimension.xml"] = "1d57f85733c3c88804a9709bfbb9ee4db50fb786022a5c9aa532646d55539e75",
            ["instance/Fact.xml"] = "71274cc995b3a9205e068c64c3a24780c03e0ae3cd29bae53af3fc41fbbc8aeb",
            ["instance/Measure.xml"] = "d5a07951a904f842a85cf9b582b07bbba29c3a030f46bf2d7b6160a50208406b",
            ["instance/System.xml"] = "68e51da68ae30019c92eaf03e1faac85600198b5e1b01eb5b8651830c535ec83",
            ["instance/SystemCube.xml"] = "c99e0e66d48be557b784a872db25d44cfb9097b22b278c21af0c291ab346685f",
            ["instance/SystemDimension.xml"] = "c6be74169de98a90be91cb9f111eef2781908878b3615765e93cba597a6ac63b",
            ["instance/SystemFact.xml"] = "6791fd3221d121de46492e1a8bd6431c8dc833c11b3efb3141600231f49413ff",
            ["instance/SystemType.xml"] = "fdb6db2b2b03c595fcd682803aa09ca11e8d21d752551e797c75a999a9f40f2d",
            ["model.xml"] = "6e473c65afd30cac887e822980f4ba541760da99e87ea4ec9c70c89f75b16c09",
            ["workspace.json"] = "e2b6e148a5fc8321c12054eddd6522c826b23f99b691f9de55ceb6c6ca9fa1ce",
        };

    private const string ExpectedXmlMetadataCombinedHash = "9a34be9d371f2a000a566dce83ea065d0cedfa266535a9a132aab7ba6d068d35";

    private static readonly IReadOnlyDictionary<string, string> ExpectedSqlHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["data.sql"] = "810c919903112a9e1caaa9bd1c77f1fde915e41430261303615cd81fc272765a",
            ["schema.sql"] = "e7d5206e17a433c3621e829ee6736de8ea76bab015674eb335b4ef25867a0974",
        };

    private const string ExpectedSqlCombinedHash = "cc068c77d683d5c9461bcda2138e236c5da72fad35d6a3d414fe5b0601e07d22";

    private static readonly IReadOnlyDictionary<string, string> ExpectedCSharpHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cube.cs"] = "78296c0e11515bd68a49320632a98b3129f985b2bd830368cd151e15f14ab4ff",
            ["Dimension.cs"] = "9bc09e08bd078d0393c892bdfb24dbd06fb1e6899955a84f368df02cbeabaee7",
            ["EnterpriseBIPlatform.cs"] = "506911d4ca17102113054d28366a68e2046d911d3516c1221a48eaf9b82da9d4",
            ["Fact.cs"] = "3ac17b963c93fbd4f56e566dc5c8006f02a4ccdad1f17c4da9602f14ab02c760",
            ["Measure.cs"] = "5de86e90270a7c10d7aeb9f419b30c341792fcdbac01cbe57299e843651073c1",
            ["System.cs"] = "b70ee2474fb3ef649b8705aab2b17efbd8d66dfa3ddb87ec279d73acf148e285",
            ["SystemCube.cs"] = "70a4007f2213e4b2995ae0d9fc150a28b158d9a51472ba1f6dc53f66fd1413d3",
            ["SystemDimension.cs"] = "9b00644f8f7a9a105276a2babefbc6bfccb935f9b841b0217954df29340748e0",
            ["SystemFact.cs"] = "144c54d53d4300272a08ed17e645d98fc8eb74eaf9c266e75f22f963ac24290f",
            ["SystemType.cs"] = "a3b54fbdfd2d315b7f3d2501f6f3ffd457dcb320f52f5606fb53c9d1549d8f9a",
        };

    private const string ExpectedCSharpCombinedHash = "ec772cf5df25aff0113850a2c25e2e0ed56fc40cf97314aa82c9dd78b8a1f754";

    [Fact]
    public async Task XmlCanonicalOutput_MatchesGoldenHashes()
    {
        var services = new ServiceCollection();
        var workspace = await LoadCanonicalSampleWorkspaceAsync(services);
        var outputA = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "a");
        var outputB = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "b");

        try
        {
            await services.ExportService.ExportXmlAsync(workspace, outputA);
            await services.ExportService.ExportXmlAsync(workspace, outputB);

            var metadataA = Path.Combine(outputA, "metadata");
            var metadataB = Path.Combine(outputB, "metadata");
            var manifestA = BuildDirectoryManifest(metadataA);
            var manifestB = BuildDirectoryManifest(metadataB);

            AssertManifestEqual(manifestA, manifestB);
            AssertManifestEqual(ExpectedXmlMetadataHashes, manifestA.FileHashes);
            Assert.Equal(ExpectedXmlMetadataCombinedHash, manifestA.CombinedHash);
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputA)!);
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputB)!);
        }
    }

    [Fact]
    public async Task SqlGeneration_MatchesGoldenHashes()
    {
        var services = new ServiceCollection();
        var workspace = await LoadCanonicalSampleWorkspaceAsync(services);
        var outputA = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "sql-a");
        var outputB = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "sql-b");

        try
        {
            GenerationService.GenerateSql(workspace, outputA);
            GenerationService.GenerateSql(workspace, outputB);

            var manifestA = BuildDirectoryManifest(outputA);
            var manifestB = BuildDirectoryManifest(outputB);

            AssertManifestEqual(manifestA, manifestB);
            AssertManifestEqual(ExpectedSqlHashes, manifestA.FileHashes);
            Assert.Equal(ExpectedSqlCombinedHash, manifestA.CombinedHash);
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputA)!);
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputB)!);
        }
    }

    [Fact]
    public async Task CSharpGeneration_MatchesGoldenHashes()
    {
        var services = new ServiceCollection();
        var workspace = await LoadCanonicalSampleWorkspaceAsync(services);
        var outputA = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "cs-a");
        var outputB = Path.Combine(Path.GetTempPath(), "metadata-golden-tests", Guid.NewGuid().ToString("N"), "cs-b");

        try
        {
            GenerationService.GenerateCSharp(workspace, outputA);
            GenerationService.GenerateCSharp(workspace, outputB);

            var manifestA = BuildDirectoryManifest(outputA);
            var manifestB = BuildDirectoryManifest(outputB);

            AssertManifestEqual(manifestA, manifestB);
            AssertManifestEqual(ExpectedCSharpHashes, manifestA.FileHashes);
            Assert.Equal(ExpectedCSharpCombinedHash, manifestA.CombinedHash);
        }
        finally
        {
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputA)!);
            DeleteDirectoryIfExists(Path.GetDirectoryName(outputB)!);
        }
    }

    private static void AssertManifestEqual(DirectoryManifest expected, DirectoryManifest actual)
    {
        AssertManifestEqual(expected.FileHashes, actual.FileHashes);
        Assert.Equal(expected.CombinedHash, actual.CombinedHash);
    }

    private static void AssertManifestEqual(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var item in expected.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            Assert.True(actual.TryGetValue(item.Key, out var actualHash), $"Missing output file '{item.Key}'.");
            Assert.Equal(item.Value, actualHash);
        }
    }

    private static DirectoryManifest BuildDirectoryManifest(string rootPath)
    {
        var root = Path.GetFullPath(rootPath);
        var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            fileHashes[relativePath] = ComputeFileHash(filePath);
        }

        return new DirectoryManifest
        {
            FileHashes = fileHashes,
            CombinedHash = ComputeCombinedHash(fileHashes),
        };
    }

    private static string ComputeFileHash(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string ComputeCombinedHash(IReadOnlyDictionary<string, string> fileHashes)
    {
        var payload = string.Join(
            "\n",
            fileHashes
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}:{item.Value}"));
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
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

    private static Task<Workspace> LoadCanonicalSampleWorkspaceAsync(ServiceCollection services)
    {
        var repoRoot = FindRepositoryRoot();
        return services.ImportService.ImportXmlAsync(
            Path.Combine(repoRoot, "Samples", "SampleModel.xml"),
            Path.Combine(repoRoot, "Samples", "SampleInstance.xml"));
    }

    private sealed class DirectoryManifest
    {
        public IReadOnlyDictionary<string, string> FileHashes { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string CombinedHash { get; set; } = string.Empty;
    }
}
