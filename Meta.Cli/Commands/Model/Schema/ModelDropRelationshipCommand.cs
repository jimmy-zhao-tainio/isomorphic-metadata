internal sealed partial class CliRuntime
{
    async Task<int> ModelDropRelationshipAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 4)
        {
            return PrintUsageError(
                "Usage: model drop-relationship <FromEntity> <ToEntity> [--workspace <path>]");
        }
    
        var fromEntityName = commandArgs[2];
        var toEntityName = commandArgs[3];
        var options = ParseMutatingCommonOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var fromEntity = RequireEntity(workspace, fromEntityName);
            var relationship = ResolveRelationshipDefinition(fromEntity, toEntityName, out var isAmbiguous);
            if (isAmbiguous)
            {
                return PrintDataError(
                    "E_RELATIONSHIP_AMBIGUOUS",
                    $"Relationship selector '{toEntityName}' is ambiguous on entity '{fromEntityName}'. Use relationship name.");
            }

            if (relationship == null)
            {
                return PrintDataError(
                    "E_RELATIONSHIP_NOT_FOUND",
                    $"Relationship '{fromEntityName}->{toEntityName}' does not exist.");
            }

            var relationshipName = relationship.GetName();
            var targetEntityName = relationship.Entity;

            var blockers = workspace.Instance.GetOrCreateEntityRecords(fromEntityName)
                .Where(row => CountRelationshipUsages(row, relationshipName) > 0)
                .Select(row => new
                {
                    Row = row,
                    RowAddress = BuildEntityInstanceAddress(fromEntityName, row.Id),
                    Display = TryGetDisplayValue(fromEntity, row),
                })
                .OrderBy(item => string.IsNullOrWhiteSpace(item.Display) ? 1 : 0)
                .ThenBy(item => item.Display, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Row.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (blockers.Count > 0)
            {
                var firstRowId = blockers[0].Row.Id;
                return PrintFormattedErrorWithTable(
                    code: "E_RELATIONSHIP_IN_USE",
                    message: $"Relationship '{fromEntityName}->{targetEntityName}' is in use.",
                    exitCode: 4,
                    where: new[]
                    {
                        ("fromEntity", fromEntityName),
                        ("relationship", relationshipName),
                        ("toEntity", targetEntityName),
                        ("occurrences", blockers.Count.ToString(CultureInfo.InvariantCulture)),
                    },
                    hints: new[]
                    {
                        $"Relationship usage exists in {blockers.Count.ToString(CultureInfo.InvariantCulture)} instance(s).",
                        $"Next: meta instance relationship set {fromEntityName} {QuoteInstanceId(firstRowId)} --to {relationshipName} <ToId>",
                    },
                    tableTitle: "Relationship usage blockers",
                    headers: new[] { "Entity", "Instance" },
                    rows: blockers
                        .Take(20)
                        .Select(item => (IReadOnlyList<string>)new[]
                        {
                            fromEntityName,
                            item.RowAddress,
                        })
                        .ToList());
            }
    
            var operation = new WorkspaceOp
            {
                Type = WorkspaceOpTypes.DeleteRelationship,
                EntityName = fromEntityName,
                RelatedEntity = relationshipName,
            };
            return await ExecuteOperationAsync(
                    options.WorkspacePath,
                    operation,
                    "model drop-relationship",
                    "relationship removed",
                    ("From", fromEntityName),
                    ("To", targetEntityName),
                    ("Name", relationshipName))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}

