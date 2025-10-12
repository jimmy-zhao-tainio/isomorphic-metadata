using System;
using System.IO;
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

            var document = new XDocument(
                new XElement("ModelInstance",
                    new XAttribute("modelName", instance.Model?.Name ?? string.Empty),
                    new XElement("Entities",
                        instance.Entities.ConvertAll(CreateEntityElement))));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            document.Save(path);
        }

        private static XElement CreateEntityElement(EntityInstance entityInstance)
        {
            var element = new XElement("Entity",
                new XAttribute("name", entityInstance.Entity?.Name ?? string.Empty));

            foreach (var record in entityInstance.Records)
            {
                element.Add(CreateRecordElement(record));
            }

            return element;
        }

        private static XElement CreateRecordElement(RecordInstance record)
        {
            var recordElement = new XElement("Record",
                new XAttribute("id", record.Id ?? string.Empty));

            foreach (var property in record.Properties)
            {
                recordElement.Add(new XElement("Property",
                    new XAttribute("name", property.Property?.Name ?? string.Empty),
                    new XAttribute("value", property.Value ?? string.Empty)));
            }

            foreach (var relationship in record.Relationships)
            {
                recordElement.Add(new XElement("Relationship",
                    new XAttribute("name", relationship.Entity?.Name ?? string.Empty),
                    new XAttribute("target", relationship.Value ?? string.Empty)));
            }

            return recordElement;
        }
    }
}
