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
                        command.CommandText = $"SELECT * FROM [{schema}].[{entity.Name}]";
                        using (var reader = command.ExecuteReader())
                        {
                            var columnCount = reader.FieldCount;
                            var columnNames = new string[columnCount];
                            for (int i = 0; i < columnCount; i++)
                            {
                                columnNames[i] = reader.GetName(i);
                            }

                            while (reader.Read())
                            {
                                var record = new RecordInstance();
                                if (reader["Id"] != DBNull.Value)
                                {
                                    record.Id = Convert.ToString(reader["Id"]) ?? string.Empty;
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
                                    record.Properties.Add(new PropertyValue
                                    {
                                        Property = property,
                                        Value = value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty
                                    });
                                }

                                if (entity.Relationship != null)
                                {
                                    foreach (var relatedEntity in entity.Relationship)
                                    {
                                        if (relatedEntity == null || string.IsNullOrWhiteSpace(relatedEntity.Name))
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

                                        record.Relationships.Add(new RelationshipValue
                                        {
                                            Entity = relatedEntity,
                                            Value = Convert.ToString(fkValue) ?? string.Empty
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
