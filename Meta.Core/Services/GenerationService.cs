using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Meta.Core.Domain;

namespace Meta.Core.Services;

public sealed class GenerationManifest
{
    public string RootPath { get; set; } = string.Empty;
    public Dictionary<string, string> FileHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string CombinedHash { get; set; } = string.Empty;
}

public static class GenerationService
{
    public static GenerationManifest GenerateSql(Workspace workspace, string outputDirectory)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        var outputRoot = PrepareOutputDirectory(outputDirectory);
        WriteText(Path.Combine(outputRoot, "schema.sql"), BuildSqlSchema(workspace));
        WriteText(Path.Combine(outputRoot, "data.sql"), BuildSqlData(workspace));
        return BuildManifest(outputRoot);
    }

    public static GenerationManifest GenerateCSharp(Workspace workspace, string outputDirectory)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        var outputRoot = PrepareOutputDirectory(outputDirectory);
        var modelTypeName = ResolveModelTypeName(workspace.Model.Name);
        var modelFileName = modelTypeName + ".cs";
        WriteText(Path.Combine(outputRoot, modelFileName), BuildCSharpModel(workspace, modelTypeName));
        var emittedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            modelFileName,
        };

        foreach (var entity in workspace.Model.Entities
                     .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entityFileName = entity.Name + ".cs";
            if (!emittedFiles.Add(entityFileName))
            {
                throw new InvalidOperationException(
                    $"Cannot generate C# output because model and entity file names collide on '{entityFileName}'.");
            }

            WriteText(Path.Combine(outputRoot, entityFileName), BuildCSharpEntity(entity));
        }

        return BuildManifest(outputRoot);
    }

    public static GenerationManifest GenerateSsdt(Workspace workspace, string outputDirectory)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        var outputRoot = PrepareOutputDirectory(outputDirectory);
        var schema = BuildSqlSchema(workspace);
        var data = BuildSqlData(workspace);
        WriteText(Path.Combine(outputRoot, "Schema.sql"), schema);
        WriteText(Path.Combine(outputRoot, "Data.sql"), data);
        WriteText(Path.Combine(outputRoot, "PostDeploy.sql"), BuildPostDeployScript());
        WriteText(Path.Combine(outputRoot, "Metadata.sqlproj"), BuildSqlProjectFile(workspace));
        return BuildManifest(outputRoot);
    }

    public static GenerationManifest BuildManifest(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        }

        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory '{root}' was not found.");
        }

        var manifest = new GenerationManifest
        {
            RootPath = root,
        };

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var path in files)
        {
            var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            var hash = ComputeFileHash(path);
            manifest.FileHashes[relativePath] = hash;
        }

        manifest.CombinedHash = ComputeCombinedHash(manifest.FileHashes);
        return manifest;
    }

    public static bool AreEquivalent(GenerationManifest left, GenerationManifest right, out string message)
    {
        if (left.FileHashes.Count != right.FileHashes.Count)
        {
            message = $"File count mismatch: left={left.FileHashes.Count}, right={right.FileHashes.Count}.";
            return false;
        }

        foreach (var file in left.FileHashes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!right.FileHashes.TryGetValue(file.Key, out var otherHash))
            {
                message = $"Missing file in right manifest: {file.Key}.";
                return false;
            }

            if (!string.Equals(file.Value, otherHash, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Content hash mismatch for '{file.Key}'.";
                return false;
            }
        }

        if (!string.Equals(left.CombinedHash, right.CombinedHash, StringComparison.OrdinalIgnoreCase))
        {
            message = "Combined hash mismatch.";
            return false;
        }

        message = "Equivalent.";
        return true;
    }

    private static string PrepareOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            Directory.Delete(dir, recursive: false);
        }

        return root;
    }

    private static void WriteText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string BuildSqlSchema(Workspace workspace)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-- Deterministic schema script");
        builder.AppendLine();

        var entities = workspace.Model.Entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
            .OrderBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var relationships = new List<(string Source, string Target, string Column)>();
        foreach (var entity in entities)
        {
            var columns = new List<string>
            {
                "    [Id] NVARCHAR(128) NOT NULL",
            };

            foreach (var property in entity.Properties
                         .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase))
            {
                var nullable = property.IsNullable ? "NULL" : "NOT NULL";
                columns.Add($"    [{EscapeSqlIdentifier(property.Name)}] NVARCHAR(256) {nullable}");
            }

            foreach (var relationship in entity.Relationships
                         .OrderBy(relationship => relationship.GetColumnName(), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(relationship => relationship.GetUsageName(), StringComparer.OrdinalIgnoreCase))
            {
                var columnName = relationship.GetColumnName();
                columns.Add($"    [{EscapeSqlIdentifier(columnName)}] NVARCHAR(128) NOT NULL");
                relationships.Add((entity.Name, relationship.Entity, columnName));
            }

            columns.Add($"    CONSTRAINT [PK_{EscapeSqlIdentifier(entity.Name)}] PRIMARY KEY CLUSTERED ([Id] ASC)");
            builder.AppendLine($"CREATE TABLE [dbo].[{EscapeSqlIdentifier(entity.Name)}] (");
            builder.AppendLine(string.Join(",\n", columns));
            builder.AppendLine(");");
            builder.AppendLine("GO");
            builder.AppendLine();
        }

        foreach (var relationship in relationships
                     .OrderBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Target, StringComparer.OrdinalIgnoreCase))
        {
            var constraintName = $"FK_{relationship.Source}_{relationship.Target}_{relationship.Column}";
            builder.AppendLine(
                $"ALTER TABLE [dbo].[{EscapeSqlIdentifier(relationship.Source)}] WITH CHECK ADD CONSTRAINT [{EscapeSqlIdentifier(constraintName)}] FOREIGN KEY([{EscapeSqlIdentifier(relationship.Column)}]) REFERENCES [dbo].[{EscapeSqlIdentifier(relationship.Target)}]([Id]);");
            builder.AppendLine("GO");
            builder.AppendLine();
        }

        return NormalizeNewlines(builder.ToString());
    }

    private static string BuildSqlData(Workspace workspace)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-- Deterministic data script");
        builder.AppendLine();

        foreach (var entity in GetEntitiesTopologically(workspace.Model))
        {
            if (!workspace.Instance.RecordsByEntity.TryGetValue(entity.Name, out var records))
            {
                continue;
            }

            foreach (var row in records.OrderBy(record => record.Id, StringComparer.OrdinalIgnoreCase))
            {
                var columns = new List<string> { "[Id]" };
                var values = new List<string> { ToSqlLiteral(row.Id) };

                foreach (var property in entity.Properties
                             .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    columns.Add($"[{EscapeSqlIdentifier(property.Name)}]");
                    values.Add(row.Values.TryGetValue(property.Name, out var propertyValue)
                        ? ToSqlLiteral(propertyValue)
                        : "NULL");
                }

                foreach (var relationship in entity.Relationships
                             .OrderBy(relationship => relationship.GetColumnName(), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(relationship => relationship.GetUsageName(), StringComparer.OrdinalIgnoreCase))
                {
                    var columnName = relationship.GetColumnName();
                    columns.Add($"[{EscapeSqlIdentifier(columnName)}]");
                    values.Add(row.RelationshipIds.TryGetValue(relationship.GetUsageName(), out var relationshipValue)
                        ? ToSqlLiteral(relationshipValue)
                        : "NULL");
                }

                builder.AppendLine(
                    $"INSERT INTO [dbo].[{EscapeSqlIdentifier(entity.Name)}] ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
            }

            builder.AppendLine();
        }

        return NormalizeNewlines(builder.ToString());
    }

    private static string BuildCSharpModel(Workspace workspace, string modelTypeName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace GeneratedMetadata");
        builder.AppendLine("{");
        builder.AppendLine($"    public sealed class {modelTypeName}");
        builder.AppendLine("    {");

        foreach (var entity in workspace.Model.Entities
                     .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
                     .OrderBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                $"        public List<{entity.Name}> {entity.Name} {{ get; set; }} = new List<{entity.Name}>();");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return NormalizeNewlines(builder.ToString());
    }

    private static string BuildCSharpEntity(EntityDefinition entity)
    {
        var builder = new StringBuilder();
        builder.AppendLine("namespace GeneratedMetadata");
        builder.AppendLine("{");
        builder.AppendLine($"    public sealed class {entity.Name}");
        builder.AppendLine("    {");
        builder.AppendLine("        public string Id { get; set; } = string.Empty;");

        foreach (var property in entity.Properties
                     .Where(property => !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"        public string {property.Name} {{ get; set; }} = string.Empty;");
        }

        foreach (var relationship in entity.Relationships
                     .OrderBy(relationship => relationship.GetUsageName(), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(relationship => relationship.Entity, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"        public string {relationship.GetUsageName()} {{ get; set; }} = string.Empty;");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return NormalizeNewlines(builder.ToString());
    }

    private static string ResolveModelTypeName(string? modelName)
    {
        return string.IsNullOrWhiteSpace(modelName) ? "MetadataModel" : modelName.Trim();
    }

    private static string BuildPostDeployScript()
    {
        return NormalizeNewlines(
            "-- Deterministic post-deploy script\n" +
            ":r .\\Data.sql\n");
    }

    private static string BuildSqlProjectFile(Workspace workspace)
    {
        var projectName = string.IsNullOrWhiteSpace(workspace.Model.Name)
            ? "MetadataModel"
            : workspace.Model.Name;
        var xml =
            "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n" +
            "  <PropertyGroup>\n" +
            $"    <Name>{EscapeXml(projectName)}</Name>\n" +
            "    <DSP>Microsoft.Data.Tools.Schema.Sql.Sql150DatabaseSchemaProvider</DSP>\n" +
            "    <ModelCollation>1033,CI</ModelCollation>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <Build Include=\"Schema.sql\" />\n" +
            "    <PostDeploy Include=\"PostDeploy.sql\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n";
        return NormalizeNewlines(xml);
    }

    private static IReadOnlyList<EntityDefinition> GetEntitiesTopologically(ModelDefinition model)
    {
        var lookup = model.Entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
            .ToDictionary(entity => entity.Name, StringComparer.OrdinalIgnoreCase);
        var result = new List<EntityDefinition>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = lookup.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var name in ordered)
        {
            Visit(name);
        }

        return result;

        void Visit(string entityName)
        {
            if (visited.Contains(entityName))
            {
                return;
            }

            if (visiting.Contains(entityName))
            {
                throw new InvalidOperationException(
                    $"Cannot generate data script because relationship cycle includes '{entityName}'.");
            }

            visiting.Add(entityName);
            var entity = lookup[entityName];
            foreach (var relationship in entity.Relationships
                         .OrderBy(item => item.Entity, StringComparer.OrdinalIgnoreCase))
            {
                if (lookup.ContainsKey(relationship.Entity))
                {
                    Visit(relationship.Entity);
                }
            }

            visiting.Remove(entityName);
            visited.Add(entityName);
            result.Add(entity);
        }
    }

    private static string ToSqlLiteral(string? value)
    {
        if (value == null)
        {
            return "NULL";
        }

        return "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static string EscapeSqlIdentifier(string? value)
    {
        return (value ?? string.Empty).Replace("]", "]]", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static string ComputeFileHash(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeCombinedHash(IReadOnlyDictionary<string, string> fileHashes)
    {
        var payload = string.Join(
            "\n",
            fileHashes
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}:{item.Value}"));
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
