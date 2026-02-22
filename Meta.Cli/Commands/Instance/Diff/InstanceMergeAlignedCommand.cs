internal sealed partial class CliRuntime
{
    async Task<int> InstanceMergeAlignedAsync(string[] commandArgs)
    {
        if (commandArgs.Length != 4)
        {
            return PrintUsageError("Usage: instance merge-aligned <targetWorkspace> <diffWorkspace>");
        }

        var targetPath = Path.GetFullPath(commandArgs[2]);
        var diffWorkspacePath = Path.GetFullPath(commandArgs[3]);

        var targetWorkspace = await services.WorkspaceService.LoadAsync(targetPath, searchUpward: false).ConfigureAwait(false);
        var diffWorkspace = await services.WorkspaceService.LoadAsync(diffWorkspacePath, searchUpward: false).ConfigureAwait(false);
        PrintContractCompatibilityWarning(targetWorkspace.Manifest);
        PrintContractCompatibilityWarning(diffWorkspace.Manifest);

        AlignedDiffData diffData;
        try
        {
            diffData = ParseAlignedDiffWorkspace(diffWorkspace);
            ValidateWorkspaceMatchesAlignment(
                targetWorkspace,
                diffData.Alignment.ModelLeftName,
                diffData.Alignment.LeftEntityNameById,
                diffData.Alignment.LeftPropertyNameById,
                diffData.Alignment.LeftPropertyEntityIdByPropertyId);
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }

        var preSnapshot = BuildWorkspaceSnapshotForAlignedDiff(targetWorkspace, diffData.Alignment);
        if (!preSnapshot.RowSet.SetEquals(diffData.LeftRowSet) ||
            !preSnapshot.PropertySet.SetEquals(diffData.LeftPropertySet))
        {
            return PrintFormattedError(
                "E_CONFLICT",
                "instance merge-aligned precondition failed: target does not match the diff left snapshot.",
                exitCode: 1,
                hints: new[]
                {
                    "Next: re-run meta instance diff-aligned on the current target, intended right workspace, and alignment workspace.",
                });
        }

        var before = WorkspaceSnapshotCloner.Capture(targetWorkspace);
        try
        {
            ApplyAlignedRightSnapshotToWorkspace(targetWorkspace, diffData);
            ApplyImplicitNormalization(targetWorkspace);

            var diagnostics = services.ValidationService.Validate(targetWorkspace);
            targetWorkspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                WorkspaceSnapshotCloner.Restore(targetWorkspace, before);
                return PrintOperationValidationFailure("instance merge-aligned", Array.Empty<WorkspaceOp>(), diagnostics);
            }

            var postSnapshot = BuildWorkspaceSnapshotForAlignedDiff(targetWorkspace, diffData.Alignment);
            if (!postSnapshot.RowSet.SetEquals(diffData.RightRowSet) ||
                !postSnapshot.PropertySet.SetEquals(diffData.RightPropertySet))
            {
                WorkspaceSnapshotCloner.Restore(targetWorkspace, before);
                return PrintDataError(
                    "E_OPERATION",
                    "instance merge-aligned postcondition failed: target does not match the diff right snapshot.");
            }

            await services.WorkspaceService.SaveAsync(targetWorkspace).ConfigureAwait(false);
            if (globalJson)
            {
                WriteJson(new
                {
                    command = "instance.merge-aligned",
                    status = "ok",
                    target = targetPath,
                });
            }
            else
            {
                presenter.WriteOk(
                    "instance merge-aligned applied",
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
