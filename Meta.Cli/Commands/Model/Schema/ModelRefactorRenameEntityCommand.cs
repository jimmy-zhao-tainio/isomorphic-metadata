using Meta.Core.Operations;
using Meta.Core.Services;

internal sealed partial class CliRuntime
{
    async Task<int> ModelRefactorRenameEntityAsync(string[] commandArgs)
    {
        var options = ParseModelRefactorRenameEntityOptions(commandArgs, startIndex: 4);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }

        var commandOptions = options.Options;

        Workspace? workspace = null;
        WorkspaceSnapshot? before = null;
        try
        {
            workspace = await LoadWorkspaceForCommandAsync(commandOptions.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.WorkspaceConfig);
            before = WorkspaceSnapshotCloner.Capture(workspace);

            var result = services.ModelRefactorService.RenameEntity(workspace, commandOptions.Refactor);
            ApplyImplicitNormalization(workspace);

            var diagnostics = services.ValidationService.Validate(workspace);
            workspace.Diagnostics = diagnostics;
            if (diagnostics.HasErrors || (globalStrict && diagnostics.WarningCount > 0))
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
                return PrintOperationValidationFailure(
                    "model refactor rename entity",
                    Array.Empty<WorkspaceOp>(),
                    diagnostics);
            }

            await services.WorkspaceService.SaveAsync(workspace).ConfigureAwait(false);

            presenter.WriteOk(
                "refactor rename entity",
                ("Workspace", Path.GetFullPath(workspace.WorkspaceRootPath)),
                ("Model", workspace.Model.Name),
                ("From", result.OldEntityName),
                ("To", result.NewEntityName),
                ("Relationships updated", result.RelationshipsUpdated.ToString()),
                ("FK fields renamed", result.FkFieldsRenamed.ToString()),
                ("Rows touched", result.RowsTouched.ToString()));
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            if (workspace != null && before != null)
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
            }

            return PrintDataError("E_OPERATION", exception.Message);
        }
        catch
        {
            if (workspace != null && before != null)
            {
                WorkspaceSnapshotCloner.Restore(workspace, before);
            }

            throw;
        }
    }

    (bool Ok, RenameEntityCommandOptions Options, string ErrorMessage)
        ParseModelRefactorRenameEntityOptions(string[] commandArgs, int startIndex)
    {
        var workspacePath = DefaultWorkspacePath();
        var from = string.Empty;
        var to = string.Empty;

        for (var i = startIndex; i < commandArgs.Length; i++)
        {
            var arg = commandArgs[i];
            if (string.Equals(arg, "--workspace", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --workspace requires a path.");
                }

                workspacePath = commandArgs[++i];
                continue;
            }

            if (string.Equals(arg, "--from", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --from requires <OldEntity>.");
                }

                from = commandArgs[++i].Trim();
                continue;
            }

            if (string.Equals(arg, "--to", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= commandArgs.Length)
                {
                    return (false, default, "Error: --to requires <NewEntity>.");
                }

                to = commandArgs[++i].Trim();
                continue;
            }

            return (false, default, $"Error: unknown option '{arg}'.");
        }

        if (string.IsNullOrWhiteSpace(from))
        {
            return (false, default, "Error: --from <OldEntity> is required.");
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            return (false, default, "Error: --to <NewEntity> is required.");
        }

        if (!ModelNamePattern.IsMatch(to))
        {
            return (false, default, "Error: --to must use identifier pattern [A-Za-z_][A-Za-z0-9_]*.");
        }

        return (true, new RenameEntityCommandOptions(
            WorkspacePath: workspacePath,
            Refactor: new RenameEntityRefactorOptions(from, to)), string.Empty);
    }

    readonly record struct RenameEntityCommandOptions(
        string WorkspacePath,
        RenameEntityRefactorOptions Refactor);
}
