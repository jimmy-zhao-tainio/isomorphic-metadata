using MetaSchema.Core;
using Meta.Core.Domain;

namespace MetaSchema.Extractors.SqlServer;

public sealed class SqlServerSchemaExtractor
{
    public Workspace ExtractEmptySchemaCatalogWorkspace(SqlServerExtractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.NewWorkspacePath))
        {
            throw new InvalidOperationException("extract sqlserver requires --new-workspace <path>.");
        }

        return MetaSchemaCatalogWorkspaces.CreateEmptySchemaCatalogWorkspace(request.NewWorkspacePath);
    }
}

public sealed class SqlServerExtractRequest
{
    public string NewWorkspacePath { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string SchemaName { get; set; } = "dbo";
}
