namespace Meta.Core.Domain;

public sealed class Workspace
{
    public string WorkspaceRootPath { get; set; } = string.Empty;
    public string MetadataRootPath { get; set; } = string.Empty;
    public WorkspaceManifest Manifest { get; set; } = WorkspaceManifest.CreateDefault();
    public ModelDefinition Model { get; set; } = new();
    public InstanceStore Instance { get; set; } = new();
    public WorkspaceDiagnostics Diagnostics { get; set; } = new();
    public bool IsDirty { get; set; }
}
