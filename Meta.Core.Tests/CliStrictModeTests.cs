using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Meta.Adapters;
using Meta.Core.Domain;
using Meta.Core.Services;

namespace Meta.Core.Tests;

public sealed class CliStrictModeTests
{
    private static readonly SemaphoreSlim CliBuildGate = new(1, 1);
    private static string? cliAssemblyPath;

    [Fact]
    public void CommandExamples_DoNotContainLegacyHumanErrorTokens()
    {
        var repoRoot = FindRepositoryRoot();
        var examplesPath = Path.Combine(repoRoot, "COMMANDS-EXAMPLES.md");
        var content = File.ReadAllText(examplesPath);

        Assert.DoesNotContain("Where:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("instance.relationship.orphan", content, StringComparison.Ordinal);
        Assert.DoesNotContain("contains(Id,'')", content, StringComparison.Ordinal);
        Assert.DoesNotContain("--where", content, StringComparison.Ordinal);
        Assert.DoesNotContain("contains(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandDocs_DoNotContainWhereDslTokens()
    {
        var repoRoot = FindRepositoryRoot();
        var commands = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS.md"));
        var examples = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS-EXAMPLES.md"));
        var combined = commands + Environment.NewLine + examples;

        Assert.DoesNotContain("--where", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("contains(", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceDocs_DoNotAdvertiseIdSwitch_ForRowTargeting()
    {
        var repoRoot = FindRepositoryRoot();
        var commands = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS.md"));
        var examples = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS-EXAMPLES.md"));
        var cliProgram = File.ReadAllText(Path.Combine(repoRoot, "Meta.Cli", "Program.cs"));
        var generator = File.ReadAllText(Path.Combine(repoRoot, "scripts", "Generate-CommandExamples.ps1"));
        var combined = string.Join(Environment.NewLine, new[] { commands, examples, cliProgram, generator });

        Assert.DoesNotMatch(@"(?im)^.*Usage:.*--id.*$", combined);
        Assert.DoesNotMatch(@"(?im)^.*Next:.*--id.*$", combined);
        Assert.DoesNotMatch(@"(?im)^.*example:.*--id.*$", combined);
        Assert.DoesNotMatch(@"(?im)^.*meta\s+.*--id.*$", combined);
    }

    [Fact]
    public void SurfaceDocs_DoNotAdvertiseSetId_ForRowTargeting()
    {
        var repoRoot = FindRepositoryRoot();
        var commands = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS.md"));
        var examples = File.ReadAllText(Path.Combine(repoRoot, "COMMANDS-EXAMPLES.md"));
        var generator = File.ReadAllText(Path.Combine(repoRoot, "scripts", "Generate-CommandExamples.ps1"));
        var combined = string.Join(Environment.NewLine, new[] { commands, examples, generator });

        Assert.DoesNotContain("--set Id=", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HumanFailures_DoNotLeakDiagnosticKeyValueTokens_AndUseSingleNext()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        var nonEmptyImportTarget = Path.Combine(Path.GetTempPath(), "metadata-import-target", Guid.NewGuid().ToString("N"));
        var brokenWorkspaceRoot = Path.Combine(Path.GetTempPath(), "metadata-broken", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nonEmptyImportTarget);
        await File.WriteAllTextAsync(Path.Combine(nonEmptyImportTarget, "placeholder.txt"), "x");

        Directory.CreateDirectory(Path.Combine(brokenWorkspaceRoot, "metadata", "instance"));
        await File.WriteAllTextAsync(
            Path.Combine(brokenWorkspaceRoot, "metadata", "workspace.xml"),
            "<MetaWorkspace><Workspaces><Workspace Id=\"1\" WorkspaceLayoutId=\"1\" EncodingId=\"1\" NewlinesId=\"1\" EntitiesOrderId=\"1\" PropertiesOrderId=\"1\" RelationshipsOrderId=\"1\" RowsOrderId=\"2\" AttributesOrderId=\"3\"><Name>Workspace</Name><FormatVersion>1.0</FormatVersion></Workspace></Workspaces><WorkspaceLayouts><WorkspaceLayout Id=\"1\"><ModelFilePath>metadata/model.xml</ModelFilePath><InstanceDirPath>metadata/instance</InstanceDirPath></WorkspaceLayout></WorkspaceLayouts><Encodings><Encoding Id=\"1\"><Name>utf-8-no-bom</Name></Encoding></Encodings><NewlinesValues><Newlines Id=\"1\"><Name>lf</Name></Newlines></NewlinesValues><CanonicalOrders><CanonicalOrder Id=\"1\"><Name>name-ordinal</Name></CanonicalOrder><CanonicalOrder Id=\"2\"><Name>id-ordinal</Name></CanonicalOrder><CanonicalOrder Id=\"3\"><Name>id-first-then-name-ordinal</Name></CanonicalOrder></CanonicalOrders><EntityStorages /></MetaWorkspace>");
        await File.WriteAllTextAsync(
            Path.Combine(brokenWorkspaceRoot, "metadata", "model.xml"),
            "<Model name=\"Broken\"><Entities><Entity name=\"X\"></Entities></Model>");

        try
        {
            var outputs = new[]
            {
                (await RunCliAsync("view", "entity", "MissingEntity", "--workspace", workspaceRoot)).CombinedOutput,
                (await RunCliAsync(
                    "import",
                    "xml",
                    @"Samples\SampleModel.xml",
                    @"Samples\SampleInstance.xml",
                    "--new-workspace",
                    nonEmptyImportTarget)).CombinedOutput,
                (await RunCliAsync("status", "--workspace", brokenWorkspaceRoot)).CombinedOutput,
            };

            var bannedTokens = new[]
            {
                "endPos=",
                "startPos=",
                "file=",
                "workspace=",
                "entity=",
                "occurrences=",
                "entries=",
                "sampleEntries=",
            };

            foreach (var output in outputs)
            {
                foreach (var banned in bannedTokens)
                {
                    Assert.DoesNotContain(banned, output, StringComparison.Ordinal);
                }

                var nextCount = Regex.Matches(output, @"(?m)^Next:\s").Count;
                Assert.True(nextCount <= 1, $"Expected at most one Next line, got {nextCount}:{Environment.NewLine}{output}");
            }
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
            DeleteDirectorySafe(nonEmptyImportTarget);
            DeleteDirectorySafe(brokenWorkspaceRoot);
        }
    }

    [Fact]
    public async Task NotFoundMessages_UseCanonicalTemplates_AcrossCommandFamilies()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var missingEntity = await RunCliAsync("view", "entity", "MissingEntity", "--workspace", workspaceRoot);
            var missingRow = await RunCliAsync("view", "instance", "Cube", "999", "--workspace", workspaceRoot);
            var missingProperty = await RunCliAsync(
                "query",
                "Cube",
                "--contains",
                "MissingField",
                "Value",
                "--workspace",
                workspaceRoot);
            var missingRelationship = await RunCliAsync(
                "instance",
                "relationship",
                "clear",
                "Cube",
                "1",
                "--to-entity",
                "System",
                "--workspace",
                workspaceRoot);

            Assert.Contains("Entity 'MissingEntity' was not found.", missingEntity.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Instance 'Cube 999' was not found.", missingRow.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Property 'Cube.MissingField' was not found.", missingProperty.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Relationship 'Cube->System' was not found.", missingRelationship.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task XmlParseErrors_UseSameEnvelope_InStatusAndGenerate()
    {
        var brokenWorkspaceRoot = Path.Combine(Path.GetTempPath(), "metadata-broken", Guid.NewGuid().ToString("N"));
        var outputRoot = Path.Combine(Path.GetTempPath(), "metadata-generate-out", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(brokenWorkspaceRoot, "metadata", "instance"));
        await File.WriteAllTextAsync(
            Path.Combine(brokenWorkspaceRoot, "metadata", "workspace.xml"),
            "<MetaWorkspace><Workspaces><Workspace Id=\"1\" WorkspaceLayoutId=\"1\" EncodingId=\"1\" NewlinesId=\"1\" EntitiesOrderId=\"1\" PropertiesOrderId=\"1\" RelationshipsOrderId=\"1\" RowsOrderId=\"2\" AttributesOrderId=\"3\"><Name>Workspace</Name><FormatVersion>1.0</FormatVersion></Workspace></Workspaces><WorkspaceLayouts><WorkspaceLayout Id=\"1\"><ModelFilePath>metadata/model.xml</ModelFilePath><InstanceDirPath>metadata/instance</InstanceDirPath></WorkspaceLayout></WorkspaceLayouts><Encodings><Encoding Id=\"1\"><Name>utf-8-no-bom</Name></Encoding></Encodings><NewlinesValues><Newlines Id=\"1\"><Name>lf</Name></Newlines></NewlinesValues><CanonicalOrders><CanonicalOrder Id=\"1\"><Name>name-ordinal</Name></CanonicalOrder><CanonicalOrder Id=\"2\"><Name>id-ordinal</Name></CanonicalOrder><CanonicalOrder Id=\"3\"><Name>id-first-then-name-ordinal</Name></CanonicalOrder></CanonicalOrders><EntityStorages /></MetaWorkspace>");
        await File.WriteAllTextAsync(
            Path.Combine(brokenWorkspaceRoot, "metadata", "model.xml"),
            "<Model name=\"Broken\"><Entities><Entity name=\"X\"></Entities></Model>");

        try
        {
            var status = await RunCliAsync("status", "--workspace", brokenWorkspaceRoot);
            var generate = await RunCliAsync(
                "generate",
                "sql",
                "--out",
                outputRoot,
                "--workspace",
                brokenWorkspaceRoot);

            Assert.Equal(4, status.ExitCode);
            Assert.Equal(4, generate.ExitCode);

            Assert.Contains("Cannot parse metadata/model.xml.", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Cannot parse metadata/model.xml.", generate.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Location: line 1, position", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Location: line 1, position", generate.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next: meta check", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next: meta check", generate.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Usage:", generate.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(brokenWorkspaceRoot);
            DeleteDirectorySafe(outputRoot);
        }
    }

    [Fact]
    public async Task ModelWithDisplayKeyAttribute_FailsWithClearError()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var modelPath = Path.Combine(workspaceRoot, "metadata", "model.xml");
            var model = XDocument.Load(modelPath);
            var firstEntity = model.Descendants("Entity").First();
            firstEntity.SetAttributeValue("displayKey", "Name");
            model.Save(modelPath);

            var status = await RunCliAsync("status", "--workspace", workspaceRoot);
            Assert.Equal(4, status.ExitCode);
            Assert.Contains("unsupported attribute 'displayKey'", status.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Help_OverviewAndCommandHelp_AreAvailable()
    {
        var overview = await RunCliAsync("help");
        Assert.Equal(0, overview.ExitCode);
        Assert.DoesNotContain("Meta CLI", overview.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Version:", overview.StdOut, StringComparison.Ordinal);
        Assert.Contains("Workspace", overview.StdOut, StringComparison.Ordinal);
        Assert.Contains("Model", overview.StdOut, StringComparison.Ordinal);
        Assert.Contains("Instance", overview.StdOut, StringComparison.Ordinal);
        Assert.Contains("Pipeline", overview.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility", overview.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("random", overview.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Command  Description", overview.StdOut, StringComparison.Ordinal);
        foreach (var line in overview.StdOut.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            Assert.Equal(line.TrimEnd(), line);
        }
        Assert.Contains("Next: meta <command> help", overview.StdOut, StringComparison.Ordinal);

        var commandHelp = await RunCliAsync("model", "--help");
        Assert.Equal(0, commandHelp.ExitCode);
        Assert.Contains("model", commandHelp.StdOut, StringComparison.Ordinal);
        Assert.Contains("Usage:", commandHelp.StdOut, StringComparison.Ordinal);
        Assert.Contains("Next: meta model <subcommand> help", commandHelp.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArgumentError_IncludesUsageAndNext()
    {
        var result = await RunCliAsync("model", "add-entity");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("missing required argument <Name>", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usage: meta model add-entity <Name> [--workspace <path>]", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Next: meta model add-entity help", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_RejectsScopeOption()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync("check", "--scope", "all", "--workspace", workspaceRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("unknown option --scope", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task ModelAddEntity_AppliesByDefault()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync("model", "add-entity", "SmokeEntity", "--workspace", workspaceRoot);
            Assert.True(result.ExitCode == 0, result.CombinedOutput);

            var modelPath = Path.Combine(workspaceRoot, "metadata", "model.xml");
            var shardPath = Path.Combine(workspaceRoot, "metadata", "instance", "SmokeEntity.xml");
            Assert.True(File.Exists(modelPath), "Expected metadata/model.xml to be written.");
            Assert.True(File.Exists(shardPath), "Expected metadata/instance/SmokeEntity.xml to be written.");

            var modelDocument = XDocument.Load(modelPath);
            Assert.NotNull(modelDocument
                .Descendants("Entity")
                .SingleOrDefault(element => string.Equals((string?)element.Attribute("name"), "SmokeEntity", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task RandomCreate_IsNotExposed_OnCliSurface()
    {
        var result = await RunCliAsync("random", "create");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown command 'random'.", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceCommand_IsNotExposed_OnCliSurface()
    {
        var result = await RunCliAsync("workspace", "migrate");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown command 'workspace'.", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphStats_ReturnsStructuredJson()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "--json",
                "graph",
                "stats",
                "--workspace",
                workspaceRoot,
                "--top",
                "3",
                "--cycles",
                "2");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"command\": \"graph.stats\"", result.StdOut, StringComparison.Ordinal);
            Assert.Contains("\"nodes\":", result.StdOut, StringComparison.Ordinal);
            Assert.Contains("\"topOutDegree\":", result.StdOut, StringComparison.Ordinal);
            Assert.Contains("\"topInDegree\":", result.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task ViewRow_RejectsLegacySymbolicRowReference()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var legacyRowReference = string.Concat("Cube", "#", "1");
            var result = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                legacyRowReference,
                "--workspace",
                workspaceRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("unsupported instance reference", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Use <Entity> <Id>", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task RowTargetCommands_RejectLegacyIdSwitch()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var cases = new[]
            {
                new[] { "view", "instance", "Cube", "1", "--id", "1", "--workspace", workspaceRoot },
                new[] { "insert", "Cube", "10", "--id", "10", "--set", "CubeName=Test", "--workspace", workspaceRoot },
                new[] { "instance", "update", "Cube", "1", "--id", "1", "--set", "RefreshMode=Manual", "--workspace", workspaceRoot },
                new[] { "delete", "Cube", "1", "--id", "1", "--workspace", workspaceRoot },
                new[] { "instance", "relationship", "set", "Measure", "--id", "1", "--to", "Cube", "1", "--workspace", workspaceRoot },
            };

            foreach (var command in cases)
            {
                var result = await RunCliAsync(command);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("unknown option", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Usage:", result.CombinedOutput, StringComparison.Ordinal);
                Assert.Contains("Next:", result.CombinedOutput, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task RowTargetCommands_RejectSetIdAsRowIdentifier()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var cases = new[]
            {
                new[] { "insert", "Cube", "10", "--set", "Id=10", "--set", "CubeName=Bad", "--workspace", workspaceRoot },
                new[] { "instance", "update", "Cube", "1", "--set", "Id=2", "--workspace", workspaceRoot },
            };

            foreach (var command in cases)
            {
                var result = await RunCliAsync(command);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("do not use --set Id", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Usage:", result.CombinedOutput, StringComparison.Ordinal);
                Assert.Contains("Next:", result.CombinedOutput, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Insert_AutoId_CreatesNextNumericRow()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "--auto-id",
                "--set",
                "CubeName=Auto Id Cube",
                "--workspace",
                workspaceRoot);

            Assert.Equal(0, insertResult.ExitCode);
            Assert.Contains("created Cube 3", insertResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);

            var viewResult = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                "3",
                "--workspace",
                workspaceRoot);

            Assert.Equal(0, viewResult.ExitCode);
            Assert.Contains("Instance: Cube 3", viewResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Insert_AutoId_CannotBeCombinedWithPositionalId()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "insert",
                "Cube",
                "10",
                "--auto-id",
                "--set",
                "CubeName=Conflicting",
                "--workspace",
                workspaceRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("cannot be combined", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Usage:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next:", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Insert_AutoId_UsesNextNumericId()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var autoIdInsert = await RunCliAsync(
                "insert",
                "Cube",
                "--auto-id",
                "--set",
                "CubeName=Auto Generated",
                "--set",
                "Purpose=Auto id test",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                workspaceRoot);

            Assert.Equal(0, autoIdInsert.ExitCode);

            var viewResult = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                "3",
                "--workspace",
                workspaceRoot);
            Assert.Equal(0, viewResult.ExitCode);
            Assert.Contains("Auto Generated", viewResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task BulkInsert_AutoId_CreatesNextNumericRows()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        var tsvPath = Path.Combine(workspaceRoot, "bulk-auto-id.tsv");
        await File.WriteAllLinesAsync(
            tsvPath,
            new[]
            {
                "CubeName\tPurpose\tRefreshMode",
                "Auto Cube A\tAuto row A\tManual",
                "Auto Cube B\tAuto row B\tScheduled",
            });

        try
        {
            var result = await RunCliAsync(
                "bulk-insert",
                "Cube",
                "--from",
                "tsv",
                "--file",
                tsvPath,
                "--auto-id",
                "--workspace",
                workspaceRoot);

            Assert.Equal(0, result.ExitCode);

            var viewThree = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                "3",
                "--workspace",
                workspaceRoot);
            var viewFour = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                "4",
                "--workspace",
                workspaceRoot);

            Assert.Equal(0, viewThree.ExitCode);
            Assert.Equal(0, viewFour.ExitCode);
            Assert.Contains("Instance: Cube 3", viewThree.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Instance: Cube 4", viewFour.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task BulkInsert_AutoId_RejectsKeyCombination()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        var tsvPath = Path.Combine(workspaceRoot, "bulk-auto-id-conflict.tsv");
        await File.WriteAllLinesAsync(
            tsvPath,
            new[]
            {
                "CubeName\tPurpose\tRefreshMode",
                "Auto Cube Conflict\tConflict row\tManual",
            });

        try
        {
            var result = await RunCliAsync(
                "bulk-insert",
                "Cube",
                "--from",
                "tsv",
                "--file",
                tsvPath,
                "--auto-id",
                "--key",
                "Id",
                "--workspace",
                workspaceRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("cannot be combined", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Usage:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next:", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task UnknownFieldOrColumnErrors_ProvideSingleNextAction()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        var tsvPath = Path.Combine(workspaceRoot, "bad-bulk-insert.tsv");
        await File.WriteAllLinesAsync(
            tsvPath,
            new[]
            {
                "Id\tUnknownColumn",
                "1\tBadValue",
            });

        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "10",
                "--set",
                "MissingField=WillFail",
                "--workspace",
                workspaceRoot);
            var rowUpdateResult = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "MissingField=BadValue",
                "--workspace",
                workspaceRoot);
            var bulkInsertResult = await RunCliAsync(
                "bulk-insert",
                "Cube",
                "--from",
                "tsv",
                "--file",
                tsvPath,
                "--key",
                "Id",
                "--workspace",
                workspaceRoot);

            foreach (var result in new[] { insertResult, rowUpdateResult, bulkInsertResult })
            {
                Assert.Equal(4, result.ExitCode);
                Assert.Contains("Property 'Cube.", result.CombinedOutput, StringComparison.Ordinal);
                Assert.Contains("' was not found.", result.CombinedOutput, StringComparison.Ordinal);
                var nextMatches = Regex.Matches(result.CombinedOutput, @"(?m)^Next:\s+meta list properties Cube\s*$");
                Assert.Single(nextMatches.Cast<Match>());
            }
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task ImportXml_RequiresNewWorkspaceOption()
    {
        var result = await RunCliAsync(
            "import",
            "xml",
            @"Samples\SampleModel.xml",
            @"Samples\SampleInstance.xml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("import requires --new-workspace", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportSql_RequiresNewWorkspaceOption()
    {
        var result = await RunCliAsync(
            "import",
            "sql",
            "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False",
            "dbo");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("import requires --new-workspace", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportXml_RejectsNonEmptyTargetDirectory()
    {
        var targetRoot = Path.Combine(Path.GetTempPath(), "metadata-import-target", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetRoot);
        await File.WriteAllTextAsync(Path.Combine(targetRoot, "placeholder.txt"), "x");

        try
        {
            var result = await RunCliAsync(
                "import",
                "xml",
                @"Samples\SampleModel.xml",
                @"Samples\SampleInstance.xml",
                "--new-workspace",
                targetRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("new workspace target directory must be empty", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(targetRoot);
        }
    }

    [Fact]
    public async Task ModelDropRelationship_FailsWhenRelationshipUsageExists()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "model",
                "drop-relationship",
                "Measure",
                "Cube",
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("Relationship 'Measure->Cube' is in use", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Relationship usage blockers:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next: meta instance relationship set Measure 1 --to Cube <ToId>", result.CombinedOutput, StringComparison.Ordinal);
            var lines = result.CombinedOutput
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd())
                .ToArray();
            Assert.True(lines.Length > 0, "Expected non-empty output.");
            Assert.StartsWith("Next:", lines[^1], StringComparison.Ordinal);
            Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("occurrences=", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task ModelDropEntity_FailsWhenEntityHasRows()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "model",
                "drop-entity",
                "Cube",
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("Cannot drop entity Cube", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Cube has", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("contains(Id,'')", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task ModelDropEntity_FailsWhenInboundRelationshipsExist()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, (await RunCliAsync("init", workspaceRoot)).ExitCode);
            Assert.Equal(0, (await RunCliAsync("model", "add-entity", "Parent", "--workspace", workspaceRoot)).ExitCode);
            Assert.Equal(0, (await RunCliAsync("model", "add-entity", "Child", "--workspace", workspaceRoot)).ExitCode);
            Assert.Equal(0, (await RunCliAsync("model", "add-relationship", "Parent", "Child", "--workspace", workspaceRoot)).ExitCode);

            var result = await RunCliAsync(
                "model",
                "drop-entity",
                "Child",
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("Entity 'Child' has inbound relationships", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Inbound relationships:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Parent", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Delete_FailsWithHumanBlockers_WhenRelationshipUsageWouldBreak()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "delete",
                "Cube",
                "2",
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("Cannot delete Cube 2", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Blocked by existing relationships (", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("references Cube 2", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("SystemCube 2 references Cube 2", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Where:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Hint:", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("instance.relationship.orphan", result.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("contains(Id,'')", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task RowRelationship_Set_ReplacesExistingUsageDeterministically()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var listBeforeSet = await RunCliAsync(
                "instance",
                "relationship",
                "list",
                "Measure",
                "1",
                "--workspace",
                workspaceRoot);
            Assert.Equal(0, listBeforeSet.ExitCode);
            Assert.Contains("Cube 1", listBeforeSet.StdOut, StringComparison.Ordinal);

            var setResult = await RunCliAsync(
                "instance",
                "relationship",
                "set",
                "Measure",
                "1",
                "--to",
                "Cube",
                "2",
                "--workspace",
                workspaceRoot);
            Assert.Equal(0, setResult.ExitCode);

            var listAfterSet = await RunCliAsync(
                "instance",
                "relationship",
                "list",
                "Measure",
                "1",
                "--workspace",
                workspaceRoot);
            Assert.Equal(0, listAfterSet.ExitCode);
            Assert.Contains("Cube 2", listAfterSet.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain("Cube 1", listAfterSet.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task RowRelationship_Clear_FailsBecauseRelationshipsAreRequired()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var clearResult = await RunCliAsync(
                "instance",
                "relationship",
                "clear",
                "Measure",
                "1",
                "--to-entity",
                "Cube",
                "--workspace",
                workspaceRoot);
            Assert.Equal(4, clearResult.ExitCode);
            Assert.Contains("Cannot clear required relationship 'Measure->Cube'", clearResult.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next: meta instance relationship set Measure 1 --to Cube <ToId>", clearResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task Insert_FailsWhenRequiredRelationshipIsMissing()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var result = await RunCliAsync(
                "insert",
                "Measure",
                "99",
                "--set",
                "MeasureName=Missing Cube Relationship",
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("insert is missing required relationship 'Cube'", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("--set Cube=<Id>", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task BulkInsert_FailsWhenRequiredRelationshipColumnIsMissing()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var tsvPath = Path.Combine(workspaceRoot, "measure-missing-cube.tsv");
            await File.WriteAllTextAsync(
                tsvPath,
                "Id\tMeasureName\n99\tMissing Cube Relationship\n");

            var result = await RunCliAsync(
                "bulk-insert",
                "Measure",
                "--from",
                "tsv",
                "--file",
                tsvPath,
                "--workspace",
                workspaceRoot);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("bulk-insert row 1 is missing required relationship 'Cube'", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Set column 'Cube' to a target Id", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task InstanceDiff_FailsWhenRightWorkspaceHasMissingRequiredRelationshipUsage()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        try
        {
            var measureShardPath = Path.Combine(rightWorkspace, "metadata", "instance", "Measure.xml");
            var measureShard = XDocument.Load(measureShardPath);
            var measureOne = measureShard
                .Descendants("Measure")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "1", StringComparison.OrdinalIgnoreCase));
            measureOne.SetAttributeValue("CubeId", null);
            measureShard.Save(measureShardPath);

            var result = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("missing required relationship 'CubeId'", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
        }
    }

    [Fact]
    public async Task GraphInbound_OutputIsDeterministic()
    {
        var workspaceRoot = CreateTempWorkspaceFromSamples();
        try
        {
            var first = await RunCliAsync(
                "graph",
                "inbound",
                "Cube",
                "--workspace",
                workspaceRoot,
                "--top",
                "20");
            var second = await RunCliAsync(
                "graph",
                "inbound",
                "Cube",
                "--workspace",
                workspaceRoot,
                "--top",
                "20");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Equal(first.StdOut, second.StdOut);
            Assert.Contains("Measure", first.StdOut, StringComparison.Ordinal);
            Assert.Contains("SystemCube", first.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(workspaceRoot);
        }
    }

    [Fact]
    public async Task WorkspaceDiff_ReturnsDifferencesAndPersistsDiffWorkspace()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        try
        {
            var updateResult = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Diff sample changed",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateResult.ExitCode);

            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Diff Cube",
                "--set",
                "Purpose=Diff sample",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertResult.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);

            Assert.Equal(1, diffResult.ExitCode);
            Assert.Contains("Instance diff: differences found.", diffResult.StdOut, StringComparison.Ordinal);
            var diffPathMatch = Regex.Match(diffResult.StdOut, @"(?m)^DiffWorkspace:\s*(.+)$");
            Assert.True(diffPathMatch.Success, $"Expected diff workspace path in output:{Environment.NewLine}{diffResult.StdOut}");

            var diffWorkspacePath = diffPathMatch.Groups[1].Value.Trim();
            Assert.True(Directory.Exists(diffWorkspacePath));

            var statusResult = await RunCliAsync("status", "--workspace", diffWorkspacePath);
            Assert.Equal(0, statusResult.ExitCode);
            Assert.Contains("InstanceDiffModelEqual", statusResult.StdOut, StringComparison.Ordinal);

            var leftNotInRightPath = Path.Combine(
                diffWorkspacePath,
                "metadata",
                "instance",
                "ModelLeftPropertyInstanceNotInRight.xml");
            var rightNotInLeftPath = Path.Combine(
                diffWorkspacePath,
                "metadata",
                "instance",
                "ModelRightPropertyInstanceNotInLeft.xml");
            Assert.True(File.Exists(leftNotInRightPath));
            Assert.True(File.Exists(rightNotInLeftPath));

            var leftNotInRight = XDocument.Load(leftNotInRightPath);
            var rightNotInLeft = XDocument.Load(rightNotInLeftPath);
            Assert.NotEmpty(leftNotInRight.Descendants("ModelLeftPropertyInstanceNotInRight"));
            Assert.NotEmpty(rightNotInLeft.Descendants("ModelRightPropertyInstanceNotInLeft"));
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
        }
    }

    [Fact]
    public async Task InstanceDiff_UsesIdentityIds_AndReferenceColumnsPointToExistingIds()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var updateResult = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Identity shape verification",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateResult.ExitCode);

            var diffResult = await RunCliAsync("instance", "diff", leftWorkspace, rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var modelIds = AssertIdentityIds(diffWorkspacePath, "Model");
            var diffIds = AssertIdentityIds(diffWorkspacePath, "Diff");
            var entityIds = AssertIdentityIds(diffWorkspacePath, "Entity");
            var propertyIds = AssertIdentityIds(diffWorkspacePath, "Property");
            var leftRowIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftEntityInstance");
            var rightRowIds = AssertIdentityIds(diffWorkspacePath, "ModelRightEntityInstance");
            var leftPropertyInstanceIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftPropertyInstance");
            var rightPropertyInstanceIds = AssertIdentityIds(diffWorkspacePath, "ModelRightPropertyInstance");
            var leftEntityNotInRightIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftEntityInstanceNotInRight");
            var rightEntityNotInLeftIds = AssertIdentityIds(diffWorkspacePath, "ModelRightEntityInstanceNotInLeft");
            var leftNotInRightIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftPropertyInstanceNotInRight");
            var rightNotInLeftIds = AssertIdentityIds(diffWorkspacePath, "ModelRightPropertyInstanceNotInLeft");

            _ = leftEntityNotInRightIds;
            _ = rightEntityNotInLeftIds;
            _ = leftNotInRightIds;
            _ = rightNotInLeftIds;

            AssertReferenceValuesExist(diffWorkspacePath, "Diff", "ModelId", modelIds);
            AssertReferenceValuesExist(diffWorkspacePath, "Entity", "ModelId", modelIds);
            AssertReferenceValuesExist(diffWorkspacePath, "Property", "EntityId", entityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftEntityInstance", "DiffId", diffIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightEntityInstance", "DiffId", diffIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftEntityInstance", "EntityId", entityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightEntityInstance", "EntityId", entityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftPropertyInstance", "ModelLeftEntityInstanceId", leftRowIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightPropertyInstance", "ModelRightEntityInstanceId", rightRowIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftPropertyInstance", "PropertyId", propertyIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightPropertyInstance", "PropertyId", propertyIds);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelLeftEntityInstanceNotInRight",
                "ModelLeftEntityInstanceId",
                leftRowIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelRightEntityInstanceNotInLeft",
                "ModelRightEntityInstanceId",
                rightRowIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelLeftPropertyInstanceNotInRight",
                "ModelLeftPropertyInstanceId",
                leftPropertyInstanceIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelRightPropertyInstanceNotInLeft",
                "ModelRightPropertyInstanceId",
                rightPropertyInstanceIds,
                allowEmptyRows: true);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task WorkspaceDiff_FailsHardWhenModelsDiffer()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        try
        {
            var modelResult = await RunCliAsync(
                "model",
                "add-property",
                "Cube",
                "ModelDiffOnlyProperty",
                "--required",
                "false",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, modelResult.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);

            Assert.Equal(4, diffResult.ExitCode);
            Assert.Contains("byte-identical model.xml", diffResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LeftModel:", diffResult.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("RightModel:", diffResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
        }
    }

    [Fact]
    public async Task WorkspaceMerge_AppliesDiffWorkspace()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Diff Cube",
                "--set",
                "Purpose=Diff sample",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertResult.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            var diffPathMatch = Regex.Match(diffResult.StdOut, @"(?m)^DiffWorkspace:\s*(.+)$");
            Assert.True(diffPathMatch.Success);
            diffWorkspacePath = diffPathMatch.Groups[1].Value.Trim();

            var mergeResult = await RunCliAsync(
                "instance",
                "merge",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(0, mergeResult.ExitCode);
            Assert.Contains("instance merge applied", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);

            var viewResult = await RunCliAsync(
                "view",
                "instance",
                "Cube",
                "99",
                "--workspace",
                leftWorkspace);
            Assert.Equal(0, viewResult.ExitCode);
            Assert.Contains("Diff Cube", viewResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task WorkspaceMerge_FailsOnFingerprintMismatch()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var insertRight = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Diff Cube",
                "--set",
                "Purpose=Diff sample",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertRight.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            var diffPathMatch = Regex.Match(diffResult.StdOut, @"(?m)^DiffWorkspace:\s*(.+)$");
            Assert.True(diffPathMatch.Success);
            diffWorkspacePath = diffPathMatch.Groups[1].Value.Trim();

            var mutateLeft = await RunCliAsync(
                "insert",
                "Cube",
                "100",
                "--set",
                "CubeName=Conflict Cube",
                "--set",
                "Purpose=Merge conflict sample",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                leftWorkspace);
            Assert.Equal(0, mutateLeft.ExitCode);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(1, mergeResult.ExitCode);
            Assert.Contains("precondition failed", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Next:", mergeResult.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task WorkspaceMerge_FailsHardWhenDiffContainsModelDeltas()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Diff Cube",
                "--set",
                "Purpose=Diff sample",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertResult.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var diffShardPath = Path.Combine(diffWorkspacePath, "metadata", "instance", "Diff.xml");
            var diffShard = XDocument.Load(diffShardPath);
            var summaryRow = diffShard.Descendants("Diff").Single();
            summaryRow.SetAttributeValue("ModelId", null);
            summaryRow.Element("ModelId")?.Remove();
            diffShard.Save(diffShardPath);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge",
                leftWorkspace,
                diffWorkspacePath);

            Assert.Equal(4, mergeResult.ExitCode);
            Assert.Contains("missing required relationship", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ModelId", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task Insert_RejectsNonNumericIds()
    {
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "b|a",
                "--set",
                "CubeName=Pipe Cube",
                "--set",
                "Purpose=Opaque id test",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(4, insertResult.ExitCode);
            Assert.Contains("invalid Id", insertResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(rightWorkspace);
        }
    }

    [Fact]
    public async Task InstanceDiffAligned_ReturnsCleanWhenMappedSubsetEqual()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            var renamePropertyResult = await RunCliAsync(
                "model",
                "rename-property",
                "Cube",
                "Purpose",
                "BusinessPurpose",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, renamePropertyResult.ExitCode);

            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    ("Purpose", "BusinessPurpose"),
                    ("RefreshMode", "RefreshMode"),
                });

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(0, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);
            Assert.Contains("no differences", diffResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceDiffAligned_UsesIdentityIds_AndReferenceColumnsPointToExistingIds()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            var renamePropertyResult = await RunCliAsync(
                "model",
                "rename-property",
                "Cube",
                "Purpose",
                "BusinessPurpose",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, renamePropertyResult.ExitCode);

            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    ("Purpose", "BusinessPurpose"),
                    ("RefreshMode", "RefreshMode"),
                });

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(0, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var modelIds = AssertIdentityIds(diffWorkspacePath, "Model");
            var modelLeftIds = AssertIdentityIds(diffWorkspacePath, "ModelLeft");
            var modelRightIds = AssertIdentityIds(diffWorkspacePath, "ModelRight");
            var alignmentIds = AssertIdentityIds(diffWorkspacePath, "Alignment");
            var modelLeftEntityIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftEntity");
            var modelRightEntityIds = AssertIdentityIds(diffWorkspacePath, "ModelRightEntity");
            var modelLeftPropertyIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftProperty");
            var modelRightPropertyIds = AssertIdentityIds(diffWorkspacePath, "ModelRightProperty");
            var entityMapIds = AssertIdentityIds(diffWorkspacePath, "EntityMap");
            var propertyMapIds = AssertIdentityIds(diffWorkspacePath, "PropertyMap");
            var leftRowIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftEntityInstance");
            var rightRowIds = AssertIdentityIds(diffWorkspacePath, "ModelRightEntityInstance");
            var leftPropertyInstanceIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftPropertyInstance");
            var rightPropertyInstanceIds = AssertIdentityIds(diffWorkspacePath, "ModelRightPropertyInstance");
            var leftEntityNotInRightIds = AssertIdentityIds(diffWorkspacePath, "ModelLeftEntityInstanceNotInRight");
            var rightEntityNotInLeftIds = AssertIdentityIds(diffWorkspacePath, "ModelRightEntityInstanceNotInLeft");

            _ = alignmentIds;
            _ = entityMapIds;
            _ = propertyMapIds;
            _ = leftEntityNotInRightIds;
            _ = rightEntityNotInLeftIds;

            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeft", "ModelId", modelIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRight", "ModelId", modelIds);
            AssertReferenceValuesExist(diffWorkspacePath, "Alignment", "ModelLeftId", modelLeftIds);
            AssertReferenceValuesExist(diffWorkspacePath, "Alignment", "ModelRightId", modelRightIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftEntity", "ModelLeftId", modelLeftIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightEntity", "ModelRightId", modelRightIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftProperty", "ModelLeftEntityId", modelLeftEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightProperty", "ModelRightEntityId", modelRightEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "EntityMap", "ModelLeftEntityId", modelLeftEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "EntityMap", "ModelRightEntityId", modelRightEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "PropertyMap", "ModelLeftPropertyId", modelLeftPropertyIds);
            AssertReferenceValuesExist(diffWorkspacePath, "PropertyMap", "ModelRightPropertyId", modelRightPropertyIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftEntityInstance", "ModelLeftEntityId", modelLeftEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightEntityInstance", "ModelRightEntityId", modelRightEntityIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftPropertyInstance", "ModelLeftEntityInstanceId", leftRowIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightPropertyInstance", "ModelRightEntityInstanceId", rightRowIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelLeftPropertyInstance", "ModelLeftPropertyId", modelLeftPropertyIds);
            AssertReferenceValuesExist(diffWorkspacePath, "ModelRightPropertyInstance", "ModelRightPropertyId", modelRightPropertyIds);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelLeftEntityInstanceNotInRight",
                "ModelLeftEntityInstanceId",
                leftRowIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelRightEntityInstanceNotInLeft",
                "ModelRightEntityInstanceId",
                rightRowIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelLeftPropertyInstanceNotInRight",
                "ModelLeftPropertyInstanceId",
                leftPropertyInstanceIds,
                allowEmptyRows: true);
            AssertReferenceValuesExist(
                diffWorkspacePath,
                "ModelRightPropertyInstanceNotInLeft",
                "ModelRightPropertyInstanceId",
                rightPropertyInstanceIds,
                allowEmptyRows: true);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task WorkspaceDiff_FailsOnBlankAndDuplicateIds()
    {
        var blankLeftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var blankRightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var duplicateLeftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var duplicateRightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        try
        {
            var blankInstancePath = Path.Combine(blankLeftWorkspace, "metadata", "instance", "Cube.xml");
            var blankInstance = XDocument.Load(blankInstancePath);
            blankInstance.Descendants("Cube").First().SetAttributeValue("Id", string.Empty);
            blankInstance.Save(blankInstancePath);

            var blankDiffResult = await RunCliAsync(
                "instance",
                "diff",
                blankLeftWorkspace,
                blankRightWorkspace);
            Assert.Equal(4, blankDiffResult.ExitCode);
            Assert.Contains("missing valid numeric Id", blankDiffResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);

            var duplicateInstancePath = Path.Combine(duplicateLeftWorkspace, "metadata", "instance", "Cube.xml");
            var duplicateInstance = XDocument.Load(duplicateInstancePath);
            var cubes = duplicateInstance.Descendants("Cube").Take(2).ToList();
            Assert.True(cubes.Count == 2);
            cubes[1].SetAttributeValue("Id", (string?)cubes[0].Attribute("Id") ?? string.Empty);
            duplicateInstance.Save(duplicateInstancePath);

            var duplicateDiffResult = await RunCliAsync(
                "instance",
                "diff",
                duplicateLeftWorkspace,
                duplicateRightWorkspace);
            Assert.Equal(4, duplicateDiffResult.ExitCode);
            Assert.Contains("duplicate Id", duplicateDiffResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(blankLeftWorkspace);
            DeleteDirectorySafe(blankRightWorkspace);
            DeleteDirectorySafe(duplicateLeftWorkspace);
            DeleteDirectorySafe(duplicateRightWorkspace);
        }
    }

    [Fact]
    public async Task WorkspaceMerge_RejectsMultipleSummaryRows()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var insertResult = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Diff Cube",
                "--set",
                "Purpose=Summary-row test",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertResult.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var summaryShardPath = Path.Combine(diffWorkspacePath, "metadata", "instance", "Diff.xml");
            var summaryShard = XDocument.Load(summaryShardPath);
            var summaryRow = summaryShard.Descendants("Diff").Single();
            var duplicateSummaryRow = new XElement(summaryRow);
            duplicateSummaryRow.SetAttributeValue("Id", "2");
            summaryRow.Parent!.Add(duplicateSummaryRow);
            summaryShard.Save(summaryShardPath);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(4, mergeResult.ExitCode);
            Assert.Contains("must contain exactly one 'Diff' row", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceDiff_TreatsMissingAndExplicitEmptyAsDifferent()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        const string propertyName = "MissingVsEmptyProp";
        try
        {
            var addLeftProperty = await RunCliAsync(
                "model",
                "add-property",
                "Cube",
                propertyName,
                "--required",
                "false",
                "--workspace",
                leftWorkspace);
            Assert.Equal(0, addLeftProperty.ExitCode);

            var addRightProperty = await RunCliAsync(
                "model",
                "add-property",
                "Cube",
                propertyName,
                "--required",
                "false",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, addRightProperty.ExitCode);

            var setRightEmpty = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                propertyName + "=",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, setRightEmpty.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var rightNotInLeftPath = Path.Combine(
                diffWorkspacePath,
                "metadata",
                "instance",
                "ModelRightPropertyInstanceNotInLeft.xml");
            var leftNotInRightPath = Path.Combine(
                diffWorkspacePath,
                "metadata",
                "instance",
                "ModelLeftPropertyInstanceNotInRight.xml");
            var rightNotInLeft = XDocument.Load(rightNotInLeftPath);
            var leftNotInRight = XDocument.Load(leftNotInRightPath);
            Assert.NotEmpty(rightNotInLeft.Descendants("ModelRightPropertyInstanceNotInLeft"));
            Assert.Empty(leftNotInRight.Descendants("ModelLeftPropertyInstanceNotInRight"));

            var propertyShardPath = Path.Combine(diffWorkspacePath, "metadata", "instance", "Property.xml");
            var rightPropertyShardPath = Path.Combine(diffWorkspacePath, "metadata", "instance", "ModelRightPropertyInstance.xml");
            var propertyShard = XDocument.Load(propertyShardPath);
            var optionalPropertyId = propertyShard
                .Descendants("Property")
                .Single(element => string.Equals(GetFieldValue(element, "Name"), propertyName, StringComparison.OrdinalIgnoreCase))
                .Attribute("Id")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(optionalPropertyId));

            var rightPropertyShard = XDocument.Load(rightPropertyShardPath);
            Assert.Contains(
                rightPropertyShard.Descendants("ModelRightPropertyInstance"),
                element =>
                    string.Equals(GetFieldValue(element, "PropertyId"), optionalPropertyId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetFieldValue(element, "Value"), string.Empty, StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceMerge_PreservesMissingVsExplicitEmptyInBothDirections()
    {
        var missingWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var emptyWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var reverseMissingWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffToEmptyWorkspace = null;
        string? diffToMissingWorkspace = null;
        const string propertyName = "MissingVsEmptyProp";
        try
        {
            foreach (var workspacePath in new[] { missingWorkspace, emptyWorkspace, reverseMissingWorkspace })
            {
                var addProperty = await RunCliAsync(
                    "model",
                    "add-property",
                    "Cube",
                    propertyName,
                    "--required",
                    "false",
                    "--workspace",
                    workspacePath);
                Assert.Equal(0, addProperty.ExitCode);
            }

            var setEmpty = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                propertyName + "=",
                "--workspace",
                emptyWorkspace);
            Assert.Equal(0, setEmpty.ExitCode);

            var diffToEmpty = await RunCliAsync(
                "instance",
                "diff",
                missingWorkspace,
                emptyWorkspace);
            Assert.Equal(1, diffToEmpty.ExitCode);
            diffToEmptyWorkspace = ExtractDiffWorkspacePath(diffToEmpty.StdOut);

            var mergeToEmpty = await RunCliAsync(
                "instance",
                "merge",
                missingWorkspace,
                diffToEmptyWorkspace);
            Assert.Equal(0, mergeToEmpty.ExitCode);

            var missingCubeShard = XDocument.Load(Path.Combine(missingWorkspace, "metadata", "instance", "Cube.xml"));
            var missingCubeOne = missingCubeShard
                .Descendants("Cube")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "1", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(string.Empty, missingCubeOne.Element(propertyName)?.Value);

            var diffToMissing = await RunCliAsync(
                "instance",
                "diff",
                emptyWorkspace,
                reverseMissingWorkspace);
            Assert.Equal(1, diffToMissing.ExitCode);
            diffToMissingWorkspace = ExtractDiffWorkspacePath(diffToMissing.StdOut);

            var mergeToMissing = await RunCliAsync(
                "instance",
                "merge",
                emptyWorkspace,
                diffToMissingWorkspace);
            Assert.Equal(0, mergeToMissing.ExitCode);

            var emptyCubeShard = XDocument.Load(Path.Combine(emptyWorkspace, "metadata", "instance", "Cube.xml"));
            var emptyCubeOne = emptyCubeShard
                .Descendants("Cube")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "1", StringComparison.OrdinalIgnoreCase));
            Assert.Null(emptyCubeOne.Element(propertyName));
        }
        finally
        {
            DeleteDirectorySafe(missingWorkspace);
            DeleteDirectorySafe(emptyWorkspace);
            DeleteDirectorySafe(reverseMissingWorkspace);
            if (!string.IsNullOrWhiteSpace(diffToEmptyWorkspace))
            {
                DeleteDirectorySafe(diffToEmptyWorkspace);
            }

            if (!string.IsNullOrWhiteSpace(diffToMissingWorkspace))
            {
                DeleteDirectorySafe(diffToMissingWorkspace);
            }
        }
    }

    [Fact]
    public async Task InstanceMerge_RejectsDiffWhenModelRightPropertyInstanceValueIsMissing()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        string? diffWorkspacePath = null;
        try
        {
            var updateRight = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Tampered diff test",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateRight.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var rightPropertyShardPath = Path.Combine(
                diffWorkspacePath,
                "metadata",
                "instance",
                "ModelRightPropertyInstance.xml");
            var rightPropertyShard = XDocument.Load(rightPropertyShardPath);
            var tampered = rightPropertyShard
                .Descendants("ModelRightPropertyInstance")
                .FirstOrDefault(element => element.Element("Value") != null);
            Assert.NotNull(tampered);
            tampered!.Element("Value")!.Remove();
            rightPropertyShard.Save(rightPropertyShardPath);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(4, mergeResult.ExitCode);
            Assert.Contains("missing required value 'Value'", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceMergeAligned_AppliesMappedRightSnapshot()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    ("Purpose", "Purpose"),
                    ("RefreshMode", "RefreshMode"),
                });

            var updateRight = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "2",
                "--set",
                "Purpose=Aligned merge update",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateRight.ExitCode);

            var insertRight = await RunCliAsync(
                "insert",
                "Cube",
                "99",
                "--set",
                "CubeName=Aligned Merge Cube",
                "--set",
                "Purpose=Added by merge-aligned",
                "--set",
                "RefreshMode=Manual",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, insertRight.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge-aligned",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(0, mergeResult.ExitCode);

            var mergedCubeShard = XDocument.Load(Path.Combine(leftWorkspace, "metadata", "instance", "Cube.xml"));
            var updatedCube = mergedCubeShard
                .Descendants("Cube")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "2", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Aligned merge update", GetFieldValue(updatedCube, "Purpose"));

            var insertedCube = mergedCubeShard
                .Descendants("Cube")
                .SingleOrDefault(element => string.Equals((string?)element.Attribute("Id"), "99", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(insertedCube);
            Assert.Equal("Aligned Merge Cube", GetFieldValue(insertedCube!, "CubeName"));
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceMergeAligned_FailsWhenTargetDoesNotMatchLeftSnapshot()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    ("Purpose", "Purpose"),
                });

            var updateRight = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Right snapshot value",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateRight.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var driftTarget = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Target drift",
                "--workspace",
                leftWorkspace);
            Assert.Equal(0, driftTarget.ExitCode);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge-aligned",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(1, mergeResult.ExitCode);
            Assert.Contains("precondition failed", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceMergeAligned_RejectsMalformedDiffWorkspace()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    ("Purpose", "Purpose"),
                });

            var updateRight = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Tampered aligned diff",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateRight.ExitCode);

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var alignmentShardPath = Path.Combine(diffWorkspacePath, "metadata", "instance", "Alignment.xml");
            var alignmentShard = XDocument.Load(alignmentShardPath);
            var alignmentRow = alignmentShard.Descendants("Alignment").Single();
            alignmentRow.Attribute("ModelRightId")?.Remove();
            alignmentRow.Element("ModelRightId")?.Remove();
            alignmentShard.Save(alignmentShardPath);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge-aligned",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(4, mergeResult.ExitCode);
            Assert.Contains("ModelRightId", mergeResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceDiff_RepeatedRuns_AreByteDeterministic()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var firstSnapshot = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        try
        {
            var updateRight = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                "Purpose=Deterministic diff content",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, updateRight.ExitCode);

            var firstDiff = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, firstDiff.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(firstDiff.StdOut);
            CopyDirectory(diffWorkspacePath, firstSnapshot);

            var secondDiff = await RunCliAsync(
                "instance",
                "diff",
                leftWorkspace,
                rightWorkspace);
            Assert.Equal(1, secondDiff.ExitCode);

            AssertDirectoryBytesEqual(firstSnapshot, diffWorkspacePath);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(firstSnapshot);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    [Fact]
    public async Task InstanceDiffAligned_RejectsIdPropertyMappings()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("Id", "Id"),
                    ("CubeName", "CubeName"),
                });

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(4, diffResult.ExitCode);
            Assert.Contains("missing aligned property 'Id'", diffResult.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
        }
    }

    [Fact]
    public async Task InstanceMergeAligned_PreservesMissingVsExplicitEmpty()
    {
        var leftWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var rightWorkspace = await CreateTempCanonicalWorkspaceFromSamplesAsync();
        var alignmentWorkspace = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        string? diffWorkspacePath = null;
        const string propertyName = "AlignedMissingVsEmpty";
        try
        {
            foreach (var workspacePath in new[] { leftWorkspace, rightWorkspace })
            {
                var addProperty = await RunCliAsync(
                    "model",
                    "add-property",
                    "Cube",
                    propertyName,
                    "--required",
                    "false",
                    "--workspace",
                    workspacePath);
                Assert.Equal(0, addProperty.ExitCode);
            }

            var setRightEmpty = await RunCliAsync(
                "instance",
                "update",
                "Cube",
                "1",
                "--set",
                propertyName + "=",
                "--workspace",
                rightWorkspace);
            Assert.Equal(0, setRightEmpty.ExitCode);

            await CreateAlignmentWorkspaceAsync(
                alignmentWorkspace,
                modelLeftName: "EnterpriseBIPlatform",
                modelRightName: "EnterpriseBIPlatform",
                leftEntityName: "Cube",
                rightEntityName: "Cube",
                propertyMappings: new (string Left, string Right)[]
                {
                    ("CubeName", "CubeName"),
                    (propertyName, propertyName),
                });

            var diffResult = await RunCliAsync(
                "instance",
                "diff-aligned",
                leftWorkspace,
                rightWorkspace,
                alignmentWorkspace);
            Assert.Equal(1, diffResult.ExitCode);
            diffWorkspacePath = ExtractDiffWorkspacePath(diffResult.StdOut);

            var mergeResult = await RunCliAsync(
                "instance",
                "merge-aligned",
                leftWorkspace,
                diffWorkspacePath);
            Assert.Equal(0, mergeResult.ExitCode);

            var cubeShard = XDocument.Load(Path.Combine(leftWorkspace, "metadata", "instance", "Cube.xml"));
            var cubeOne = cubeShard
                .Descendants("Cube")
                .Single(element => string.Equals((string?)element.Attribute("Id"), "1", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(cubeOne.Element(propertyName));
            Assert.Equal(string.Empty, cubeOne.Element(propertyName)!.Value);
        }
        finally
        {
            DeleteDirectorySafe(leftWorkspace);
            DeleteDirectorySafe(rightWorkspace);
            DeleteDirectorySafe(alignmentWorkspace);
            if (!string.IsNullOrWhiteSpace(diffWorkspacePath))
            {
                DeleteDirectorySafe(diffWorkspacePath);
            }
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr, string CombinedOutput)> RunCliAsync(
        params string[] arguments)
    {
        var repoRoot = FindRepositoryRoot();
        var cliPath = await EnsureCliAssemblyAsync(repoRoot).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        };

        startInfo.ArgumentList.Add(cliPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);
        return (process.ExitCode, stdOut, stdErr, stdOut + Environment.NewLine + stdErr);
    }

    private static async Task<string> EnsureCliAssemblyAsync(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(cliAssemblyPath) && File.Exists(cliAssemblyPath))
        {
            return cliAssemblyPath;
        }

        await CliBuildGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(cliAssemblyPath) && File.Exists(cliAssemblyPath))
            {
                return cliAssemblyPath;
            }

            var candidate = Path.Combine(repoRoot, "Meta.Cli", "bin", "Debug", "net9.0", "meta.dll");
            if (!File.Exists(candidate))
            {
                var cliProject = Path.Combine(repoRoot, "Meta.Cli", "Meta.Cli.csproj");
                var buildInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = repoRoot,
                };
                buildInfo.ArgumentList.Add("build");
                buildInfo.ArgumentList.Add(cliProject);
                buildInfo.ArgumentList.Add("-c");
                buildInfo.ArgumentList.Add("Debug");
                buildInfo.ArgumentList.Add("-v");
                buildInfo.ArgumentList.Add("quiet");
                buildInfo.ArgumentList.Add("--nologo");

                using var buildProcess = new Process { StartInfo = buildInfo };
                buildProcess.Start();
                var buildStdOutTask = buildProcess.StandardOutput.ReadToEndAsync();
                var buildStdErrTask = buildProcess.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await buildProcess.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                var buildStdOut = await buildStdOutTask.ConfigureAwait(false);
                var buildStdErr = await buildStdErrTask.ConfigureAwait(false);

                if (buildProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Failed to build Meta.Cli for tests." +
                        Environment.NewLine +
                        buildStdOut +
                        Environment.NewLine +
                        buildStdErr);
                }
            }

            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException($"Could not find compiled CLI assembly at '{candidate}'.");
            }

            cliAssemblyPath = candidate;
            return candidate;
        }
        finally
        {
            CliBuildGate.Release();
        }
    }

    private static HashSet<string> AssertIdentityIds(string workspacePath, string entityName)
    {
        var rows = LoadEntityRows(workspacePath, entityName);
        var parsedIds = rows
            .Select(row =>
            {
                var idText = (string?)row.Attribute("Id");
                Assert.False(string.IsNullOrWhiteSpace(idText), $"{entityName} row is missing Id.");
                Assert.True(int.TryParse(idText, out _), $"{entityName} row Id '{idText}' is not numeric.");
                return int.Parse(idText!, System.Globalization.CultureInfo.InvariantCulture);
            })
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, parsedIds.Length), parsedIds);
        return rows
            .Select(row => (string)row.Attribute("Id")!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertReferenceValuesExist(
        string workspacePath,
        string entityName,
        string referenceAttributeName,
        IReadOnlySet<string> targetIds,
        bool allowEmptyRows = false)
    {
        var rows = LoadEntityRows(workspacePath, entityName);
        if (!rows.Any())
        {
            Assert.True(allowEmptyRows, $"{entityName} has no rows but rows were expected.");
            return;
        }

        foreach (var row in rows)
        {
            var reference = GetFieldValue(row, referenceAttributeName);
            Assert.False(
                string.IsNullOrWhiteSpace(reference),
                $"{entityName} row '{(string?)row.Attribute("Id")}' is missing '{referenceAttributeName}'.");
            Assert.Contains(reference!, targetIds);
        }
    }

    private static string? GetFieldValue(XElement row, string fieldName)
    {
        var attributeValue = (string?)row.Attribute(fieldName);
        if (!string.IsNullOrWhiteSpace(attributeValue) || row.Attribute(fieldName) != null)
        {
            return attributeValue;
        }

        var element = row.Element(fieldName);
        return element?.Value;
    }

    private static IReadOnlyList<XElement> LoadEntityRows(string workspacePath, string entityName)
    {
        var shardPath = Path.Combine(workspacePath, "metadata", "instance", entityName + ".xml");
        Assert.True(File.Exists(shardPath), $"Expected shard '{shardPath}' to exist.");
        var document = XDocument.Load(shardPath);
        return document.Descendants(entityName).ToList();
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, recursive: true);
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file);
            var destinationFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static void AssertDirectoryBytesEqual(string expectedPath, string actualPath)
    {
        var expectedFiles = Directory.GetFiles(expectedPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(expectedPath, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualFiles = Directory.GetFiles(actualPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(actualPath, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(expectedFiles, actualFiles);

        foreach (var relativePath in expectedFiles)
        {
            var expectedBytes = File.ReadAllBytes(Path.Combine(expectedPath, relativePath));
            var actualBytes = File.ReadAllBytes(Path.Combine(actualPath, relativePath));
            Assert.True(
                expectedBytes.AsSpan().SequenceEqual(actualBytes),
                $"File '{relativePath}' differs between expected and actual directories.");
        }
    }

    private static async Task CreateAlignmentWorkspaceAsync(
        string workspaceRoot,
        string modelLeftName,
        string modelRightName,
        string leftEntityName,
        string rightEntityName,
        IReadOnlyList<(string Left, string Right)> propertyMappings)
    {
        if (propertyMappings.Count == 0)
        {
            throw new InvalidOperationException("Alignment test helper requires at least one property mapping.");
        }

        var repoRoot = FindRepositoryRoot();
        var metadataRoot = Path.Combine(workspaceRoot, "metadata");
        var instanceRoot = Path.Combine(metadataRoot, "instance");
        Directory.CreateDirectory(instanceRoot);
        File.Copy(
            Path.Combine(repoRoot, "Meta.Cli", "Templates", "InstanceDiffModel.Alignment.xml"),
            Path.Combine(metadataRoot, "model.xml"),
            overwrite: true);

        var services = new ServiceCollection();
        var workspace = await services.WorkspaceService
            .LoadAsync(workspaceRoot, searchUpward: false)
            .ConfigureAwait(false);
        workspace.WorkspaceRootPath = workspaceRoot;
        workspace.MetadataRootPath = metadataRoot;
        workspace.IsDirty = true;
        workspace.Instance.ModelName = workspace.Model.Name;

        AddRow("Model", "1", ("Name", modelLeftName));
        AddRow("Model", "2", ("Name", modelRightName));
        AddRow("ModelLeft", "1", ("ModelId", "1"));
        AddRow("ModelRight", "1", ("ModelId", "2"));
        AddRow(
            "Alignment",
            "1",
            ("Name", "TestAlignment"),
            ("ModelLeftId", "1"),
            ("ModelRightId", "1"));

        AddRow(
            "ModelLeftEntity",
            "1",
            ("Name", leftEntityName),
            ("ModelLeftId", "1"));
        AddRow(
            "ModelRightEntity",
            "1",
            ("Name", rightEntityName),
            ("ModelRightId", "1"));
        AddRow(
            "EntityMap",
            "1",
            ("ModelLeftEntityId", "1"),
            ("ModelRightEntityId", "1"));

        for (var index = 0; index < propertyMappings.Count; index++)
        {
            var ordinal = (index + 1).ToString();
            var leftPropertyId = ordinal;
            var rightPropertyId = ordinal;
            AddRow(
                "ModelLeftProperty",
                leftPropertyId,
                ("Name", propertyMappings[index].Left),
                ("ModelLeftEntityId", "1"));
            AddRow(
                "ModelRightProperty",
                rightPropertyId,
                ("Name", propertyMappings[index].Right),
                ("ModelRightEntityId", "1"));
            AddRow(
                "PropertyMap",
                ordinal,
                ("ModelLeftPropertyId", leftPropertyId),
                ("ModelRightPropertyId", rightPropertyId));
        }

        await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);

        void AddRow(
            string entityName,
            string id,
            params (string Key, string Value)[] values)
        {
            var entity = workspace.Model.FindEntity(entityName)
                         ?? throw new InvalidOperationException($"Alignment helper is missing entity '{entityName}'.");
            var propertyNames = entity.Properties
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var relationshipNames = entity.Relationships
                .Select(item => item.Entity)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var row = new InstanceRecord
            {
                Id = id,
            };
            foreach (var (key, value) in values)
            {
                if (key.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                {
                    var relationshipName = key[..^2];
                    if (relationshipNames.Contains(relationshipName))
                    {
                        row.RelationshipIds[relationshipName] = value;
                        continue;
                    }
                }

                if (propertyNames.Contains(key))
                {
                    row.Values[key] = value;
                    continue;
                }

                throw new InvalidOperationException(
                    $"Alignment helper cannot map field '{key}' for entity '{entityName}'.");
            }

            workspace.Instance.GetOrCreateEntityRecords(entityName).Add(row);
        }
    }

    private static string CreateTempWorkspaceFromSamples()
    {
        var root = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var repoRoot = FindRepositoryRoot();
        var services = new ServiceCollection();
        var workspace = services.ImportService
            .ImportXmlAsync(
                Path.Combine(repoRoot, "Samples", "SampleModel.xml"),
                Path.Combine(repoRoot, "Samples", "SampleInstance.xml"))
            .GetAwaiter()
            .GetResult();
        workspace.WorkspaceRootPath = root;
        workspace.MetadataRootPath = string.Empty;
        workspace.IsDirty = true;
        services.WorkspaceService
            .SaveAsync(workspace)
            .GetAwaiter()
            .GetResult();

        return root;
    }

    private static async Task<string> CreateTempCanonicalWorkspaceFromSamplesAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "metadata-studio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var repoRoot = FindRepositoryRoot();
        var services = new ServiceCollection();
        var workspace = await services.ImportService
            .ImportXmlAsync(
                Path.Combine(repoRoot, "Samples", "SampleModel.xml"),
                Path.Combine(repoRoot, "Samples", "SampleInstance.xml"))
            .ConfigureAwait(false);
        workspace.WorkspaceRootPath = root;
        workspace.MetadataRootPath = string.Empty;
        workspace.IsDirty = true;
        await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);
        return root;
    }

    private static void DeleteDirectorySafe(string path)
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

    private static string ExtractDiffWorkspacePath(string output)
    {
        var diffPathMatch = Regex.Match(output, @"(?m)^DiffWorkspace:\s*(.+)$");
        Assert.True(diffPathMatch.Success, $"Expected diff workspace path in output:{Environment.NewLine}{output}");
        return diffPathMatch.Groups[1].Value.Trim();
    }
}


