using Meta.Core.Services;

namespace Meta.Adapters;

public sealed class ServiceCollection
{
    public IWorkspaceService WorkspaceService { get; }
    public IValidationService ValidationService { get; }
    public IImportService ImportService { get; }
    public IExportService ExportService { get; }
    public IOperationService OperationService { get; }
    public IModelRefactorService ModelRefactorService { get; }

    public ServiceCollection()
    {
        WorkspaceService = new WorkspaceService();
        ValidationService = new ValidationService();
        OperationService = new OperationService();
        ModelRefactorService = new ModelRefactorService();
        ImportService = new ImportService(WorkspaceService);
        ExportService = new ExportService(WorkspaceService);
    }
}
