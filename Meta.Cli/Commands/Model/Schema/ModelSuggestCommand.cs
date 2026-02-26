internal sealed partial class CliRuntime
{
    async Task<int> ModelSuggestAsync(string[] commandArgs)
    {
        if (globalJson)
        {
            var previousJson = globalJson;
            globalJson = false;
            try
            {
                return PrintArgumentError("--json is not supported for 'meta model suggest'.");
            }
            finally
            {
                globalJson = previousJson;
            }
        }

        if (commandArgs.Length >= 3 && !commandArgs[2].StartsWith("--", StringComparison.Ordinal))
        {
            var mode = commandArgs[2].Trim().ToLowerInvariant();
            return PrintCommandUnknownError($"model suggest {mode}");
        }

        var options = ParseModelSuggestOptions(commandArgs, startIndex: 2);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }

        var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
        PrintContractCompatibilityWarning(workspace.WorkspaceConfig);
        var report = ModelSuggestService.Analyze(workspace);

        PrintModelSuggestReport(report, options.ShowKeys, options.ShowBlocked, options.Explain);
        return 0;
    }

    (bool Ok, string WorkspacePath, bool ShowKeys, bool ShowBlocked, bool Explain, string ErrorMessage)
        ParseModelSuggestOptions(string[] commandArgs, int startIndex)
    {
        var workspacePath = DefaultWorkspacePath();
        var showKeys = false;
        var showBlocked = false;
        var explain = false;

        for (var i = startIndex; i < commandArgs.Length; i++)
        {
            var arg = commandArgs[i];
            if (string.Equals(arg, "--workspace", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, workspacePath, showKeys, showBlocked, explain, "Error: --workspace requires a path.");
                }

                workspacePath = commandArgs[++i];
                continue;
            }

            if (string.Equals(arg, "--show-keys", StringComparison.OrdinalIgnoreCase))
            {
                showKeys = true;
                continue;
            }

            if (string.Equals(arg, "--show-blocked", StringComparison.OrdinalIgnoreCase))
            {
                showBlocked = true;
                continue;
            }

            if (string.Equals(arg, "--explain", StringComparison.OrdinalIgnoreCase))
            {
                explain = true;
                continue;
            }

            return (false, workspacePath, showKeys, showBlocked, explain, $"Error: unknown option '{arg}'.");
        }

        return (true, workspacePath, showKeys, showBlocked, explain, string.Empty);
    }

    void PrintModelSuggestReport(ModelSuggestReport report, bool showKeys, bool showBlocked, bool explain)
    {
        presenter.WriteInfo("meta model suggest");
        presenter.WriteInfo($"Workspace: {report.WorkspaceRootPath}");
        presenter.WriteInfo($"Model: {report.ModelName}");
        presenter.WriteInfo(string.Empty);

        presenter.WriteInfo("Relationship suggestions");
        if (report.EligibleRelationshipSuggestions.Count == 0)
        {
            presenter.WriteInfo("  (none)");
        }
        else
        {
            for (var index = 0; index < report.EligibleRelationshipSuggestions.Count; index++)
            {
                var suggestion = report.EligibleRelationshipSuggestions[index];
                PrintEligibleRelationshipSuggestion(suggestion, index + 1, explain);
            }
        }

        if (showKeys)
        {
            PrintKeySection(report.BusinessKeys, explain);
        }

        if (showBlocked)
        {
            PrintBlockedSection(report.BlockedRelationshipCandidates, explain);
        }

        presenter.WriteInfo("Summary");
        presenter.WriteInfo($"  Relationship suggestions: {report.EligibleRelationshipSuggestions.Count.ToString(CultureInfo.InvariantCulture)}");
        if (showKeys)
        {
            presenter.WriteInfo($"  Candidate business keys: {report.BusinessKeys.Count.ToString(CultureInfo.InvariantCulture)}");
        }

        if (showBlocked)
        {
            presenter.WriteInfo($"  Blocked relationship candidates: {report.BlockedRelationshipCandidates.Count.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    void PrintEligibleRelationshipSuggestion(LookupRelationshipSuggestion suggestion, int ordinal, bool explain)
    {
        presenter.WriteInfo($"  Suggestion {ordinal.ToString(CultureInfo.InvariantCulture)}");
        presenter.WriteInfo($"    Source: {suggestion.Source.EntityName}.{suggestion.Source.PropertyName}");
        presenter.WriteInfo(
            $"    Target: {suggestion.TargetLookup.EntityName} (lookup key: {suggestion.TargetLookup.EntityName}.{suggestion.TargetLookup.PropertyName})");
        presenter.WriteInfo("    Proposed refactor:");
        presenter.WriteInfo(
            $"      - Add relationship {suggestion.Source.EntityName} -> {suggestion.TargetLookup.EntityName}");
        presenter.WriteInfo(
            $"      - Rewrite {suggestion.Source.EntityName} rows by resolving {suggestion.Source.PropertyName} against {suggestion.TargetLookup.EntityName}.{suggestion.TargetLookup.PropertyName}");
        presenter.WriteInfo(
            $"      - {suggestion.Source.EntityName}.{suggestion.Source.PropertyName} can be dropped after successful rewrite");

        if (explain)
        {
            presenter.WriteInfo("    Stats:");
            presenter.WriteInfo(
                $"      - Source non-blank rows: {suggestion.SourceComparableRowCount.ToString(CultureInfo.InvariantCulture)} (distinct {suggestion.SourceDistinctComparableValueCount.ToString(CultureInfo.InvariantCulture)})");
            presenter.WriteInfo(
                $"      - Target non-blank rows: {suggestion.TargetComparableRowCount.ToString(CultureInfo.InvariantCulture)} (distinct {suggestion.TargetDistinctComparableValueCount.ToString(CultureInfo.InvariantCulture)})");
            presenter.WriteInfo(
                $"      - Matched source rows: {suggestion.MatchedSourceRowCount.ToString(CultureInfo.InvariantCulture)}/{suggestion.SourceComparableRowCount.ToString(CultureInfo.InvariantCulture)}");

            presenter.WriteInfo("    Evidence:");
            foreach (var reason in suggestion.Evidence)
            {
                presenter.WriteInfo("      - " + reason);
            }

            presenter.WriteInfo("    Why:");
            presenter.WriteInfo("      - Candidate is eligible: strict RI checks passed.");
            presenter.WriteInfo("      - Source values fully resolve to target lookup key.");
        }

        presenter.WriteInfo(string.Empty);
    }

    void PrintKeySection(IReadOnlyList<BusinessKeyCandidate> keys, bool explain)
    {
        presenter.WriteInfo("Candidate business keys");
        if (keys.Count == 0)
        {
            presenter.WriteInfo("  (none)");
            presenter.WriteInfo(string.Empty);
            return;
        }

        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            presenter.WriteInfo($"  Key {(index + 1).ToString(CultureInfo.InvariantCulture)}");
            presenter.WriteInfo($"    Target: {key.Target.EntityName}.{key.Target.PropertyName}");

            if (explain)
            {
                presenter.WriteInfo("    Stats:");
                presenter.WriteInfo(
                    $"      - rows={key.Target.RowCount.ToString(CultureInfo.InvariantCulture)}, non-null={key.Target.NonNullCount.ToString(CultureInfo.InvariantCulture)}, non-blank={key.Target.NonBlankCount.ToString(CultureInfo.InvariantCulture)}, distinct={key.Target.DistinctNonBlankCount.ToString(CultureInfo.InvariantCulture)}, unique={(key.Target.IsUniqueOverNonBlank ? "yes" : "no")}");

                presenter.WriteInfo("    Why:");
                if (key.Reasons.Count == 0)
                {
                    presenter.WriteInfo("      - (none)");
                }
                else
                {
                    foreach (var reason in key.Reasons)
                    {
                        presenter.WriteInfo("      - " + reason);
                    }
                }

                presenter.WriteInfo("    Blockers:");
                if (key.Blockers.Count == 0)
                {
                    presenter.WriteInfo("      - (none)");
                }
                else
                {
                    foreach (var blocker in key.Blockers)
                    {
                        presenter.WriteInfo("      - " + blocker);
                    }
                }
            }

            presenter.WriteInfo(string.Empty);
        }
    }

    void PrintBlockedSection(IReadOnlyList<LookupRelationshipSuggestion> blockedCandidates, bool explain)
    {
        presenter.WriteInfo("Blocked relationship candidates");
        if (blockedCandidates.Count == 0)
        {
            presenter.WriteInfo("  (none)");
            presenter.WriteInfo(string.Empty);
            return;
        }

        for (var index = 0; index < blockedCandidates.Count; index++)
        {
            var suggestion = blockedCandidates[index];
            presenter.WriteInfo($"  Candidate {(index + 1).ToString(CultureInfo.InvariantCulture)}");
            presenter.WriteInfo($"    Source: {suggestion.Source.EntityName}.{suggestion.Source.PropertyName}");
            presenter.WriteInfo(
                $"    Target: {suggestion.TargetLookup.EntityName} (lookup key: {suggestion.TargetLookup.EntityName}.{suggestion.TargetLookup.PropertyName})");

            presenter.WriteInfo("    Blockers:");
            foreach (var blocker in suggestion.Blockers)
            {
                presenter.WriteInfo("      - " + blocker);
            }

            if (explain)
            {
                presenter.WriteInfo("    Stats:");
                presenter.WriteInfo(
                    $"      - Source non-blank rows: {suggestion.SourceComparableRowCount.ToString(CultureInfo.InvariantCulture)} (distinct {suggestion.SourceDistinctComparableValueCount.ToString(CultureInfo.InvariantCulture)})");
                presenter.WriteInfo(
                    $"      - Target non-blank rows: {suggestion.TargetComparableRowCount.ToString(CultureInfo.InvariantCulture)} (distinct {suggestion.TargetDistinctComparableValueCount.ToString(CultureInfo.InvariantCulture)})");
                presenter.WriteInfo(
                    $"      - Matched source rows: {suggestion.MatchedSourceRowCount.ToString(CultureInfo.InvariantCulture)}/{suggestion.SourceComparableRowCount.ToString(CultureInfo.InvariantCulture)}");

                if (suggestion.UnmatchedDistinctValueCount > 0)
                {
                    presenter.WriteInfo(
                        $"      - Unmatched value sample: {string.Join(", ", suggestion.UnmatchedDistinctValuesSample)}");
                }

                presenter.WriteInfo("    Evidence:");
                foreach (var reason in suggestion.Evidence)
                {
                    presenter.WriteInfo("      - " + reason);
                }
            }

            presenter.WriteInfo(string.Empty);
        }
    }
}
