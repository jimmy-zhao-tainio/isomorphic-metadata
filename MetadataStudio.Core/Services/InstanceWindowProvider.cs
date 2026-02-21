using System;
using System.Collections.Generic;
using System.Linq;
using MetadataStudio.Core.Domain;

namespace MetadataStudio.Core.Services;

public sealed class InstanceWindowProvider
{
    private readonly Dictionary<string, EntityWindowCache> _cache = new(StringComparer.OrdinalIgnoreCase);

    public EntityWindowResult GetWindow(InstanceStore instance, string entityName, int offset, int pageSize)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            return new EntityWindowResult(entityName ?? string.Empty, 0, 0, pageSize <= 0 ? 1 : pageSize, []);
        }

        if (pageSize <= 0)
        {
            throw new InvalidOperationException("Page size must be greater than 0.");
        }

        if (!instance.RecordsByEntity.TryGetValue(entityName, out var records))
        {
            return new EntityWindowResult(entityName, 0, 0, pageSize, []);
        }

        var ordered = GetOrderedRecords(entityName, records);
        var totalCount = ordered.Count;
        if (totalCount == 0)
        {
            return new EntityWindowResult(entityName, 0, 0, pageSize, []);
        }

        var maxOffset = Math.Max(0, totalCount - pageSize);
        var effectiveOffset = Math.Clamp(offset, 0, maxOffset);
        var page = ordered.Skip(effectiveOffset).Take(pageSize).ToList();
        return new EntityWindowResult(entityName, totalCount, effectiveOffset, pageSize, page);
    }

    public void InvalidateEntity(string entityName)
    {
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            _cache.Remove(entityName);
        }
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }

    private IReadOnlyList<InstanceRecord> GetOrderedRecords(string entityName, List<InstanceRecord> records)
    {
        if (_cache.TryGetValue(entityName, out var existing))
        {
            if (ReferenceEquals(existing.Source, records) && existing.Count == records.Count)
            {
                return existing.Ordered;
            }
        }

        var ordered = records
            .OrderBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _cache[entityName] = new EntityWindowCache(records, records.Count, ordered);
        return ordered;
    }

    private sealed class EntityWindowCache
    {
        public EntityWindowCache(List<InstanceRecord> source, int count, IReadOnlyList<InstanceRecord> ordered)
        {
            Source = source;
            Count = count;
            Ordered = ordered;
        }

        public List<InstanceRecord> Source { get; }
        public int Count { get; }
        public IReadOnlyList<InstanceRecord> Ordered { get; }
    }
}

public sealed class EntityWindowResult
{
    public EntityWindowResult(
        string entityName,
        int totalCount,
        int offset,
        int pageSize,
        IReadOnlyList<InstanceRecord> rows)
    {
        EntityName = entityName;
        TotalCount = totalCount;
        Offset = offset;
        PageSize = pageSize;
        Rows = rows;
    }

    public string EntityName { get; }
    public int TotalCount { get; }
    public int Offset { get; }
    public int PageSize { get; }
    public IReadOnlyList<InstanceRecord> Rows { get; }
}
