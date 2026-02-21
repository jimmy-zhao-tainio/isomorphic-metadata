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
            return "string";
        }
    }
}
