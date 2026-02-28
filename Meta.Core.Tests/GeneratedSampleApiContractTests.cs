using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Meta.Core.Tests;

public sealed class GeneratedSampleApiContractTests
{
    [Fact]
    public void SampleModel_GeneratedApiShape_MatchesStaticFacadeAndInstanceContract()
    {
        var repoRoot = FindRepositoryRoot();
        var generatedDirectory = Path.Combine(repoRoot, "Samples", "ConsumerApi", "Generated");
        var modelPath = Path.Combine(generatedDirectory, "EnterpriseBIPlatform.cs");
        var entityPath = Path.Combine(generatedDirectory, "Measure.cs");
        var modelCode = File.ReadAllText(modelPath);
        var entityCode = File.ReadAllText(entityPath);

        Assert.Contains("namespace EnterpriseBIPlatform", modelCode, StringComparison.Ordinal);
        Assert.Contains("public static class EnterpriseBIPlatform", modelCode, StringComparison.Ordinal);
        Assert.Contains("private static readonly EnterpriseBIPlatformInstance _builtIn", modelCode, StringComparison.Ordinal);
        Assert.Contains("public static EnterpriseBIPlatformInstance BuiltIn", modelCode, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<Measure> Measures", modelCode, StringComparison.Ordinal);
        Assert.Contains("public sealed class EnterpriseBIPlatformInstance", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public static class EnterpriseBIPlatformModel", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadFromXmlWorkspace", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveToXmlWorkspace", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("GetId(int id)", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetId(int id", modelCode, StringComparison.Ordinal);
        Assert.DoesNotContain("MeasureList", modelCode, StringComparison.Ordinal);

        Assert.Contains("namespace EnterpriseBIPlatform", entityCode, StringComparison.Ordinal);
        Assert.Contains("public string Id { get; internal set; }", entityCode, StringComparison.Ordinal);
        Assert.Contains("public string CubeId { get; internal set; }", entityCode, StringComparison.Ordinal);
        Assert.Contains("public Cube Cube { get; internal set; }", entityCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public int Id { get; }", entityCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public int CubeId { get; }", entityCode, StringComparison.Ordinal);
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
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        Assert.True(process.ExitCode == 0, $"Samples.Console failed with exit code {process.ExitCode}.{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
        Assert.Contains("Systems:", stdOut, StringComparison.Ordinal);
        Assert.Contains("Enterprise Analytics Platform [Internal]", stdOut, StringComparison.Ordinal);
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

