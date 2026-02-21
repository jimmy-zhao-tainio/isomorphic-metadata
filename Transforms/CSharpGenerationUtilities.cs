using System.Collections.Generic;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    internal static class CSharpGenerationUtilities
    {
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while",
        };

        internal static string EnsureUniqueName(string baseName, HashSet<string> existingNames)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Item";
            }

            var candidate = baseName;
            var suffix = 1;
            while (!existingNames.Add(candidate))
            {
                suffix++;
                candidate = baseName + suffix;
            }

            return candidate;
        }

        internal static string GetClrType(Property property)
        {
            return "string";
        }

        internal static string ToListTypeName(string entityName)
        {
            return entityName + "s";
        }

        internal static string ToPluralName(Entity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(entity.Plural)
                ? (entity.Name ?? string.Empty) + "s"
                : entity.Plural;
        }

        internal static string ToCamelCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "value";
            }

            if (name.Length == 1)
            {
                return name.ToLowerInvariant();
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        internal static string EscapeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "_";
            }

            return CSharpKeywords.Contains(name) ? "@" + name : name;
        }

        internal static string GetCanonicalNamePropertyName(Entity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            if (entity.Properties.Exists(property =>
                property != null &&
                string.Equals(property.Name, "Name", System.StringComparison.OrdinalIgnoreCase)))
            {
                return string.Empty;
            }

            var candidate = entity.Name + "Name";
            return entity.Properties.Exists(property =>
                property != null &&
                string.Equals(property.Name, candidate, System.StringComparison.OrdinalIgnoreCase))
                ? candidate
                : string.Empty;
        }
    }
}
