using System;
using System.Collections.Generic;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    internal static class CSharpGenerationUtilities
    {
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
            if (property == null)
            {
                return "string";
            }

            var dataType = property.DataType ?? string.Empty;
            if (string.Equals(dataType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return property.IsNullable ? "bool?" : "bool";
            }

            return "string";
        }
    }
}
