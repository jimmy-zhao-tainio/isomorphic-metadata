using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Metadata.Framework.Generic
{
    public class InstanceReader
    {
        public InstanceReadResult Read(string path, Model model)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            using (var stream = File.OpenRead(path))
            {
                return Read(stream, model);
            }
        }

        public InstanceReadResult Read(Stream stream, Model model)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var result = new InstanceReadResult();
            var instance = result.ModelInstance;
            instance.Model = model;

            var document = XDocument.Load(stream);
            var root = document.Root;
            if (root == null)
            {
                result.Errors.Add("Missing root model element.");
                return result;
            }

            if (!string.IsNullOrWhiteSpace(model.Name) &&
                !string.Equals(model.Name, root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Model name mismatch. Expected '{model.Name}', found '{root.Name.LocalName}'.");
            }

            var entityLookup = model.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            var recordIdsByEntity = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entityDefinition in model.Entities.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
            {
                var entityInstance = new EntityInstance
                {
                    Entity = entityDefinition
                };

                var collectionElement = root.Element(entityDefinition.Name + "List");
                if (collectionElement == null)
                {
                    recordIdsByEntity[entityDefinition.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    instance.Entities.Add(entityInstance);
                    continue;
                }

                var propertyLookup = entityDefinition.Properties
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                var relationshipLookup = entityDefinition.Relationship
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Entity))
                    .ToDictionary(r => r.Entity, StringComparer.OrdinalIgnoreCase);
                var recordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var recordElement in collectionElement.Elements(entityDefinition.Name))
                {
                    var record = new RecordInstance
                    {
                        Id = GetAttributeValue(recordElement, "Id")
                    };
                    if (string.IsNullOrWhiteSpace(record.Id))
                    {
                        result.Errors.Add($"Record in entity '{entityDefinition.Name}' is missing required Id.");
                    }
                    else if (!recordIds.Add(record.Id))
                    {
                        result.Errors.Add($"Duplicate Id '{record.Id}' in entity '{entityDefinition.Name}'.");
                    }

                    foreach (var attribute in recordElement.Attributes())
                    {
                        var attributeName = attribute.Name.LocalName;
                        var attributeValue = attribute.Value;

                        if (string.Equals(attributeName, "Id", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!propertyLookup.TryGetValue(attributeName, out var propertyDefinition))
                        {
                            result.Errors.Add($"Unknown property '{attributeName}' for entity '{entityDefinition.Name}'.");
                            continue;
                        }

                        record.Properties.Add(new PropertyValue
                        {
                            Property = propertyDefinition,
                            Value = attributeValue
                        });
                    }

                    foreach (var propertyDefinition in entityDefinition.Properties)
                    {
                        if (propertyDefinition == null ||
                            string.IsNullOrWhiteSpace(propertyDefinition.Name) ||
                            string.Equals(propertyDefinition.Name, "Id", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (propertyDefinition.IsNullable)
                        {
                            continue;
                        }

                        var hasValue = record.Properties.Any(p =>
                            p.Property != null &&
                            string.Equals(p.Property.Name, propertyDefinition.Name, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(p.Value));

                        if (!hasValue)
                        {
                            result.Errors.Add(
                                $"Property '{propertyDefinition.Name}' on entity '{entityDefinition.Name}' is non-nullable and must have a value.");
                        }
                    }

                    var seenRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var relationshipElement in recordElement.Elements())
                    {
                        var relationshipEntityName = relationshipElement.Name.LocalName;
                        var relationshipId = GetAttributeValue(relationshipElement, "Id");

                        if (string.IsNullOrWhiteSpace(relationshipEntityName) ||
                            string.IsNullOrWhiteSpace(relationshipId))
                        {
                            result.Errors.Add($"Relationship element missing entity or Id in entity '{entityDefinition.Name}'.");
                            continue;
                        }

                        if (!entityLookup.TryGetValue(relationshipEntityName, out var relatedEntity))
                        {
                            result.Errors.Add($"Unknown relationship entity '{relationshipEntityName}' in entity '{entityDefinition.Name}'.");
                            continue;
                        }

                        if (!relationshipLookup.ContainsKey(relationshipEntityName))
                        {
                            result.Errors.Add($"Relationship entity '{relationshipEntityName}' not defined for entity '{entityDefinition.Name}'.");
                            continue;
                        }

                        if (!seenRelationships.Add(relationshipEntityName))
                        {
                            result.Errors.Add(
                                $"Duplicate relationship '{entityDefinition.Name}' -> '{relationshipEntityName}' on record '{record.Id}'.");
                            continue;
                        }

                        record.Relationships.Add(new RelationshipValue
                        {
                            Entity = relatedEntity,
                            Value = relationshipId
                        });
                    }

                    foreach (var expectedRelationship in entityDefinition.Relationship)
                    {
                        if (expectedRelationship == null || string.IsNullOrWhiteSpace(expectedRelationship.Entity))
                        {
                            continue;
                        }

                        if (!seenRelationships.Contains(expectedRelationship.Entity))
                        {
                            result.Errors.Add(
                                $"Missing required relationship '{entityDefinition.Name}' -> '{expectedRelationship.Entity}' on record '{record.Id}'.");
                        }
                    }

                    entityInstance.Records.Add(record);
                }

                entityInstance.Records.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.Id ?? string.Empty, right.Id ?? string.Empty));
                recordIdsByEntity[entityDefinition.Name] = recordIds;
                instance.Entities.Add(entityInstance);
            }

            ValidateRelationshipReferences(instance, recordIdsByEntity, result.Errors);

            return result;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            var attribute = element.Attribute(attributeName);
            return attribute != null ? attribute.Value : string.Empty;
        }

        private static void ValidateRelationshipReferences(
            ModelInstance instance,
            IDictionary<string, HashSet<string>> recordIdsByEntity,
            List<string> errors)
        {
            foreach (var entityInstance in instance.Entities)
            {
                var sourceEntityName = entityInstance.Entity?.Name ?? string.Empty;
                foreach (var record in entityInstance.Records)
                {
                    foreach (var relationship in record.Relationships)
                    {
                        var relatedEntityName = relationship.Entity?.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(relatedEntityName))
                        {
                            continue;
                        }

                        if (!recordIdsByEntity.TryGetValue(relatedEntityName, out var relatedRecordIds))
                        {
                            errors.Add(
                                $"Relationship '{sourceEntityName}' -> '{relatedEntityName}' references unknown entity.");
                            continue;
                        }

                        if (!relatedRecordIds.Contains(relationship.Value))
                        {
                            errors.Add(
                                $"Relationship '{sourceEntityName}' -> '{relatedEntityName}' on record '{record.Id}' references missing Id '{relationship.Value}'.");
                        }
                    }
                }
            }
        }
    }
}
