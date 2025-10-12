using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public class SqlServerSchemaGenerator
    {
        public string Generate(Model model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var databaseName = string.IsNullOrWhiteSpace(model.Name) ? "MetadataModel" : model.Name;
            var relationships = new List<RelationshipDefinition>();
            var builder = new StringBuilder();

            AppendDatabaseHeader(builder, databaseName);

            foreach (var entity in model.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                AppendTableDefinition(builder, entity, relationships);
            }

            if (relationships.Count > 0)
            {
                builder.AppendLine("-- Foreign keys");
                foreach (var relationship in relationships)
                {
                    builder.AppendLine(
                        $"ALTER TABLE [dbo].[{EscapeIdentifier(relationship.SourceEntity)}] WITH CHECK ADD CONSTRAINT [FK_{EscapeIdentifier(relationship.SourceEntity)}_{EscapeIdentifier(relationship.TargetEntity)}] FOREIGN KEY([{EscapeIdentifier(relationship.ColumnName)}]) REFERENCES [dbo].[{EscapeIdentifier(relationship.TargetEntity)}]([Id]);");
                    builder.AppendLine("GO");
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static void AppendDatabaseHeader(StringBuilder builder, string databaseName)
        {
            builder.AppendLine($"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NULL");
            builder.AppendLine("BEGIN");
            builder.AppendLine($"    CREATE DATABASE [{EscapeIdentifier(databaseName)}];");
            builder.AppendLine("END");
            builder.AppendLine("GO");
            builder.AppendLine($"USE [{EscapeIdentifier(databaseName)}];");
            builder.AppendLine("GO");
            builder.AppendLine();
        }

        private static void AppendTableDefinition(
            StringBuilder builder,
            Entity entity,
            List<RelationshipDefinition> relationships)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnDefinitions = new List<string>();

            // Ensure Id column exists first.
            if (!entity.Properties.Any(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)))
            {
                columnDefinitions.Add($"    [Id] {GetIdSqlType()} NOT NULL");
                existingColumns.Add("Id");
            }

            foreach (var property in entity.Properties)
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                if (!existingColumns.Add(property.Name))
                {
                    continue;
                }

                var sqlType = GetSqlType(property);
                var nullability = property.IsNullable && !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)
                    ? "NULL"
                    : "NOT NULL";

                columnDefinitions.Add($"    [{EscapeIdentifier(property.Name)}] {sqlType} {nullability}");
            }

            if (entity.Relationship != null)
            {
                foreach (var relationshipEntity in entity.Relationship)
                {
                    if (relationshipEntity == null || string.IsNullOrWhiteSpace(relationshipEntity.Name))
                    {
                        continue;
                    }

                    var columnName = GetRelationshipColumnName(relationshipEntity.Name, existingColumns);
                    columnDefinitions.Add($"    [{EscapeIdentifier(columnName)}] {GetIdSqlType()} NOT NULL");
                    relationships.Add(new RelationshipDefinition
                    {
                        SourceEntity = entity.Name,
                        TargetEntity = relationshipEntity.Name,
                        ColumnName = columnName
                    });
                }
            }

            columnDefinitions.Add($"    CONSTRAINT [PK_{EscapeIdentifier(entity.Name)}] PRIMARY KEY CLUSTERED ([Id] ASC)");

            builder.AppendLine($"-- Table: {entity.Name}");
            builder.AppendLine($"CREATE TABLE [dbo].[{EscapeIdentifier(entity.Name)}] (");
            builder.AppendLine(string.Join("," + Environment.NewLine, columnDefinitions));
            builder.AppendLine(");");
            builder.AppendLine("GO");
            builder.AppendLine();
        }

        private static string GetRelationshipColumnName(string targetEntityName, HashSet<string> existingColumns)
        {
            var baseName = $"{targetEntityName}Id";
            var candidate = baseName;
            var index = 1;

            while (existingColumns.Contains(candidate))
            {
                index++;
                candidate = $"{baseName}{index}";
            }

            existingColumns.Add(candidate);
            return candidate;
        }

        private static string GetSqlType(Property property)
        {
            if (property == null)
            {
                return "NVARCHAR(256)";
            }

            var dataType = property.DataType ?? string.Empty;
            if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
            {
                return GetIdSqlType();
            }

            if (string.Equals(dataType, "bool", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dataType, "boolean", StringComparison.OrdinalIgnoreCase))
            {
                return "BIT";
            }

            return "NVARCHAR(256)";
        }

        private static string GetIdSqlType()
        {
            return "NVARCHAR(128)";
        }

        private static string EscapeIdentifier(string name)
        {
            return (name ?? string.Empty).Replace("]", "]]");
        }

        private static string EscapeLiteral(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private class RelationshipDefinition
        {
            public string SourceEntity { get; set; }
            public string TargetEntity { get; set; }
            public string ColumnName { get; set; }
        }
    }
}
