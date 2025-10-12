using System;
using System.Collections.Generic;
using System.Text;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public class ModelToCSharpConverter
    {
        public string Generate(Model model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine("namespace GeneratedModel");
            builder.AppendLine("{");

            AppendModelClass(model, builder);

            foreach (var entity in model.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                AppendEntityClass(entity, builder);
            }

            builder.AppendLine("}");

            return builder.ToString();
        }

        private static void AppendModelClass(Model model, StringBuilder builder)
        {
            builder.AppendLine("    public class Model");
            builder.AppendLine("    {");
            builder.AppendLine("        public Model()");
            builder.AppendLine("        {");
            foreach (var entity in model.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                builder.AppendLine($"            {entity.Name} = new List<{entity.Name}>();");
            }
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public string Name { get; set; }");
            foreach (var entity in model.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                builder.AppendLine($"        public List<{entity.Name}> {entity.Name} {{ get; set; }}");
            }
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendEntityClass(Entity entity, StringBuilder builder)
        {
            builder.AppendLine($"    public class {entity.Name}");
            builder.AppendLine("    {");

            var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entity.Properties)
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                memberNames.Add(property.Name);
                var propertyType = CSharpGenerationUtilities.GetClrType(property);
                builder.AppendLine($"        public {propertyType} {property.Name} {{ get; set; }}");
            }

            AppendRelationshipMembers(entity, builder, memberNames);

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendRelationshipMembers(Entity entity, StringBuilder builder, HashSet<string> memberNames)
        {
            if (entity.Relationship == null || entity.Relationship.Count == 0)
            {
                return;
            }

            foreach (var relatedEntity in entity.Relationship)
            {
                if (relatedEntity == null || string.IsNullOrWhiteSpace(relatedEntity.Name))
                {
                    continue;
                }

                var propertyName = CSharpGenerationUtilities.EnsureUniqueName(relatedEntity.Name, memberNames);
                builder.AppendLine($"        public {relatedEntity.Name} {propertyName} {{ get; set; }}");
            }
        }
    }
}
