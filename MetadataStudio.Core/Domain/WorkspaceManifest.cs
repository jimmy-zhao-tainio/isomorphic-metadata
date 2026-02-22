namespace MetadataStudio.Core.Domain;

public sealed class WorkspaceManifest
{
    public string ContractVersion { get; set; } = "1.0";
    public string ModelFile { get; set; } = "metadata/model.xml";
    public string InstanceDir { get; set; } = "metadata/instance";
    public string Encoding { get; set; } = "utf-8-no-bom";
    public string Newlines { get; set; } = "lf";
    public CanonicalSortManifest CanonicalSort { get; set; } = new();
    public List<EntityStorageManifest> EntityStorages { get; set; } = new();

    public static WorkspaceManifest CreateDefault()
    {
        return new WorkspaceManifest();
    }
}

public sealed class EntityStorageManifest
{
    public string EntityName { get; set; } = string.Empty;
    public string StorageKind { get; set; } = "Sharded";
    public string DirectoryPath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
}

public sealed class CanonicalSortManifest
{
    public string Entities { get; set; } = "name-ordinal";
    public string Properties { get; set; } = "name-ordinal";
    public string Relationships { get; set; } = "name-ordinal";
    public string Rows { get; set; } = "id-ordinal";
    public string Attributes { get; set; } = "id-first-then-name-ordinal";
}
