using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Meta.Adapters;
using Meta.Core.Domain;
using Meta.Core.Services;

namespace Meta.Core.Tests;

public sealed class DeterminismGoldenTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedXmlMetadataHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["instance/Cube.xml"] = "7e1cdd9d9e3ef20bf50dda876e4ce435809019c4d6d3dbbd2f2fc885c3af1858",
            ["instance/Dimension.xml"] = "0465171623420221211b6a4aceb597227fcfb06d79dc489d495994faf9b635d7",
            ["instance/Fact.xml"] = "03482418a41841181a954ab3bd23d93a78f6f7afef652eebf585e4270737de1f",
            ["instance/Measure.xml"] = "9805a5fe77a8952bb1bed8f274e8f37a52ec55b3409c6da6706e39141e69796f",
            ["instance/System.xml"] = "da7a807995ff53dfc337f344f8b7173c011c718f5b64ee880c91554781dfce12",
            ["instance/SystemCube.xml"] = "0d012bd20081ed6ae31d2a38cd8b687ba4381f13cae1a5aabe264c04366b702d",
            ["instance/SystemDimension.xml"] = "c8c495f17a6db14cccf0e000097b73cbdccdb1e0be29e21e057fa6e414831439",
            ["instance/SystemFact.xml"] = "33b7bba7b37768b09b8e3b19122fe29ad063473835ed9155561aa53f5ed5d583",
            ["instance/SystemType.xml"] = "61bd50d754f2a26b860ba877eb5429174ea26e567766dc634b78ed3f5848fb4e",
            ["model.xml"] = "6e473c65afd30cac887e822980f4ba541760da99e87ea4ec9c70c89f75b16c09",
            ["workspace.xml"] = "ca0ab519ee08b5a4c85eb08069236f1bafb1aac320d963614aa02c6e252d44ec",
        };

    private const string ExpectedXmlMetadataCombinedHash = "b8a8b2974e73779da552b01e0fd229006eebc2a35f17aabb60d0b1c890f347cd";

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
            ["Measure.cs"] = "77eed83ec53063bcef21ad8e11ff79a85ecf30a39179b1e51010ac9712342ade",
            ["System.cs"] = "aacf31c6cff5225fbe816da482d650ab2a48934e1a3926dec2326371677a2572",
            ["SystemCube.cs"] = "5e5fec0ccf027690461d4913eddbd78628479aafbd116322c2285710dda7ef83",
            ["SystemDimension.cs"] = "4834676f07bff9f1e02089f0b93a3c499d380fdc4b94f0f7c89d36e3c3325edc",
            ["SystemFact.cs"] = "7faad743945ce43057243112175f4276d499b1048b45ac12e50ae147b5a06e77",
            ["SystemType.cs"] = "a3b54fbdfd2d315b7f3d2501f6f3ffd457dcb320f52f5606fb53c9d1549d8f9a",
        };

    private const string ExpectedCSharpCombinedHash = "49040a8397dae33e630f33652097aa8febf321f384ee622769fc968472a86854";

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
