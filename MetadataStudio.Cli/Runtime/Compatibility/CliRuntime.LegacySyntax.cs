internal sealed partial class CliRuntime
{
    string BuildEntityRowAddress(string entityName, string id)
    {
        return $"{entityName} {QuoteRowId(id)}";
    }

    string QuoteRowId(string id)
    {
        var value = id ?? string.Empty;
        if (value.IndexOfAny([' ', '\t', '"']) >= 0)
        {
            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }
    
        return value;
    }

    bool ContainsLegacyRowReferenceSyntax(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
    
        var trimmed = value.Trim();
        if (trimmed.Contains('#', StringComparison.Ordinal) ||
            trimmed.Contains('[', StringComparison.Ordinal) ||
            trimmed.Contains(']', StringComparison.Ordinal))
        {
            return true;
        }
    
        return Regex.IsMatch(
            trimmed,
            @"^[A-Za-z_][A-Za-z0-9_]*:",
            RegexOptions.CultureInvariant);
    }

    bool TryGetRelationshipId(InstanceRecord record, string relationshipEntity, out string relationshipId)
    {
        if (record.RelationshipIds.TryGetValue(relationshipEntity, out var directValue) &&
            !string.IsNullOrWhiteSpace(directValue))
        {
            relationshipId = directValue;
            return true;
        }
    
        foreach (var pair in record.RelationshipIds)
        {
            if (string.Equals(pair.Key, relationshipEntity, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                relationshipId = pair.Value;
                return true;
            }
        }
    
        relationshipId = string.Empty;
        return false;
    }
}
