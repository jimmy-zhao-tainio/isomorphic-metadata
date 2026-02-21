using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public class SqlServerDataGenerator
    {
        public string Generate(Model model, ModelInstance instance)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var entityLookup = instance.Entities
                .Where(e => e.Entity != null && !string.IsNullOrWhiteSpace(e.Entity.Name))
                .ToDictionary(e => e.Entity.Name, StringComparer.OrdinalIgnoreCase);

            var orderedEntities = GetOrderedEntities(model);
            var commands = new StringBuilder();
            commands.AppendLine("-- Data insertion script");

            foreach (var entity in orderedEntities)
            {
                EntityInstance entityInstance;
                if (!entityLookup.TryGetValue(entity.Name, out entityInstance))
                {
                    continue;
                }

                foreach (var record in entityInstance.Records
                    .OrderBy(r => r.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    AppendInsertStatement(commands, entity, record);
                }
            }

            return commands.ToString();
        }

        private static List<Entity> GetOrderedEntities(Model model)
        {
            // Topological sorting ensures parent entities are inserted before dependents.
            var result = new List<Entity>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = new List<string>();
            var entityLookup = model.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var entity in model.Entities
                .OrderBy(e => e.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                Visit(entity, entityLookup, visited, visiting, path, result);
            }

            return result;
        }

        private static void Visit(
            Entity entity,
            IDictionary<string, Entity> entityLookup,
            HashSet<string> visited,
            HashSet<string> visiting,
            List<string> path,
            List<Entity> result)
        {
            if (visited.Contains(entity.Name))
            {
                return;
            }

            if (visiting.Contains(entity.Name))
            {
                var cycleStart = path.FindIndex(n => string.Equals(n, entity.Name, StringComparison.OrdinalIgnoreCase));
                var cycle = cycleStart >= 0
                    ? path.Skip(cycleStart).Concat(new[] { entity.Name })
                    : new[] { entity.Name };
                throw new InvalidOperationException("Relationship cycle detected: " + string.Join(" -> ", cycle) + ".");
            }

            visiting.Add(entity.Name);
            path.Add(entity.Name);

            if (entity.Relationship != null)
            {
                foreach (var related in entity.Relationship)
                {
                    if (related == null || string.IsNullOrWhiteSpace(related.Entity))
                    {
                        continue;
                    }

                    Entity relatedEntity;
                    if (entityLookup.TryGetValue(related.Entity, out relatedEntity))
                    {
                        Visit(relatedEntity, entityLookup, visited, visiting, path, result);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Entity '{entity.Name}' references unknown relationship entity '{related.Entity}'.");
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(entity.Name);
            visited.Add(entity.Name);
            result.Add(entity);
        }

        private static void AppendInsertStatement(StringBuilder builder, Entity entity, RecordInstance record)
        {
            var columns = new List<string> { "[Id]" };
            var values = new List<string> { ToSqlLiteral(record.Id) };
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" };

            foreach (var property in entity.Properties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var propertyValue = record.Properties.FirstOrDefault(p =>
                    p.Property != null &&
                    string.Equals(p.Property.Name, property.Name, StringComparison.OrdinalIgnoreCase));

                columns.Add($"[{EscapeIdentifier(property.Name)}]");
                existingColumns.Add(property.Name);
                values.Add(ToSqlLiteral(propertyValue != null ? propertyValue.Value : null));
            }

            foreach (var relationship in record.Relationships)
            {
                if (relationship.Entity == null || string.IsNullOrWhiteSpace(relationship.Entity.Name))
                {
                    continue;
                }

                var columnName = $"{relationship.Entity.Name}Id";

                if (existingColumns.Contains(columnName))
                {
                    continue;
                }

                columns.Add($"[{EscapeIdentifier(columnName)}]");
                existingColumns.Add(columnName);
                values.Add(ToSqlLiteral(relationship.Value));
            }

            builder.AppendLine($"INSERT INTO [dbo].[{EscapeIdentifier(entity.Name)}] ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
        }

        private static string ToSqlLiteral(string value)
        {
            if (value == null)
            {
                return "NULL";
            }

            return "N'" + value.Replace("'", "''") + "'";
        }

        private static string EscapeIdentifier(string name)
        {
            return (name ?? string.Empty).Replace("]", "]]");
        }
    }
}

