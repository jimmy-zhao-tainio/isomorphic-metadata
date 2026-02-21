using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public static class XmlInstanceWriter
    {
        public static void Write(ModelInstance instance, string path)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            var rootName = !string.IsNullOrWhiteSpace(instance.Model?.Name)
                ? instance.Model.Name
                : "Model";
            var root = new XElement(rootName);
            foreach (var entityInstance in instance.Entities
                .OrderBy(e => e.Entity?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var collectionElement = CreateCollectionElement(entityInstance);
                if (collectionElement != null)
                {
                    root.Add(collectionElement);
                }
            }

            var document = new XDocument(root);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            document.Save(path);
        }

        private static XElement CreateCollectionElement(EntityInstance entityInstance)
        {
            var entityName = entityInstance.Entity?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return null;
            }

            var collectionElement = new XElement(entityName + "List");
            foreach (var record in entityInstance.Records
                .OrderBy(r => r.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                collectionElement.Add(CreateRecordElement(entityName, record));
            }

            return collectionElement;
        }

        private static XElement CreateRecordElement(string entityName, RecordInstance record)
        {
            var recordElement = new XElement(entityName,
                new XAttribute("Id", record.Id ?? string.Empty));
            var existingAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Id"
            };

            foreach (var property in record.Properties)
            {
                var propertyName = property.Property?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                if (!existingAttributes.Add(propertyName))
                {
                    continue;
                }

                recordElement.Add(new XAttribute(propertyName, property.Value ?? string.Empty));
            }

            foreach (var relationship in record.Relationships)
            {
                var relatedEntityName = relationship.Entity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(relatedEntityName))
                {
                    continue;
                }

                recordElement.Add(new XElement(relatedEntityName,
                    new XAttribute("Id", relationship.Value ?? string.Empty)));
            }

            recordElement.ReplaceNodes(recordElement.Nodes().OrderBy(node =>
            {
                var element = node as XElement;
                return element != null ? element.Name.LocalName : string.Empty;
            }, StringComparer.OrdinalIgnoreCase));

            return recordElement;
        }
    }
}
