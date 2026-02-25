internal sealed partial class CliRuntime
{
    async Task<int> ImportAsync(string[] commandArgs)
    {
        if (commandArgs.Length < 2)
        {
            return PrintUsageError("Usage: import <xml|sql|csv> ...");
        }
    
        var mode = commandArgs[1].Trim().ToLowerInvariant();
        try
        {
            switch (mode)
            {
                case "xml":
                    if (commandArgs.Length < 4)
                    {
                        return PrintUsageError("Usage: import xml <modelXmlPath> <instanceXmlPath> --new-workspace <path>");
                    }
    
                    var xmlOptions = ParseRequiredNewWorkspaceOption(commandArgs, startIndex: 4);
                    if (!xmlOptions.Ok)
                    {
                        return PrintArgumentError(xmlOptions.ErrorMessage);
                    }
    
                    var workspacePath = xmlOptions.NewWorkspacePath;
                    var targetValidation = ValidateNewWorkspaceTarget(workspacePath);
                    if (targetValidation != 0)
                    {
                        return targetValidation;
                    }
    
                    var importedWorkspace = await services.ImportService.ImportXmlAsync(commandArgs[2], commandArgs[3]).ConfigureAwait(false);
                    ApplyImplicitNormalization(importedWorkspace);
                    var xmlDiagnostics = services.ValidationService.Validate(importedWorkspace);
                    importedWorkspace.Diagnostics = xmlDiagnostics;
                    if (xmlDiagnostics.HasErrors || (globalStrict && xmlDiagnostics.WarningCount > 0))
                    {
                        return PrintOperationValidationFailure("import", Array.Empty<WorkspaceOp>(), xmlDiagnostics);
                    }
                    await services.ExportService.ExportXmlAsync(importedWorkspace, workspacePath).ConfigureAwait(false);
                    if (globalJson)
                    {
                        WriteJson(new
                        {
                            command = "import.xml",
                            status = "ok",
                            workspace = Path.GetFullPath(workspacePath),
                        });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "imported xml",
                            ("Workspace", Path.GetFullPath(workspacePath)));
                    }
    
                    return 0;
                case "sql":
                    if (commandArgs.Length < 4)
                    {
                        return PrintUsageError("Usage: import sql <connectionString> <schema> --new-workspace <path>");
                    }
    
                    var sqlOptions = ParseRequiredNewWorkspaceOption(commandArgs, startIndex: 4);
                    if (!sqlOptions.Ok)
                    {
                        return PrintArgumentError(sqlOptions.ErrorMessage);
                    }
    
                    workspacePath = sqlOptions.NewWorkspacePath;
                    targetValidation = ValidateNewWorkspaceTarget(workspacePath);
                    if (targetValidation != 0)
                    {
                        return targetValidation;
                    }
    
                    var importedFromSql = await services.ImportService.ImportSqlAsync(commandArgs[2], commandArgs[3]).ConfigureAwait(false);
                    ApplyImplicitNormalization(importedFromSql);
                    var sqlDiagnostics = services.ValidationService.Validate(importedFromSql);
                    importedFromSql.Diagnostics = sqlDiagnostics;
                    if (sqlDiagnostics.HasErrors || (globalStrict && sqlDiagnostics.WarningCount > 0))
                    {
                        return PrintOperationValidationFailure("import", Array.Empty<WorkspaceOp>(), sqlDiagnostics);
                    }
                    await services.ExportService.ExportXmlAsync(importedFromSql, workspacePath).ConfigureAwait(false);
                    if (globalJson)
                    {
                        WriteJson(new
                        {
                            command = "import.sql",
                            status = "ok",
                            workspace = Path.GetFullPath(workspacePath),
                            model = importedFromSql.Model.Name,
                            entities = importedFromSql.Model.Entities.Count,
                            rows = importedFromSql.Instance.RecordsByEntity.Values.Sum(rows => rows.Count),
                        });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "imported sql",
                            ("Workspace", Path.GetFullPath(workspacePath)));
                    }
    
                    return 0;
                case "csv":
                    if (commandArgs.Length < 3)
                    {
                        return PrintUsageError(
                            "Usage: import csv <csvFile> --entity <EntityName> (--workspace <path> | --new-workspace <path>)");
                    }

                    var csvOptions = ParseImportCsvOptions(commandArgs, startIndex: 3);
                    if (!csvOptions.Ok)
                    {
                        return PrintArgumentError(csvOptions.ErrorMessage);
                    }

                    var importedFromCsv = await services.ImportService
                        .ImportCsvAsync(commandArgs[2], csvOptions.EntityName)
                        .ConfigureAwait(false);
                    var importedEntity = importedFromCsv.Model.Entities.Single();
                    var importedRows = importedFromCsv.Instance.RecordsByEntity[importedEntity.Name];

                    if (csvOptions.UseNewWorkspace)
                    {
                        workspacePath = csvOptions.NewWorkspacePath;
                        targetValidation = ValidateNewWorkspaceTarget(workspacePath);
                        if (targetValidation != 0)
                        {
                            return targetValidation;
                        }

                        ApplyImplicitNormalization(importedFromCsv);
                        var csvDiagnostics = services.ValidationService.Validate(importedFromCsv);
                        importedFromCsv.Diagnostics = csvDiagnostics;
                        if (csvDiagnostics.HasErrors || (globalStrict && csvDiagnostics.WarningCount > 0))
                        {
                            return PrintOperationValidationFailure("import", Array.Empty<WorkspaceOp>(), csvDiagnostics);
                        }

                        await services.ExportService.ExportXmlAsync(importedFromCsv, workspacePath).ConfigureAwait(false);
                        if (globalJson)
                        {
                            WriteJson(new
                            {
                                command = "import.csv",
                                status = "ok",
                                workspace = Path.GetFullPath(workspacePath),
                                entity = importedEntity.Name,
                                rows = importedRows.Count,
                                properties = importedEntity.Properties.Count,
                            });
                        }
                        else
                        {
                            presenter.WriteOk(
                                "imported csv",
                                ("Workspace", Path.GetFullPath(workspacePath)),
                                ("Entity", importedEntity.Name),
                                ("Rows", importedRows.Count.ToString()));
                        }

                        return 0;
                    }

                    workspacePath = csvOptions.WorkspacePath;
                    var workspaceForCsv = await LoadWorkspaceForCommandAsync(workspacePath).ConfigureAwait(false);
                    PrintContractCompatibilityWarning(workspaceForCsv.WorkspaceConfig);

                    if (workspaceForCsv.Model.Entities.Any(entity =>
                            string.Equals(entity.Name, importedEntity.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return PrintDataError(
                            "E_OPERATION",
                            $"Entity '{importedEntity.Name}' already exists in workspace.");
                    }

                    workspaceForCsv.Model.Entities.Add(importedEntity);
                    workspaceForCsv.Instance.RecordsByEntity[importedEntity.Name] = importedRows;
                    ApplyImplicitNormalization(workspaceForCsv);

                    var workspaceCsvDiagnostics = services.ValidationService.Validate(workspaceForCsv);
                    workspaceForCsv.Diagnostics = workspaceCsvDiagnostics;
                    if (workspaceCsvDiagnostics.HasErrors || (globalStrict && workspaceCsvDiagnostics.WarningCount > 0))
                    {
                        return PrintOperationValidationFailure("import", Array.Empty<WorkspaceOp>(), workspaceCsvDiagnostics);
                    }

                    await services.WorkspaceService.SaveAsync(workspaceForCsv).ConfigureAwait(false);
                    if (globalJson)
                    {
                        WriteJson(new
                        {
                            command = "import.csv",
                            status = "ok",
                            workspace = Path.GetFullPath(workspaceForCsv.WorkspaceRootPath),
                            entity = importedEntity.Name,
                            rows = importedRows.Count,
                            properties = importedEntity.Properties.Count,
                        });
                    }
                    else
                    {
                        presenter.WriteOk(
                            "imported csv",
                            ("Workspace", Path.GetFullPath(workspaceForCsv.WorkspaceRootPath)),
                            ("Entity", importedEntity.Name),
                            ("Rows", importedRows.Count.ToString()));
                    }

                    return 0;
                default:
                    return PrintUsageError("Usage: import <xml|sql|csv> ...");
            }
        }
        catch (Exception exception)
        {
            return PrintDataError("E_IMPORT", exception.Message);
        }
    }
}

