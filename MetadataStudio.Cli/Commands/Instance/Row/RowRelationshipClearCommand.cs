internal sealed partial class CliRuntime
{
    async Task<int> RowRelationshipClearAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 6)
        {
            return PrintUsageError(
                "Usage: row relationship clear <FromEntity> <FromId> --to-entity <ToEntity> [--workspace <path>]");
        }
    
        var fromEntityName = commandArgs[3];
        var fromId = commandArgs[4];
        if (ContainsLegacyRowReferenceSyntax(fromId))
        {
            return PrintArgumentError($"Error: unsupported row reference '{fromId}'. Use <Entity> <Id>.");
        }
        var options = ParseRowRelationshipClearOptions(commandArgs, startIndex: 5);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        if (string.IsNullOrWhiteSpace(options.ToEntity))
        {
            return PrintArgumentError("Error: row relationship clear requires --to-entity <ToEntity>.");
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.Manifest);
            var fromEntity = RequireEntity(workspace, fromEntityName);
            var fromRow = ResolveRowById(workspace, fromEntityName, fromId);
            var toEntityName = options.ToEntity;
            var toRelationshipName = ResolveRelationshipName(fromEntity, toEntityName);
            if (string.IsNullOrWhiteSpace(toRelationshipName))
            {
                return PrintDataError(
                    "E_RELATIONSHIP_NOT_FOUND",
                    $"Relationship '{fromEntityName}->{toEntityName}' does not exist.");
            }
    
            if (CountRelationshipUsages(fromRow, toRelationshipName) == 0)
            {
                if (globalJson)
                {
                    WriteJson(new
                    {
                        command = "row.relationship.clear",
                        status = "ok",
                        changes = 0,
                        fromRow = BuildEntityRowAddress(fromEntityName, fromRow.Id),
                        toEntity = toRelationshipName,
                    });
                }
                else
                {
                    presenter.WriteOk(
                        "relationship usage clear (no changes)",
                        ("FromRow", BuildEntityRowAddress(fromEntityName, fromRow.Id)),
                        ("ToEntity", toRelationshipName));
                }
    
                return 0;
            }
    
            var patch = BuildRelationshipUsageRewritePatch(fromRow, toRelationshipName, targetId: null);
    
            var operation = new WorkspaceOp
            {
                Type = WorkspaceOpTypes.BulkUpsertRows,
                EntityName = fromEntityName,
                RowPatches = { patch },
            };
            return await ExecuteOperationsAgainstLoadedWorkspaceAsync(
                    workspace,
                    new[] { operation },
                    commandName: "row.relationship.clear",
                    successMessage: "relationship usage removed",
                    successDetails: new[]
                    {
                        ("FromRow", BuildEntityRowAddress(fromEntityName, fromRow.Id)),
                        ("ToEntity", toRelationshipName),
                    })
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}
