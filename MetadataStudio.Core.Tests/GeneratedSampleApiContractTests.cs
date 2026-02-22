using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace MetadataStudio.Core.Tests;

public sealed class GeneratedSampleApiContractTests
{
    [Fact]
    public void SampleModel_GeneratedApiShape_MatchesSingletonPluralCollectionContract()
    {
        var repoRoot = FindRepositoryRoot();
        var generatedPath = Path.Combine(repoRoot, "Samples", "SampleModel.cs");
        var code = File.ReadAllText(generatedPath);

        Assert.Contains("public sealed class EnterpriseBIPlatform", code, StringComparison.Ordinal);
        Assert.Contains("public static class EnterpriseBIPlatformModel", code, StringComparison.Ordinal);
        Assert.Contains("private static readonly EnterpriseBIPlatform _current", code, StringComparison.Ordinal);
        Assert.Contains("public static EnterpriseBIPlatform Current", code, StringComparison.Ordinal);
        Assert.Contains("public static EnterpriseBIPlatform LoadFromXml", code, StringComparison.Ordinal);
        Assert.Contains("public Measures Measures", code, StringComparison.Ordinal);
        Assert.Contains("public sealed class Measures : IEnumerable<Measure>", code, StringComparison.Ordinal);
        Assert.Contains("public Measure GetId(int id)", code, StringComparison.Ordinal);
        Assert.Contains("public bool TryGetId(int id, out Measure row)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("is not loaded. Call EnterpriseBIPlatformModel.LoadFromXml/LoadFromSql first.", code, StringComparison.Ordinal);
        Assert.Contains("public int? CubeId { get; }", code, StringComparison.Ordinal);
        Assert.Contains("public Cube Cube { get; internal set; }", code, StringComparison.Ordinal);
        Assert.DoesNotContain("MeasureList", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleConsole_LoadForeachGetIdAndNavigation_Work()
    {
        var repoRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(repoRoot, "Samples.Console", "Samples.Console.csproj"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("Samples");

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        Assert.True(process.ExitCode == 0, $"Samples.Console failed with exit code {process.ExitCode}.{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
        Assert.Contains("Measures:", stdOut, StringComparison.Ordinal);
        Assert.Contains("Measure Id=1", stdOut, StringComparison.Ordinal);
        Assert.Contains("Lookup example:", stdOut, StringComparison.Ordinal);
        Assert.Contains("Measure 1 cube = Sales Performance", stdOut, StringComparison.Ordinal);
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
