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

            if (entity.Properties.Count > 0)
            {
                element.Add(new XElement("Properties",
                    entity.Properties.ConvertAll(CreatePropertyElement)));
            }

            if (entity.Relationship.Count > 0)
            {
                element.Add(new XElement("Relationships",
                    entity.Relationship.ConvertAll(related =>
                        new XElement("Relationship",
                            new XAttribute("target", related.Name ?? string.Empty)))));
            }

            return element;
        }

        private static XElement CreatePropertyElement(Property property)
        {
            return new XElement("Property",
                new XAttribute("name", property.Name ?? string.Empty),
                new XAttribute("dataType", property.DataType ?? string.Empty),
                new XAttribute("isNullable", property.IsNullable.ToString().ToLowerInvariant()));
        }
    }
}
