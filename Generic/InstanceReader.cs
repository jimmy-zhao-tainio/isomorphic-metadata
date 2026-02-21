
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

            if (Directory.Exists(path))
            {
                return ReadWorkspace(path, model);
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
            result.ModelInstance.Model = model;

            var document = XDocument.Load(stream);
            ParseMonolithicDocument(document, model, result);
            ValidateRelationshipReferences(result.ModelInstance, result.Errors);
            return result;
        }

        public InstanceReadResult ReadWorkspace(string workspacePath, Model model)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentException("Workspace path must not be null or empty.", nameof(workspacePath));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var fullWorkspacePath = Path.GetFullPath(workspacePath);
            var result = new InstanceReadResult();
            result.ModelInstance.Model = model;

            var shardDirectory = Path.Combine(fullWorkspacePath, "metadata", "instance");
            if (Directory.Exists(shardDirectory))
            {
                ParseShardedWorkspace(shardDirectory, model, result);
                ValidateRelationshipReferences(result.ModelInstance, result.Errors);
                return result;
            }

            var monolithicPath = ResolveMonolithicInstancePath(fullWorkspacePath);
            if (string.IsNullOrWhiteSpace(monolithicPath))
            {
                result.Errors.Add($"Could not find instance XML under workspace '{fullWorkspacePath}'.");
                return result;
            }

            var document = XDocument.Load(monolithicPath);
            ParseMonolithicDocument(document, model, result);
            ValidateRelationshipReferences(result.ModelInstance, result.Errors);
            return result;
        }

        private static void ParseShardedWorkspace(string shardDirectory, Model model, InstanceReadResult result)
        {
            foreach (var entityDefinition in model.Entities.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                var entityInstance = new EntityInstance { Entity = entityDefinition };
                result.ModelInstance.Entities.Add(entityInstance);

                var shardPath = Path.Combine(shardDirectory, entityDefinition.Name + ".xml");
                if (!File.Exists(shardPath))
                {
                    continue;
                }

                var document = XDocument.Load(shardPath);
                ParseEntityFromRoot(document.Root, entityDefinition, model, entityInstance, result.Errors);
                SortEntityRecordsById(entityInstance, entityDefinition.Name);
            }
        }

        private static void ParseMonolithicDocument(XDocument document, Model model, InstanceReadResult result)
        {
            var root = document.Root;
            if (root == null)
            {
                result.Errors.Add("Missing root model element.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.Name) &&
                !string.Equals(model.Name, root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Model name mismatch. Expected '{model.Name}', found '{root.Name.LocalName}'.");
            }

            foreach (var entityDefinition in model.Entities.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                var entityInstance = new EntityInstance { Entity = entityDefinition };
                result.ModelInstance.Entities.Add(entityInstance);
                ParseEntityFromRoot(root, entityDefinition, model, entityInstance, result.Errors);
                SortEntityRecordsById(entityInstance, entityDefinition.Name);
            }
        }

        private static void ParseEntityFromRoot(
            XElement root,
            Entity entityDefinition,
            Model model,
            EntityInstance entityInstance,
            List<string> errors)
        {
            if (root == null)
            {
                errors.Add($"Missing root model element in shard for entity '{entityDefinition.Name}'.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.Name) &&
                !string.Equals(model.Name, root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Model name mismatch for entity '{entityDefinition.Name}'. Expected '{model.Name}', found '{root.Name.LocalName}'.");
            }

            var collectionElements = GetCollectionElements(root, entityDefinition).ToList();
            if (collectionElements.Count == 0)
            {
                return;
            }

            var propertyLookup = entityDefinition.Properties
                .Where(property => property != null && !string.IsNullOrWhiteSpace(property.Name))
                .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
            var relationshipLookup = entityDefinition.Relationship
                .Where(relationship => relationship != null && !string.IsNullOrWhiteSpace(relationship.Entity))
                .ToDictionary(relationship => relationship.Entity, StringComparer.OrdinalIgnoreCase);
            var modelEntityLookup = model.Entities
                .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.Name))
                .ToDictionary(entity => entity.Name, StringComparer.OrdinalIgnoreCase);
            var recordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var collectionElement in collectionElements)
            {
                foreach (var rowElement in collectionElement.Elements(entityDefinition.Name))
                {
                    var record = new RecordInstance();
                    var idAttribute = rowElement.Attribute("Id");
                    record.Id = idAttribute != null ? idAttribute.Value : string.Empty;

                    if (!TryParsePositiveInt(record.Id, out _))
                    {
                        errors.Add($"Entity '{entityDefinition.Name}' contains invalid Id '{record.Id}'. Id must be a positive integer.");
                    }
                    else if (!recordIds.Add(record.Id))
                    {
                        errors.Add($"Duplicate Id '{record.Id}' in entity '{entityDefinition.Name}'.");
                    }

                    ParseAttributes(entityDefinition, rowElement, record, relationshipLookup, modelEntityLookup, errors);
                    ParsePropertyElements(entityDefinition, rowElement, record, propertyLookup, relationshipLookup, errors);
                    ValidateRequiredProperties(entityDefinition, record, errors);

                    entityInstance.Records.Add(record);
                }
            }
        }

        private static IEnumerable<XElement> GetCollectionElements(XElement root, Entity entityDefinition)
        {
            if (root == null || entityDefinition == null)
            {
                return Enumerable.Empty<XElement>();
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                entityDefinition.GetPluralName(),
            };

            var legacyName = entityDefinition.Name + "List";
            names.Add(legacyName);

            return root.Elements()
                .Where(element => names.Contains(element.Name.LocalName));
        }

        private static void ParseAttributes(
            Entity entityDefinition,
            XElement rowElement,
            RecordInstance record,
            IDictionary<string, RelationshipDefinition> relationshipLookup,
            IDictionary<string, Entity> modelEntityLookup,
            List<string> errors)
        {
            var seenRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attribute in rowElement.Attributes())
            {
                var attributeName = attribute.Name.LocalName;
                if (string.Equals(attributeName, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (attributeName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                {
                    var relationshipEntityName = attributeName.Substring(0, attributeName.Length - 2);
                    if (relationshipLookup.ContainsKey(relationshipEntityName))
                    {
                        if (!TryParsePositiveInt(attribute.Value, out _))
                        {
                            errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' has invalid relationship value '{attribute.Value}' for '{attributeName}'.");
                            continue;
                        }

                        if (!seenRelationships.Add(relationshipEntityName))
                        {
                            errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' has duplicate relationship attribute '{attributeName}'.");
                            continue;
                        }

                        Entity relatedEntity;
                        if (!modelEntityLookup.TryGetValue(relationshipEntityName, out relatedEntity))
                        {
                            errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' references unknown relationship entity '{relationshipEntityName}'.");
                            continue;
                        }

                        record.Relationships.Add(new RelationshipValue
                        {
                            Entity = relatedEntity,
                            Value = attribute.Value,
                        });

                        continue;
                    }
                }

                errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' uses unsupported attribute '{attributeName}'. Properties must be child elements.");
            }
        }

        private static void ParsePropertyElements(
            Entity entityDefinition,
            XElement rowElement,
            RecordInstance record,
            IDictionary<string, Property> propertyLookup,
            IDictionary<string, RelationshipDefinition> relationshipLookup,
            List<string> errors)
        {
            var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var childElement in rowElement.Elements())
            {
                var childName = childElement.Name.LocalName;
                if (relationshipLookup.ContainsKey(childName) ||
                    (childName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) &&
                     relationshipLookup.ContainsKey(childName.Substring(0, childName.Length - 2))))
                {
                    errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' contains relationship element '{childName}'. Relationships must be attributes.");
                    continue;
                }

                Property property;
                if (!propertyLookup.TryGetValue(childName, out property) || string.Equals(childName, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Unknown property element '{childName}' for entity '{entityDefinition.Name}'.");
                    continue;
                }

                if (!seenProperties.Add(childName))
                {
                    errors.Add($"Entity '{entityDefinition.Name}' row '{record.Id}' has duplicate property element '{childName}'.");
                    continue;
                }

                record.Properties.Add(new PropertyValue
                {
                    Property = property,
                    Value = childElement.Value,
                });
            }
        }

        private static void ValidateRequiredProperties(Entity entityDefinition, RecordInstance record, List<string> errors)
        {
            foreach (var property in entityDefinition.Properties)
            {
                if (property == null ||
                    string.IsNullOrWhiteSpace(property.Name) ||
                    string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) ||
                    property.IsNullable)
                {
                    continue;
                }

                var value = record.Properties.FirstOrDefault(item =>
                    item.Property != null &&
                    string.Equals(item.Property.Name, property.Name, StringComparison.OrdinalIgnoreCase));
                if (value == null || string.IsNullOrWhiteSpace(value.Value))
                {
                    errors.Add($"Property '{property.Name}' on entity '{entityDefinition.Name}' is required and must have a non-empty value.");
                }
            }
        }

        private static void ValidateRelationshipReferences(ModelInstance instance, List<string> errors)
        {
            var recordIdsByEntity = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in instance.Entities)
            {
                var entityName = entity.Entity != null ? entity.Entity.Name : string.Empty;
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    continue;
                }

                recordIdsByEntity[entityName] = entity.Records
                    .Where(record => !string.IsNullOrWhiteSpace(record.Id))
                    .Select(record => record.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var entity in instance.Entities)
            {
                var sourceEntityName = entity.Entity != null ? entity.Entity.Name : string.Empty;
                if (string.IsNullOrWhiteSpace(sourceEntityName))
                {
                    continue;
                }

                foreach (var record in entity.Records)
                {
                    foreach (var relationship in record.Relationships)
                    {
                        var targetEntityName = relationship.Entity != null ? relationship.Entity.Name : string.Empty;
                        if (string.IsNullOrWhiteSpace(targetEntityName))
                        {
                            continue;
                        }

                        HashSet<string> targetIds;
                        if (!recordIdsByEntity.TryGetValue(targetEntityName, out targetIds))
                        {
                            errors.Add($"Relationship '{sourceEntityName}->{targetEntityName}' references unknown target entity.");
                            continue;
                        }

                        if (!targetIds.Contains(relationship.Value))
                        {
                            errors.Add($"Relationship '{sourceEntityName}->{targetEntityName}' on row '{record.Id}' references missing Id '{relationship.Value}'.");
                        }
                    }
                }
            }
        }

        private static void SortEntityRecordsById(EntityInstance entityInstance, string entityName)
        {
            entityInstance.Records.Sort((left, right) =>
            {
                int leftId;
                int rightId;
                var leftParsed = TryParsePositiveInt(left.Id, out leftId);
                var rightParsed = TryParsePositiveInt(right.Id, out rightId);

                if (leftParsed && rightParsed)
                {
                    return leftId.CompareTo(rightId);
                }

                return StringComparer.OrdinalIgnoreCase.Compare(left.Id ?? string.Empty, right.Id ?? string.Empty);
            });
        }

        private static bool TryParsePositiveInt(string value, out int number)
        {
            return int.TryParse(value, out number) && number > 0;
        }

        private static string ResolveMonolithicInstancePath(string workspacePath)
        {
            var candidates = new[]
            {
                Path.Combine(workspacePath, "metadata", "instance.xml"),
                Path.Combine(workspacePath, "SampleInstance.xml"),
                Path.Combine(workspacePath, "instance.xml"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
