internal sealed partial class CliRuntime
{
    async Task<int> RowRelationshipListAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 5)
        {
            return PrintUsageError("Usage: row relationship list <FromEntity> <FromId> [--workspace <path>]");
        }
    
        var fromEntityName = commandArgs[3];
        var fromId = commandArgs[4];
        if (ContainsLegacyRowReferenceSyntax(fromId))
        {
            return PrintArgumentError($"Error: unsupported row reference '{fromId}'. Use <Entity> <Id>.");
        }
        var options = ParseWorkspaceOnlyOptions(commandArgs, startIndex: 5);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            RequireEntity(workspace, fromEntityName);
            var row = ResolveRowById(workspace, fromEntityName, fromId);
            var relationshipRows = row.RelationshipIds
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    ToEntity = item.Key,
                    ToRow = BuildEntityRowAddress(item.Key, item.Value),
                })
                .ToList();
    
            if (globalJson)
            {
                WriteJson(new
                {
                    command = "row.relationship.list",
                    fromRow = BuildEntityRowAddress(fromEntityName, row.Id),
                    count = relationshipRows.Count,
                    relationships = relationshipRows,
                });
                return 0;
            }
    
            if (relationshipRows.Count == 0)
            {
                presenter.WriteOk("no relationship usage", ("Row", BuildEntityRowAddress(fromEntityName, row.Id)));
                return 0;
            }
    
            presenter.WriteInfo("Relationships:");
            presenter.WriteInfo($"  FromRow: {BuildEntityRowAddress(fromEntityName, row.Id)}");
            presenter.WriteTable(
                new[] { "ToEntity", "ToRow" },
                relationshipRows
                    .Select(item => (IReadOnlyList<string>)new[]
                    {
                        item.ToEntity,
                        item.ToRow,
                    })
                    .ToList());
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}
