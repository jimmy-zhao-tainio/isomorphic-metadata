using System;
using System.IO;
using System.Xml.Linq;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public static class XmlModelWriter
    {
        public static void Write(Model model, string path)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            var document = new XDocument(
                new XElement("Model",
                    new XAttribute("name", model.Name ?? string.Empty),
                    new XElement("Entities",
                        model.Entities.ConvertAll(CreateEntityElement))));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            document.Save(path);
        }

        private static XElement CreateEntityElement(Entity entity)
        {
            var element = new XElement("Entity",
                new XAttribute("name", entity.Name ?? string.Empty));

            var properties = entity.Properties
                .FindAll(property => !string.Equals(property.Name, "Id", System.StringComparison.OrdinalIgnoreCase));
            if (properties.Count > 0)
            {
                element.Add(new XElement("Properties",
                    properties.ConvertAll(CreatePropertyElement)));
            }

            if (entity.Relationship.Count > 0)
            {
                element.Add(new XElement("Relationships",
                    entity.Relationship.ConvertAll(relationship =>
                        new XElement("Relationship",
                            new XAttribute("entity", relationship.Entity ?? string.Empty)))));
            }

            return element;
        }

        private static XElement CreatePropertyElement(Property property)
        {
            var element = new XElement("Property",
                new XAttribute("name", property.Name ?? string.Empty));

            var dataType = string.IsNullOrWhiteSpace(property.DataType) ? "string" : property.DataType;
            if (!string.Equals(dataType, "string", System.StringComparison.OrdinalIgnoreCase))
            {
                element.Add(new XAttribute("dataType", dataType));
            }

            if (property.IsNullable)
            {
                element.Add(new XAttribute("isRequired", "false"));
            }

            return element;
        }
    }
}
