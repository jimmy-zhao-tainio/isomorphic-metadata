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
            var root = document.Element("ModelInstance");
            if (root == null)
            {
                result.Errors.Add("Missing ModelInstance element.");
                return result;
            }

            var modelName = GetAttributeValue(root, "modelName");
            if (!string.IsNullOrWhiteSpace(model.Name) &&
                !string.IsNullOrWhiteSpace(modelName) &&
                !string.Equals(model.Name, modelName, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Model name mismatch. Expected '{model.Name}', found '{modelName}'.");
            }

            var entitiesElement = root.Element("Entities");
            if (entitiesElement == null)
            {
                result.Errors.Add("Missing Entities element.");
                return result;
            }

            var entityLookup = model.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var entityElement in entitiesElement.Elements("Entity"))
            {
                var entityName = GetAttributeValue(entityElement, "name");
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    result.Errors.Add("Entity element missing name attribute.");
                    continue;
                }

                if (!entityLookup.TryGetValue(entityName, out var entityDefinition))
                {
                    result.Errors.Add($"Unknown entity '{entityName}' in instance document.");
                    continue;
                }

                var entityInstance = new EntityInstance
                {
                    Entity = entityDefinition
                };

                var propertyLookup = entityDefinition.Properties
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var recordElement in entityElement.Elements("Record"))
                {
                    var record = new RecordInstance
                    {
                        Id = GetAttributeValue(recordElement, "id")
                    };

                    foreach (var propertyElement in recordElement.Elements("Property"))
                    {
                        var propertyName = GetAttributeValue(propertyElement, "name");
                        var propertyValue = GetAttributeValue(propertyElement, "value");

                        if (string.IsNullOrWhiteSpace(propertyName))
                        {
                            result.Errors.Add($"Property element missing name attribute in entity '{entityName}'.");
                            continue;
                        }

                        if (!propertyLookup.TryGetValue(propertyName, out var propertyDefinition))
                        {
                            result.Errors.Add($"Unknown property '{propertyName}' for entity '{entityName}'.");
                            continue;
                        }

                        record.Properties.Add(new PropertyValue
                        {
                            Property = propertyDefinition,
                            Value = propertyValue
                        });
                    }

                    foreach (var relationshipElement in recordElement.Elements("Relationship"))
                    {
                        var relationshipName = GetAttributeValue(relationshipElement, "name");
                        var targetIdentifier = GetAttributeValue(relationshipElement, "target");

                        if (string.IsNullOrWhiteSpace(relationshipName) ||
                            string.IsNullOrWhiteSpace(targetIdentifier))
                        {
                            result.Errors.Add($"Relationship element missing name or target in entity '{entityName}'.");
                            continue;
                        }

                        if (!entityLookup.TryGetValue(relationshipName, out var targetEntity))
                        {
                            targetEntity = entityDefinition.Relationship
                                .FirstOrDefault(r => string.Equals(r.Name, relationshipName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (targetEntity == null)
                        {
                            result.Errors.Add($"Relationship target '{relationshipName}' not defined for entity '{entityName}'.");
                            continue;
                        }

                        record.Relationships.Add(new RelationshipValue
                        {
                            Entity = targetEntity,
                            Value = targetIdentifier
                        });
                    }

                    entityInstance.Records.Add(record);
                }

                instance.Entities.Add(entityInstance);
            }

            return result;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            var attribute = element.Attribute(attributeName);
            return attribute != null ? attribute.Value : string.Empty;
        }
    }
}
