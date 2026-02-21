using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Generic
{
    public class DatabaseInstanceReader
    {
        public ModelInstance Read(string connectionString, Model model, string schema = "dbo")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var instance = new ModelInstance { Model = model };
            var entityLookup = new Dictionary<string, EntityInstance>(StringComparer.OrdinalIgnoreCase);
            var modelEntityLookup = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var modelEntity in model.Entities)
            {
                if (modelEntity != null && !string.IsNullOrWhiteSpace(modelEntity.Name))
                {
                    modelEntityLookup[modelEntity.Name] = modelEntity;
                }
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (var entity in model.Entities)
                {
                    if (string.IsNullOrWhiteSpace(entity.Name))
                    {
                        continue;
                    }

                    var entityInstance = new EntityInstance { Entity = entity };
                    instance.Entities.Add(entityInstance);
                    entityLookup[entity.Name] = entityInstance;

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"SELECT * FROM [{schema}].[{entity.Name}] ORDER BY [Id]";
                        using (var reader = command.ExecuteReader())
                        {
                            var columnCount = reader.FieldCount;
                            var columnNames = new string[columnCount];
                            for (int i = 0; i < columnCount; i++)
                            {
                                columnNames[i] = reader.GetName(i);
                            }

                            var hasIdColumn = reader.ColumnExists("Id");
                            if (!hasIdColumn)
                            {
                                throw new InvalidOperationException(
                                    $"Table '{schema}.{entity.Name}' does not include an 'Id' column. This framework requires 'Id' for instance hydration.");
                            }

                            var seenRecordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            while (reader.Read())
                            {
                                var record = new RecordInstance();
                                if (reader["Id"] == DBNull.Value)
                                {
                                    throw new InvalidOperationException(
                                        $"Table '{schema}.{entity.Name}' contains a null Id value.");
                                }

                                record.Id = Convert.ToString(reader["Id"]) ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(record.Id))
                                {
                                    throw new InvalidOperationException(
                                        $"Table '{schema}.{entity.Name}' contains an empty Id value.");
                                }

                                int parsedRecordId;
                                if (!int.TryParse(record.Id, out parsedRecordId) || parsedRecordId <= 0)
                                {
                                    throw new InvalidOperationException(
                                        $"Table '{schema}.{entity.Name}' contains non-numeric Id '{record.Id}'.");
                                }

                                if (!seenRecordIds.Add(record.Id))
                                {
                                    throw new InvalidOperationException(
                                        $"Table '{schema}.{entity.Name}' contains duplicate Id '{record.Id}'.");
                                }

                                foreach (var property in entity.Properties)
                                {
                                    if (property == null || string.IsNullOrWhiteSpace(property.Name))
                                    {
                                        continue;
                                    }

                                    if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }

                                    var columnName = property.Name;
                                    if (!reader.ColumnExists(columnName))
                                    {
                                        continue;
                                    }

                                    var value = reader[columnName];
                                    if (value == DBNull.Value)
                                    {
                                        continue;
                                    }

                                    record.Properties.Add(new PropertyValue
                                    {
                                        Property = property,
                                        Value = Convert.ToString(value) ?? string.Empty
                                    });
                                }

                                if (entity.Relationship != null)
                                {
                                    foreach (var relationship in entity.Relationship)
                                    {
                                        if (relationship == null || string.IsNullOrWhiteSpace(relationship.Entity))
                                        {
                                            continue;
                                        }

                                        if (!modelEntityLookup.TryGetValue(relationship.Entity, out var relatedEntity))
                                        {
                                            continue;
                                        }

                                        var columnName = $"{relatedEntity.Name}Id";

                                        if (!reader.ColumnExists(columnName))
                                        {
                                            continue;
                                        }

                                        var fkValue = reader[columnName];
                                        if (fkValue == DBNull.Value)
                                        {
                                            continue;
                                        }

                                        var relationshipIdText = Convert.ToString(fkValue) ?? string.Empty;
                                        int relationshipId;
                                        if (!int.TryParse(relationshipIdText, out relationshipId) || relationshipId <= 0)
                                        {
                                            throw new InvalidOperationException(
                                                $"Relationship column '{schema}.{entity.Name}.{columnName}' has invalid value '{relationshipIdText}' for record Id '{record.Id}'.");
                                        }

                                        record.Relationships.Add(new RelationshipValue
                                        {
                                            Entity = relatedEntity,
                                            Value = relationshipIdText
                                        });
                                    }
                                }

                                entityInstance.Records.Add(record);
                            }
                        }
                    }
                }
            }

            return instance;
        }
    }

    internal static class SqlDataReaderExtensions
    {
        public static bool ColumnExists(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
