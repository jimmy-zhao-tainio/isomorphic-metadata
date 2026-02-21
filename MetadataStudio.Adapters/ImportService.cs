using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MetadataStudio.Core.Domain;
using MetadataStudio.Core.Services;

namespace MetadataStudio.Adapters;

public sealed class ImportService : IImportService
{
    private const int MaxIdentifierLength = 128;
    private static readonly Regex IdentifierPattern = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IWorkspaceService _workspaceService;

    public ImportService(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
    }

    public async Task<Workspace> ImportXmlAsync(string modelPath, string instancePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || string.IsNullOrWhiteSpace(instancePath))
        {
            throw new ArgumentException("Model and instance paths are required.");
        }

        var importRoot = Path.Combine(Path.GetTempPath(), "metadata-studio-import", Guid.NewGuid().ToString("N"));
        var metadataRoot = Path.Combine(importRoot, "metadata");
        Directory.CreateDirectory(metadataRoot);
        File.Copy(modelPath, Path.Combine(metadataRoot, "model.xml"), overwrite: true);
        File.Copy(instancePath, Path.Combine(metadataRoot, "instance.xml"), overwrite: true);
        return await _workspaceService.LoadAsync(importRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<Workspace> ImportSqlAsync(string connectionString, string schema, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var effectiveSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema.Trim();
        ValidateIdentifier(effectiveSchema, "Schema name");

        var workspace = new Workspace
        {
            WorkspaceRootPath = Path.Combine(Path.GetTempPath(), "metadata-studio-import", Guid.NewGuid().ToString("N")),
            MetadataRootPath = string.Empty,
            Manifest = WorkspaceManifest.CreateDefault(),
            Model = new ModelDefinition(),
            Instance = new InstanceStore(),
            IsDirty = true,
        };

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        workspace.Model.Name = connection.Database ?? "MetadataModel";
        ValidateIdentifier(workspace.Model.Name, "Database name");
        workspace.Instance.ModelName = workspace.Model.Name;

        var entityLookup = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in await LoadTableNamesAsync(connection, effectiveSchema, cancellationToken).ConfigureAwait(false))
        {
            ValidateIdentifier(tableName, "Table name");
            if (entityLookup.ContainsKey(tableName))
            {
                throw new InvalidOperationException($"Duplicate table name '{tableName}' in schema '{effectiveSchema}'.");
            }

            var entity = new EntityDefinition
            {
                Name = tableName,
            };

            workspace.Model.Entities.Add(entity);
            entityLookup[tableName] = entity;
        }

        foreach (var entity in workspace.Model.Entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var columns = await LoadColumnsAsync(connection, effectiveSchema, entity.Name, cancellationToken).ConfigureAwait(false);
            ApplyEntityColumns(entity, columns);
        }

        var relationships = await LoadRelationshipsAsync(connection, effectiveSchema, cancellationToken).ConfigureAwait(false);
        foreach (var relationship in relationships)
        {
            if (!entityLookup.TryGetValue(relationship.SourceTable, out var sourceEntity) ||
                !entityLookup.TryGetValue(relationship.TargetTable, out var targetEntity))
            {
                continue;
            }

            var expectedColumn = targetEntity.Name + "Id";
            if (!string.Equals(relationship.SourceColumn, expectedColumn, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Relationship from '{sourceEntity.Name}' to '{targetEntity.Name}' must use column '{expectedColumn}'. Found '{relationship.SourceColumn}'.");
            }

            if (!sourceEntity.Relationships.Any(item =>
                    string.Equals(item.Entity, targetEntity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                sourceEntity.Relationships.Add(new RelationshipDefinition
                {
                    Entity = targetEntity.Name,
                });
            }
        }

        foreach (var entity in workspace.Model.Entities)
        {
            NormalizeRelationshipProperties(entity);
            var rows = await LoadRowsAsync(connection, effectiveSchema, entity, cancellationToken).ConfigureAwait(false);
            workspace.Instance.RecordsByEntity[entity.Name] = rows;
        }

        return workspace;
    }

    private static async Task<List<string>> LoadTableNamesAsync(
        SqlConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT TABLE_NAME
                              FROM INFORMATION_SCHEMA.TABLES
                              WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = @schema
                              ORDER BY TABLE_NAME;
                              """;
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<ColumnRow>> LoadColumnsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COLUMN_NAME, IS_NULLABLE
                              FROM INFORMATION_SCHEMA.COLUMNS
                              WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                              ORDER BY ORDINAL_POSITION;
                              """;
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
        command.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = tableName });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new ColumnRow
            {
                Name = reader.GetString(0),
                IsNullable = string.Equals(reader.GetString(1), "YES", StringComparison.OrdinalIgnoreCase),
            });
        }

        return columns;
    }

    private static void ApplyEntityColumns(EntityDefinition entity, IReadOnlyCollection<ColumnRow> columns)
    {
        var properties = new List<PropertyDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            ValidateIdentifier(column.Name, $"Column name on table '{entity.Name}'");
            if (!seen.Add(column.Name))
            {
                throw new InvalidOperationException($"Duplicate column '{column.Name}' on table '{entity.Name}'.");
            }

            if (string.Equals(column.Name, "Id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            properties.Add(new PropertyDefinition
            {
                Name = column.Name,
                DataType = "string",
                IsNullable = column.IsNullable,
            });
        }

        if (!seen.Contains("Id"))
        {
            throw new InvalidOperationException($"Table '{entity.Name}' must contain required column 'Id'.");
        }

        entity.Properties.Clear();
        entity.Properties.AddRange(properties);
    }

    private static async Task<List<RelationshipRow>> LoadRelationshipsAsync(
        SqlConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var relationships = new List<RelationshipRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  fk.TABLE_NAME AS SourceTable,
                                  fk.COLUMN_NAME AS SourceColumn,
                                  pk.TABLE_NAME AS TargetTable
                              FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                              INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE fk
                                  ON rc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
                              INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk
                                  ON rc.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
                              WHERE fk.TABLE_SCHEMA = @schema AND pk.TABLE_SCHEMA = @schema
                              ORDER BY fk.TABLE_NAME, fk.ORDINAL_POSITION;
                              """;
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            relationships.Add(new RelationshipRow
            {
                SourceTable = reader.GetString(0),
                SourceColumn = reader.GetString(1),
                TargetTable = reader.GetString(2),
            });
        }

        return relationships;
    }

    private static void NormalizeRelationshipProperties(EntityDefinition entity)
    {
        if (entity.Relationships.Count == 0 || entity.Properties.Count == 0)
        {
            return;
        }

        var relationshipColumns = entity.Relationships
            .Where(item => !string.IsNullOrWhiteSpace(item.Entity))
            .Select(item => item.Entity + "Id")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        entity.Properties.RemoveAll(property =>
            relationshipColumns.Contains(property.Name));
    }

    private static async Task<List<InstanceRecord>> LoadRowsAsync(
        SqlConnection connection,
        string schema,
        EntityDefinition entity,
        CancellationToken cancellationToken)
    {
        var rows = new List<InstanceRecord>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableName = EscapeSqlIdentifier(entity.Name);
        var schemaName = EscapeSqlIdentifier(schema);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM [{schemaName}].[{tableName}] ORDER BY [Id];";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        if (!columnNames.Contains("Id"))
        {
            throw new InvalidOperationException(
                $"Table '{schema}.{entity.Name}' does not include required column 'Id'.");
        }

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader["Id"] is DBNull)
            {
                throw new InvalidOperationException($"Table '{schema}.{entity.Name}' contains null Id values.");
            }

            var id = Convert.ToString(reader["Id"], CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"Table '{schema}.{entity.Name}' contains empty Id values.");
            }
            if (!IsPositiveIntegerIdentity(id))
            {
                throw new InvalidOperationException($"Table '{schema}.{entity.Name}' contains non-numeric Id '{id}'.");
            }

            if (!seenIds.Add(id))
            {
                throw new InvalidOperationException($"Table '{schema}.{entity.Name}' contains duplicate Id '{id}'.");
            }

            var record = new InstanceRecord
            {
                Id = id,
            };

            foreach (var property in entity.Properties
                         .Where(item => !string.Equals(item.Name, "Id", StringComparison.OrdinalIgnoreCase)))
            {
                if (!columnNames.Contains(property.Name))
                {
                    continue;
                }

                var value = reader[property.Name];
                if (value is DBNull)
                {
                    continue;
                }

                var textValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (textValue == null)
                {
                    continue;
                }

                record.Values[property.Name] = textValue;
            }

            foreach (var relationship in entity.Relationships)
            {
                var columnName = relationship.Entity + "Id";
                if (!columnNames.Contains(columnName))
                {
                    throw new InvalidOperationException(
                        $"Table '{schema}.{entity.Name}' is missing relationship column '{columnName}'.");
                }

                var relationshipValue = reader[columnName];
                if (relationshipValue is DBNull)
                {
                    throw new InvalidOperationException(
                        $"Table '{schema}.{entity.Name}' has null relationship value for '{columnName}' on row '{id}'.");
                }

                var relationshipId = Convert.ToString(relationshipValue, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(relationshipId))
                {
                    throw new InvalidOperationException(
                        $"Table '{schema}.{entity.Name}' has empty relationship value for '{columnName}' on row '{id}'.");
                }
                if (!IsPositiveIntegerIdentity(relationshipId))
                {
                    throw new InvalidOperationException(
                        $"Table '{schema}.{entity.Name}' has non-numeric relationship value '{relationshipId}' for '{columnName}' on row '{id}'.");
                }

                record.RelationshipIds[relationship.Entity] = relationshipId;
            }

            rows.Add(record);
        }

        return rows;
    }

    private static void ValidateIdentifier(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        if (value.Length > MaxIdentifierLength)
        {
            throw new InvalidOperationException($"{label} '{value}' exceeds max length {MaxIdentifierLength}.");
        }

        if (!IdentifierPattern.IsMatch(value))
        {
            throw new InvalidOperationException(
                $"{label} '{value}' is invalid. Use [A-Za-z_][A-Za-z0-9_]* and max length {MaxIdentifierLength}.");
        }
    }

    private static string EscapeSqlIdentifier(string value)
    {
        return value.Replace("]", "]]", StringComparison.Ordinal);
    }

    private static bool IsPositiveIntegerIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length == 0 || text[0] == '-')
        {
            return false;
        }

        var hasNonZeroDigit = false;
        foreach (var ch in text)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }

            if (ch != '0')
            {
                hasNonZeroDigit = true;
            }
        }

        return hasNonZeroDigit;
    }

    private sealed class ColumnRow
    {
        public string Name { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
    }

    private sealed class RelationshipRow
    {
        public string SourceTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
    }
}
