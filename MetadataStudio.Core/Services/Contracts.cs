using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MetadataStudio.Core.Domain;
using MetadataStudio.Core.Operations;

namespace MetadataStudio.Core.Services;

public interface IWorkspaceService
{
    Task<Workspace> LoadAsync(
        string workspaceRootPath,
        bool searchUpward = true,
        CancellationToken cancellationToken = default);
    Task SaveAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task SaveAsync(Workspace workspace, string? expectedFingerprint, CancellationToken cancellationToken = default);
    string CalculateHash(Workspace workspace);
}

public interface IValidationService
{
    WorkspaceDiagnostics Validate(Workspace workspace);
    WorkspaceDiagnostics ValidateIncremental(Workspace workspace, IReadOnlyCollection<string> touchedEntities);
}

public interface IImportService
{
    Task<Workspace> ImportXmlAsync(string modelPath, string instancePath, CancellationToken cancellationToken = default);
    Task<Workspace> ImportSqlAsync(string connectionString, string schema, CancellationToken cancellationToken = default);
}

public interface IExportService
{
    Task ExportXmlAsync(Workspace workspace, string outputDirectory, CancellationToken cancellationToken = default);
    Task ExportSqlAsync(Workspace workspace, string schemaOutputPath, string dataOutputPath, CancellationToken cancellationToken = default);
    Task ExportCSharpAsync(Workspace workspace, string outputPath, CancellationToken cancellationToken = default);
}

public interface IOperationService
{
    void Execute(Workspace workspace, WorkspaceOp operation);
    bool CanUndo(Workspace workspace);
    bool CanRedo(Workspace workspace);
    void Undo(Workspace workspace);
    void Redo(Workspace workspace);
    void ApplyWithoutHistory(Workspace workspace, WorkspaceOp operation);
    IReadOnlyCollection<WorkspaceOp> GetUndoOperations(Workspace workspace);
}
