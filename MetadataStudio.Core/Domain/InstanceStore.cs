using System;
using System.Collections.Generic;

namespace MetadataStudio.Core.Domain;

public sealed class InstanceStore
{
    public string ModelName { get; set; } = string.Empty;
    public Dictionary<string, List<InstanceRecord>> RecordsByEntity { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<InstanceRecord> GetOrCreateEntityRecords(string entityName)
    {
        if (!RecordsByEntity.TryGetValue(entityName, out var records))
        {
            records = new List<InstanceRecord>();
            RecordsByEntity[entityName] = records;
        }

        return records;
    }
}

public sealed class InstanceRecord
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RelationshipIds { get; } = new(StringComparer.OrdinalIgnoreCase);
}
