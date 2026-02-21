using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Metadata.Framework.Generic
{
    public class Reader
    {
        private const int MaxIdentifierLength = 128;
        private static readonly Regex IdentifierPattern = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public ReadResult Read(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return Read(stream);
            }
        }

        public ReadResult Read(Stream stream)
        {
            var result = new ReadResult();
            var document = XDocument.Load(stream);
            var modelElement = document.Element("Model");
            if (modelElement == null)
            {
                result.Errors.Add("Missing Model element.");
                return result;
            }

            var model = result.Model;
            model.Name = GetAttributeValue(modelElement, "name");
            ValidateIdentifier(model.Name, "Model name", result.Errors);

            var entityLookup = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            var entitiesElement = modelElement.Element("Entities");
            if (entitiesElement == null)
            {
                result.Errors.Add("Missing Entities element.");
                return result;
            }

            foreach (var entityElement in entitiesElement.Elements("Entity"))
            {
                var entityName = GetAttributeValue(entityElement, "name");
                ValidateIdentifier(entityName, "Entity name", result.Errors);

                var entity = new Entity
                {
                    Name = entityName
                };

                EnsureProperties(entity, entityElement.Element("Properties"), result.Errors);

                model.Entities.Add(entity);
                if (!string.IsNullOrWhiteSpace(entity.Name))
                {
                    if (entityLookup.ContainsKey(entity.Name))
                    {
                        result.Errors.Add($"Duplicate entity name '{entity.Name}'. Entity names must be unique.");
                    }

                    entityLookup[entity.Name] = entity;
                }
            }

            // Second pass to resolve entity relationships.
            foreach (var entityElement in entitiesElement.Elements("Entity"))
            {
                var entityName = GetAttributeValue(entityElement, "name");
                if (string.IsNullOrEmpty(entityName))
                {
                    continue;
                }

                Entity entity;
                if (!entityLookup.TryGetValue(entityName, out entity))
                {
                    continue;
                }

                var relationshipsElement = entityElement.Element("Relationships");
                if (relationshipsElement == null)
                {
                    continue;
                }

                foreach (var relationshipElement in relationshipsElement.Elements("Relationship"))
                {
                    var relationshipEntityName = GetAttributeValue(relationshipElement, "entity");
                    if (string.IsNullOrWhiteSpace(relationshipEntityName))
                    {
                        result.Errors.Add($"Relationship on entity '{entity.Name}' is missing required 'entity' attribute.");
                        continue;
                    }

                    ValidateIdentifier(
                        relationshipEntityName,
                        $"Relationship entity on '{entity.Name}'",
                        result.Errors);

                    Entity relatedEntity;
                    if (entityLookup.TryGetValue(relationshipEntityName, out relatedEntity))
                    {
                        if (entity.Relationship.Any(r =>
                            string.Equals(r.Entity, relatedEntity.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Errors.Add(
                                $"Duplicate relationship '{entity.Name}' -> '{relatedEntity.Name}'.");
                            continue;
                        }

                        entity.Relationship.Add(new RelationshipDefinition
                        {
                            Entity = relatedEntity.Name
                        });
                    }
                    else
                    {
                        result.Errors.Add($"Unknown relationship entity '{relationshipEntityName}' on entity '{entity.Name}'.");
                    }
                }
            }

            foreach (var entity in model.Entities)
            {
                NormalizeRelationshipProperties(entity);
            }

            ValidateNoRelationshipCycles(model, result.Errors);

            return result;
        }

        public ReadResult ReadFromDatabase(string connectionString, string schema = "dbo")
        {
            var result = new ReadResult();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                result.Errors.Add("Connection string is required.");
                return result;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var model = result.Model;
                model.Name = connection.Database;
                ValidateIdentifier(model.Name, "Database name", result.Errors);

                var entityLookup = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT TABLE_NAME
                                            FROM INFORMATION_SCHEMA.TABLES
                                            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = @schema
                                            ORDER BY TABLE_NAME";
                    command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var tableName = reader.GetString(0);
                            ValidateIdentifier(tableName, "Table name", result.Errors);
                            var entity = new Entity { Name = tableName };
                            model.Entities.Add(entity);

                            if (entityLookup.ContainsKey(tableName))
                            {
                                result.Errors.Add($"Duplicate table name '{tableName}' in schema '{schema}'.");
                            }

                            entityLookup[tableName] = entity;
                        }
                    }
                }

                foreach (var entity in model.Entities)
                {
                    LoadColumns(connection, schema, entity, result.Errors);
                }

                var relationships = LoadRelationships(connection, schema);
                foreach (var relationship in relationships)
                {
                    Entity sourceEntity;
                    Entity relatedEntity;
                    if (entityLookup.TryGetValue(relationship.SourceTable, out sourceEntity) &&
                        entityLookup.TryGetValue(relationship.RelatedTable, out relatedEntity))
                    {
                        var expectedSourceColumn = relatedEntity.Name + "Id";
                        if (!string.Equals(relationship.SourceColumn, expectedSourceColumn, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Errors.Add(
                                $"Relationship from '{sourceEntity.Name}' to '{relatedEntity.Name}' must use column '{expectedSourceColumn}'. Found '{relationship.SourceColumn}'.");
                            continue;
                        }

                        if (!sourceEntity.Relationship.Any(r =>
                            string.Equals(r.Entity, relatedEntity.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            sourceEntity.Relationship.Add(new RelationshipDefinition
                            {
                                Entity = relatedEntity.Name
                            });
                        }
                    }
                }

                foreach (var entity in model.Entities)
                {
                    NormalizeRelationshipProperties(entity);
                }

                ValidateNoRelationshipCycles(model, result.Errors);
            }

            return result;
        }

        private static bool ParseBool(
            XAttribute attribute,
            bool defaultValue,
            string attributeName,
            List<string> errors,
            string entityName,
            string propertyName)
        {
            if (attribute == null)
            {
                return defaultValue;
            }

            bool result;
            if (!bool.TryParse(attribute.Value, out result))
            {
                errors.Add(
                    $"Invalid boolean value '{attribute.Value}' for {attributeName} on property '{propertyName}' in entity '{entityName}'.");
                return defaultValue;
            }

            return result;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            var attribute = element.Attribute(attributeName);
            return attribute != null ? attribute.Value : string.Empty;
        }

        private static void LoadColumns(SqlConnection connection, string schema, Entity entity, List<string> errors)
        {
            var columns = new List<Property>();
            var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
                                        FROM INFORMATION_SCHEMA.COLUMNS
                                        WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                                        ORDER BY ORDINAL_POSITION";
                command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
                command.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = entity.Name });

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader.GetString(0);
                        ValidateIdentifier(columnName, $"Column name on table '{entity.Name}'", errors);
                        var isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);

                        if (!propertyNames.Add(columnName))
                        {
                            errors.Add($"Duplicate column '{columnName}' found on table '{entity.Name}'.");
                            continue;
                        }

                        var property = new Property
                        {
                            Name = columnName,
                            DataType = "string",
                            IsNullable = isNullable
                        };

                        columns.Add(property);
                    }
                }
            }

            // Ensure Id property exists and is first.
            var hasId = false;
            for (int i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i].Name, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    hasId = true;
                    columns[i].IsNullable = false;
                    if (i != 0)
                    {
                        var idColumn = columns[i];
                        columns.RemoveAt(i);
                        columns.Insert(0, idColumn);
                    }
                    break;
                }
            }

            if (!hasId)
            {
                columns.Insert(0, new Property
                {
                    Name = "Id",
                    DataType = "string",
                    IsNullable = false
                });
            }

            entity.Properties.Clear();
            entity.Properties.AddRange(columns);
        }

        private static List<DatabaseRelationshipRow> LoadRelationships(SqlConnection connection, string schema)
        {
            var relationships = new List<DatabaseRelationshipRow>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        fk.TABLE_NAME AS SourceTable,
                        fk.COLUMN_NAME AS SourceColumn,
                        pk.TABLE_NAME AS RelatedTable
                    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE fk ON rc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk ON rc.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
                    WHERE fk.TABLE_SCHEMA = @schema AND pk.TABLE_SCHEMA = @schema
                    ORDER BY fk.TABLE_NAME, fk.ORDINAL_POSITION";
                command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        relationships.Add(new DatabaseRelationshipRow
                        {
                            SourceTable = reader.GetString(0),
                            SourceColumn = reader.GetString(1),
                            RelatedTable = reader.GetString(2)
                        });
                    }
                }
            }

            return relationships;
        }

        private static void EnsureProperties(Entity entity, XElement propertiesElement, List<string> errors)
        {
            var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (propertiesElement != null)
            {
                foreach (var propertyElement in propertiesElement.Elements("Property"))
                {
                    var propertyName = GetAttributeValue(propertyElement, "name");
                    ValidateIdentifier(propertyName, $"Property name on entity '{entity.Name}'", errors);
                    if (string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Entity '{entity.Name}' must not define explicit property 'Id'. It is implicit.");
                        continue;
                    }

                    if (propertyElement.Attribute("isNullable") != null)
                    {
                        errors.Add(
                            $"Property '{entity.Name}.{propertyName}' uses unsupported attribute 'isNullable'. Use 'isRequired'.");
                    }

                    var isRequiredAttribute = propertyElement.Attribute("isRequired");
                    var isRequired = ParseBool(
                        isRequiredAttribute,
                        defaultValue: true,
                        attributeName: "isRequired",
                        errors,
                        entity.Name,
                        propertyName);
                    var dataType = GetAttributeValue(propertyElement, "dataType");
                    if (string.IsNullOrWhiteSpace(dataType))
                    {
                        dataType = "string";
                    }

                    if (!propertyNames.Add(propertyName))
                    {
                        errors.Add($"Duplicate property '{propertyName}' on entity '{entity.Name}'.");
                        continue;
                    }

                    entity.Properties.Add(new Property
                    {
                        Name = propertyName,
                        DataType = dataType,
                        IsNullable = !isRequired
                    });
                }
            }

            if (!entity.Properties.Any(property => string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)))
            {
                entity.Properties.Insert(0, new Property
                {
                    Name = "Id",
                    DataType = "string",
                    IsNullable = false
                });
            }
        }

        private static void ValidateNoRelationshipCycles(Model model, List<string> errors)
        {
            var entityLookup = model.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var path = new List<string>();

            foreach (var entityName in entityLookup.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Visit(entityName);
            }

            void Visit(string entityName)
            {
                if (state.TryGetValue(entityName, out var currentState))
                {
                    if (currentState == 1)
                    {
                        var cycleStart = path.FindIndex(n => string.Equals(n, entityName, StringComparison.OrdinalIgnoreCase));
                        var cycle = cycleStart >= 0
                            ? path.Skip(cycleStart).Concat(new[] { entityName })
                            : new[] { entityName };
                        errors.Add("Relationship cycle detected: " + string.Join(" -> ", cycle) + ".");
                    }

                    return;
                }

                state[entityName] = 1;
                path.Add(entityName);

                var entity = entityLookup[entityName];
                foreach (var relationship in entity.Relationship)
                {
                    if (relationship == null || string.IsNullOrWhiteSpace(relationship.Entity))
                    {
                        continue;
                    }

                    if (!entityLookup.ContainsKey(relationship.Entity))
                    {
                        continue;
                    }

                    Visit(relationship.Entity);
                }

                path.RemoveAt(path.Count - 1);
                state[entityName] = 2;
            }
        }

        private static void ValidateIdentifier(string value, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{label} is required.");
                return;
            }

            if (value.Length > MaxIdentifierLength)
            {
                errors.Add($"{label} '{value}' exceeds max length {MaxIdentifierLength}.");
            }

            if (!IdentifierPattern.IsMatch(value))
            {
                errors.Add(
                    $"{label} '{value}' is invalid. Use [A-Za-z_][A-Za-z0-9_]* and max length {MaxIdentifierLength}.");
            }
        }

        private static void NormalizeRelationshipProperties(Entity entity)
        {
            if (entity == null || entity.Relationship == null || entity.Relationship.Count == 0)
            {
                return;
            }

            var relationshipPropertyNames = entity.Relationship
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Entity))
                .Select(r => r.Entity + "Id")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            entity.Properties.RemoveAll(p =>
                p != null &&
                !string.IsNullOrWhiteSpace(p.Name) &&
                !string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase) &&
                relationshipPropertyNames.Contains(p.Name));
        }

        private class DatabaseRelationshipRow
        {
            public string SourceTable { get; set; } = string.Empty;
            public string SourceColumn { get; set; } = string.Empty;
            public string RelatedTable { get; set; } = string.Empty;
        }
    }
}
