using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Metadata.Framework.Generic
{
    public class Reader
    {
        private static readonly HashSet<string> AllowedDataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string",
            "bool"
        };

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

            var entityLookup = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            var entitiesElement = modelElement.Element("Entities");
            if (entitiesElement == null)
            {
                result.Errors.Add("Missing Entities element.");
                return result;
            }

            foreach (var entityElement in entitiesElement.Elements("Entity"))
            {
                var entity = new Entity
                {
                    Name = GetAttributeValue(entityElement, "name")
                };

                EnsureProperties(entity, entityElement.Element("Properties"), result.Errors);

                model.Entities.Add(entity);
                if (!string.IsNullOrWhiteSpace(entity.Name))
                {
                    entityLookup[entity.Name] = entity;
                }
            }

            // Second pass to resolve entity relationships to shared instances.
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
                    var targetName = GetAttributeValue(relationshipElement, "target");
                    if (!string.IsNullOrEmpty(targetName))
                    {
                        Entity targetEntity;
                        if (entityLookup.TryGetValue(targetName, out targetEntity))
                        {
                            entity.Relationship.Add(targetEntity);
                        }
                    }
                }
            }

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
                            var entity = new Entity { Name = tableName };
                            model.Entities.Add(entity);
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
                    Entity source;
                    Entity target;
                    if (entityLookup.TryGetValue(relationship.SourceTable, out source) &&
                        entityLookup.TryGetValue(relationship.TargetTable, out target))
                    {
                        if (!source.Relationship.Contains(target))
                        {
                            source.Relationship.Add(target);
                        }
                    }
                }
            }

            return result;
        }

        private static bool ParseBool(XAttribute attribute, List<string> errors, string entityName, string propertyName)
        {
            if (attribute == null)
            {
                return false;
            }

            bool result;
            if (!bool.TryParse(attribute.Value, out result))
            {
                errors.Add($"Invalid boolean value '{attribute.Value}' for isNullable on property '{propertyName}' in entity '{entityName}'.");
                return false;
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
                        var sqlType = reader.GetString(1);
                        var isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);

                        var property = new Property
                        {
                            Name = columnName,
                            DataType = MapSqlTypeToGeneric(sqlType),
                            IsNullable = isNullable
                        };

                        if (!AllowedDataTypes.Contains(property.DataType))
                        {
                            errors.Add($"Column '{columnName}' on table '{entity.Name}' has unsupported data type '{sqlType}'.");
                        }

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

        private static List<RelationshipDefinition> LoadRelationships(SqlConnection connection, string schema)
        {
            var relationships = new List<RelationshipDefinition>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        fk.TABLE_NAME AS SourceTable,
                        fk.COLUMN_NAME AS SourceColumn,
                        pk.TABLE_NAME AS TargetTable
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
                        relationships.Add(new RelationshipDefinition
                        {
                            SourceTable = reader.GetString(0),
                            SourceColumn = reader.GetString(1),
                            TargetTable = reader.GetString(2)
                        });
                    }
                }
            }

            return relationships;
        }

        private static string MapSqlTypeToGeneric(string sqlType)
        {
            switch ((sqlType ?? string.Empty).ToLowerInvariant())
            {
                case "bit":
                    return "bool";
                default:
                    return "string";
            }
        }

        private static void EnsureProperties(Entity entity, XElement propertiesElement, List<string> errors)
        {
            var idPropertyCount = 0;
            if (propertiesElement != null)
            {
                foreach (var propertyElement in propertiesElement.Elements("Property"))
                {
                    var propertyName = GetAttributeValue(propertyElement, "name");
                    var dataType = GetAttributeValue(propertyElement, "dataType");
                    if (!string.IsNullOrEmpty(dataType) && !AllowedDataTypes.Contains(dataType))
                    {
                        errors.Add($"Unsupported data type '{dataType}' on property '{propertyName}' in entity '{entity.Name}'. Allowed types: string, bool.");
                    }

                    var isNullableAttribute = propertyElement.Attribute("isNullable");
                    bool isNullable = ParseBool(isNullableAttribute, errors, entity.Name, propertyName);

                    entity.Properties.Add(new Property
                    {
                        Name = propertyName,
                        DataType = dataType,
                        IsNullable = isNullable
                    });

                    if (string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase))
                    {
                        idPropertyCount++;
                        if (!string.Equals(dataType, "string", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Id property on entity '{entity.Name}' must have dataType 'string'.");
                        }
                        if (isNullable)
                        {
                            errors.Add($"Id property on entity '{entity.Name}' cannot be nullable.");
                        }
                    }
                }
            }

            if (idPropertyCount == 0)
            {
                entity.Properties.Insert(0, new Property
                {
                    Name = "Id",
                    DataType = "string",
                    IsNullable = false
                });
            }
            else if (idPropertyCount > 1)
            {
                errors.Add($"Entity '{entity.Name}' declares {idPropertyCount} properties named 'Id'; only one is allowed.");
            }
        }

        private class RelationshipDefinition
        {
            public string SourceTable { get; set; } = string.Empty;
            public string SourceColumn { get; set; } = string.Empty;
            public string TargetTable { get; set; } = string.Empty;
        }
    }
}
