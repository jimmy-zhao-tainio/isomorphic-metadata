internal sealed partial class CliRuntime
{
    string BuildUsageHintForCurrentArgs()
    {
        if (args.Length == 0)
        {
            return string.Empty;
        }
    
        var command = args[0].Trim().ToLowerInvariant();
        return command switch
        {
            "help" => "meta help [<command> ...]",
            "init" => "meta init [<path>]",
            "status" => "meta status [--workspace <path>]",
            "instance" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "diff" => "meta instance diff <leftWorkspace> <rightWorkspace>",
                    "merge" => "meta instance merge <targetWorkspace> <diffWorkspace>",
                    "diff-aligned" => "meta instance diff-aligned <leftWorkspace> <rightWorkspace> <alignmentWorkspace>",
                    "merge-aligned" => "meta instance merge-aligned <targetWorkspace> <diffWorkspace>",
                    "update" => "meta instance update <Entity> <Id> --set Field=Value [--set Field=Value ...] [--workspace <path>]",
                    "relationship" when args.Length >= 3 => args[2].Trim().ToLowerInvariant() switch
                    {
                        "set" => "meta instance relationship set <FromEntity> <FromId> --to <ToEntity> <ToId> [--workspace <path>]",
                        "list" => "meta instance relationship list <FromEntity> <FromId> [--workspace <path>]",
                        _ => "meta instance relationship <set|list> ...",
                    },
                    _ => "meta instance <diff|merge|diff-aligned|merge-aligned|update|relationship> ...",
                }
                : "meta instance <diff|merge|diff-aligned|merge-aligned|update|relationship> ...",
            "list" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "entities" => "meta list entities [--workspace <path>]",
                    "properties" => "meta list properties <Entity> [--workspace <path>]",
                    "relationships" => "meta list relationships <Entity> [--workspace <path>]",
                    "tasks" => "meta list tasks [--workspace <path>]",
                    _ => "meta list <entities|properties|relationships|tasks> [--workspace <path>]",
                }
                : "meta list <entities|properties|relationships|tasks> [--workspace <path>]",
            "check" => "meta check [--workspace <path>]",
            "view" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "entity" => "meta view entity <Entity> [--workspace <path>]",
                    "instance" => "meta view instance <Entity> <Id> [--workspace <path>]",
                    _ => "meta view <entity|instance> ...",
                }
                : "meta view <entity|instance> ...",
            "query" => "meta query <Entity> [--equals <Field> <Value>]... [--contains <Field> <Value>]... [--top <n>] [--workspace <path>]",
            "graph" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "stats" => "meta graph stats [--workspace <path>] [--top <n>] [--cycles <n>]",
                    "inbound" => "meta graph inbound <Entity> [--workspace <path>] [--top <n>]",
                    _ => "meta graph <stats|inbound> ...",
                }
                : "meta graph <stats|inbound> ...",
            "model" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "add-entity" => "meta model add-entity <Name> [--workspace <path>]",
                    "rename-entity" => "meta model rename-entity <Old> <New> [--workspace <path>]",
                    "add-property" => "meta model add-property <Entity> <Property> [--required true|false] [--default-value <Value>] [--workspace <path>]",
                    "rename-property" => "meta model rename-property <Entity> <Old> <New> [--workspace <path>]",
                    "add-relationship" => "meta model add-relationship <FromEntity> <ToEntity> [--role <RoleName>] [--default-id <ToId>] [--workspace <path>]",
                    "drop-property" => "meta model drop-property <Entity> <Property> [--workspace <path>]",
                    "drop-relationship" => "meta model drop-relationship <FromEntity> <ToEntity> [--workspace <path>]",
                    "drop-entity" => "meta model drop-entity <Entity> [--workspace <path>]",
                    "suggest" => "meta model suggest [--show-keys] [--show-blocked] [--explain] [--workspace <path>]",
                    _ => "meta model <subcommand> [arguments] [--workspace <path>]",
                }
                : "meta model <subcommand> [arguments] [--workspace <path>]",
            "insert" => "meta insert <Entity> [<Id>|--auto-id] --set Field=Value [--set Field=Value ...] [--workspace <path>]",
            "bulk-insert" => "meta bulk-insert <Entity> [--from tsv|csv|jsonl] [--file <path>|--stdin] [--key Field[,Field2...]] [--auto-id] [--workspace <path>]",
            "delete" => "meta delete <Entity> <Id> [--workspace <path>]",
            "generate" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "sql" => "meta generate sql --out <dir> [--workspace <path>]",
                    "csharp" => "meta generate csharp --out <dir> [--workspace <path>] [--tooling]",
                    "ssdt" => "meta generate ssdt --out <dir> [--workspace <path>]",
                    _ => "meta generate <sql|csharp|ssdt> --out <dir> [--workspace <path>]",
                }
                : "meta generate <sql|csharp|ssdt> --out <dir> [--workspace <path>]",
            "import" => args.Length >= 2
                ? args[1].Trim().ToLowerInvariant() switch
                {
                    "xml" => "meta import xml <modelXmlPath> <instanceXmlPath> --new-workspace <path>",
                    "sql" => "meta import sql <connectionString> <schema> --new-workspace <path>",
                    "csv" => "meta import csv <csvFile> --entity <EntityName> (--workspace <path> | --new-workspace <path>)",
                    _ => "meta import <xml|sql|csv> ...",
                }
                : "meta import <xml|sql|csv> ...",
            _ => string.Empty,
        };
    }

    string NormalizeUsageSyntax(string usage)
    {
        if (string.IsNullOrWhiteSpace(usage))
        {
            return string.Empty;
        }
    
        var trimmed = usage.Trim();
        const string Prefix = "Usage:";
        if (trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[Prefix.Length..].Trim();
        }
    
        if (!trimmed.StartsWith("meta ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "meta " + trimmed;
        }
    
        return trimmed;
    }

    string BuildNextHelpHintFromUsage(string usage)
    {
        var normalizedUsage = NormalizeUsageSyntax(usage);
        if (string.IsNullOrWhiteSpace(normalizedUsage))
        {
            return "meta help";
        }
    
        var tokens = normalizedUsage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= 1 || !string.Equals(tokens[0], "meta", StringComparison.OrdinalIgnoreCase))
        {
            return "meta help";
        }
    
        var topic = new List<string>();
        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("<", StringComparison.Ordinal) ||
                token.StartsWith("[", StringComparison.Ordinal) ||
                token.StartsWith("--", StringComparison.Ordinal) ||
                token.Contains('|', StringComparison.Ordinal))
            {
                break;
            }
    
            topic.Add(token);
        }
    
        if (topic.Count == 0)
        {
            return "meta help";
        }
    
        if (topic.Count == 1 && string.Equals(topic[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            return "meta help";
        }
    
        return $"meta {string.Join(" ", topic)} help";
    }

    string BuildNextHelpHintForCurrentArgs()
    {
        var usage = BuildUsageHintForCurrentArgs();
        return BuildNextHelpHintFromUsage(usage);
    }

    bool IsHelpToken(string value)
    {
        return string.Equals(value, "help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
    }

    bool TryHandleHelpRequest(string[] commandArgs, out int exitCode)
    {
        exitCode = 0;
        if (commandArgs.Length == 0)
        {
            return false;
        }
    
        if (IsHelpToken(commandArgs[0]))
        {
            exitCode = PrintHelpForTopic(commandArgs.Skip(1).ToArray());
            return true;
        }
    
        var helpIndex = Array.FindIndex(commandArgs, IsHelpToken);
        if (helpIndex > 0)
        {
            exitCode = PrintHelpForTopic(commandArgs.Take(helpIndex).ToArray());
            return true;
        }
    
        return false;
    }

    int PrintHelpForTopic(string[] topicTokens)
    {
        if (topicTokens == null || topicTokens.Length == 0)
        {
            PrintUsage();
            return 0;
        }
    
        var normalizedTokens = topicTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToArray();
        if (normalizedTokens.Length == 0)
        {
            PrintUsage();
            return 0;
        }
    
        var key = string.Join(" ", normalizedTokens).ToLowerInvariant();
        if (string.Equals(key, "help", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 0;
        }

        if (HelpTopics.TryBuildHelpTopic(key, out var topicDocument))
        {
            RenderHelpDocument(WithRuntimeHeader(topicDocument));
            return 0;
        }
    
        var suggestionCandidates = HelpTopics.GetCommandSuggestions();
        var suggestions = SuggestValues(normalizedTokens[0], suggestionCandidates);
    
        var hints = new List<string>();
        if (suggestions.Count > 0)
        {
            hints.Add("Did you mean: " + string.Join(", ", suggestions.Take(3)));
        }
    
        hints.Add("Usage: meta help [<command> ...]");
        hints.Add("Next: meta help");
    
        return PrintFormattedError(
            "E_USAGE",
            $"unknown help topic '{string.Join(" ", normalizedTokens)}'.",
            exitCode: 1,
            hints: hints);
    }

    void PrintUsage()
    {
        var sections = HelpTopics.GetCommandCatalogByDomain()
            .Select(item => (Title: NormalizeHelpDomainTitle(item.Domain), item.Commands))
            .Select(item => new HelpSection($"{item.Title}:", item.Commands))
            .ToArray();

        RenderHelpDocument(new HelpDocument(
            Header: new HelpHeader(
                "Meta CLI",
                TryGetCliVersion(),
                "Workspace is discovered from current directory; use --workspace to override."),
            Usage: "meta <command> [options]",
            OptionsTitle: "Global options:",
            Options: new[]
            {
                ("--workspace <path>", "Override workspace root."),
                ("--json", "Return structured JSON output."),
                ("--strict", "Treat warnings as errors for mutating commands."),
            },
            Sections: sections,
            Examples: new[]
            {
                "meta status",
                "meta model add-entity SourceSystem",
                "meta insert Cube 10 --set \"CubeName=Ops Cube\"",
            },
            Next: "meta <command> help"));
    }

    void RenderHelpDocument(HelpDocument document)
    {
        WriteHelpHeader(document.Header);

        presenter.WriteInfo(string.Empty);
        presenter.WriteUsage(document.Usage);

        presenter.WriteInfo(string.Empty);
        presenter.WriteOptionCatalog(document.Options, document.OptionsTitle);

        if (document.Sections is { Count: > 0 })
        {
            presenter.WriteInfo(string.Empty);
            for (var i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
                presenter.WriteCommandCatalog(section.Title, section.Entries);
                if (i < document.Sections.Count - 1)
                {
                    presenter.WriteInfo(string.Empty);
                }
            }
        }

        if (document.Examples is { Count: > 0 })
        {
            presenter.WriteInfo(string.Empty);
            presenter.WriteExamples(document.Examples);
        }

        presenter.WriteInfo(string.Empty);
        presenter.WriteNext(NormalizeNextHelpHint(document.Next));
    }

    HelpDocument WithRuntimeHeader(HelpDocument document)
    {
        var header = document.Header;
        var version = string.IsNullOrWhiteSpace(header.Version)
            ? TryGetCliVersion()
            : header.Version;

        var product = string.IsNullOrWhiteSpace(header.Product)
            ? "Meta CLI"
            : header.Product;

        return document with
        {
            Header = header with
            {
                Product = product,
                Version = version,
            },
        };
    }
    static string NormalizeNextHelpHint(string next)
    {
        var trimmed = next?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "meta help";
        }

        if (trimmed.EndsWith(" --help", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^7] + " help";
        }

        return trimmed;
    }

    static string NormalizeHelpDomainTitle(string domain)
    {
        return domain.Trim().ToLowerInvariant() switch
        {
            "workspace" => "Workspace",
            "model" => "Model",
            "instance" => "Instance",
            "pipeline" => "Pipeline",
            "inspect" => "Model",
            "modify" => "Instance",
            "generate" => "Pipeline",
            "utility" => "Utility",
            _ => string.IsNullOrWhiteSpace(domain) ? "Other" : domain.Trim(),
        };
    }

    int PrintCommandUnknownError(string command)
    {
        var suggestionCandidates = HelpTopics.GetCommandSuggestions();
        var suggestions = SuggestValues(command, suggestionCandidates);
        var hints = new List<string>();
        if (suggestions.Count > 0)
        {
            hints.Add("Did you mean: " + string.Join(", ", suggestions.Take(3)));
        }
    
        hints.Add("Next: meta help");
    
        return PrintFormattedError(
            "E_COMMAND_UNKNOWN",
            $"Unknown command '{command}'.",
            exitCode: 1,
            hints: hints);
    }

    void WriteHelpHeader(HelpHeader header)
    {
        if (!string.IsNullOrWhiteSpace(header.Note))
        {
            presenter.WriteInfo(header.Note);
        }
    }

    static string TryGetCliVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            if (version == null)
            {
                return string.Empty;
            }

            return version.Revision > 0
                ? version.ToString(3)
                : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
