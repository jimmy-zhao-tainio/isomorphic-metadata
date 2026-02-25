internal sealed partial class CliRuntime
{
    async Task<int> InstanceMergeAsync(string[] commandArgs)
    {
        if (commandArgs.Length != 4)
        {
            return PrintUsageError("Usage: instance merge <targetWorkspace> <diffWorkspace>");
        }

        var targetPath = Path.GetFullPath(commandArgs[2]);
        var diffWorkspacePath = Path.GetFullPath(commandArgs[3]);

        var targetWorkspace = await services.WorkspaceService.LoadAsync(targetPath, searchUpward: false).ConfigureAwait(false);
        var diffWorkspace = await services.WorkspaceService.LoadAsync(diffWorkspacePath, searchUpward: false).ConfigureAwait(false);
        PrintContractCompatibilityWarning(targetWorkspace.WorkspaceConfig);
        PrintContractCompatibilityWarning(diffWorkspace.WorkspaceConfig);

        EqualDiffData diffData;
        try
        {
            diffData = ParseEqualDiffWorkspace(diffWorkspace);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }

        var preSnapshot = BuildWorkspaceSnapshotForEqualDiff(targetWorkspace, diffData);
        if (!preSnapshot.RowSet.SetEquals(diffData.LeftRowSet) ||
            !preSnapshot.PropertySet.SetEquals(diffData.LeftPropertySet))
        {
            return PrintFormattedError(
                "E_CONFLICT",
                "instance merge precondition failed: target does not match the diff left snapshot.",
                exitCode: 1,
                hints: new[]
                {
                    "Next: re-run meta instance diff on the current target and intended right workspace.",
                });
        }

        var before = WorkspaceSnapshotCloner.Capture(targetWorkspace);
        try
        {
            ApplyEqualRightSnapshotToWorkspace(targetWorkspace, diffData);
            ApplyImplicitNormalization(targetWorkspace);

            var diagnostics = services.ValidationService.Validate(targetWorkspace);
            targetWorkspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                WorkspaceSnapshotCloner.Restore(targetWorkspace, before);
                return PrintOperationValidationFailure("instance merge", Array.Empty<WorkspaceOp>(), diagnostics);
            }

            var postSnapshot = BuildWorkspaceSnapshotForEqualDiff(targetWorkspace, diffData);
            if (!postSnapshot.RowSet.SetEquals(diffData.RightRowSet) ||
                !postSnapshot.PropertySet.SetEquals(diffData.RightPropertySet))
            {
                WorkspaceSnapshotCloner.Restore(targetWorkspace, before);
                return PrintDataError(
                    "E_OPERATION",
                    "instance merge postcondition failed: target does not match the diff right snapshot.");
            }

            await services.WorkspaceService.SaveAsync(targetWorkspace).ConfigureAwait(false);
            if (globalJson)
            {
                WriteJson(new
                {
                    command = "instance.merge",
                    status = "ok",
                    target = targetPath,
                });
            }
            else
            {
                presenter.WriteOk(
                    "instance merge applied",
                    ("Target", targetPath));
            }

            return 0;
        }
        catch (InvalidOperationException exception)
        {
            WorkspaceSnapshotCloner.Restore(targetWorkspace, before);
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}

