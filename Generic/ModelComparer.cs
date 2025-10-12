using System;
using System.Collections.Generic;
using System.Linq;

namespace Metadata.Framework.Generic
{
    public class ModelComparer
    {
        public ModelComparisonResult Compare(Model original, Model updated)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            if (updated == null)
            {
                throw new ArgumentNullException(nameof(updated));
            }

            var result = new ModelComparisonResult();

            var originalEntities = original.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            var updatedEntities = updated.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var entityName in updatedEntities.Keys)
            {
                if (!originalEntities.ContainsKey(entityName))
                {
                    result.AddedEntities.Add(entityName);
                }
            }

            foreach (var entityName in originalEntities.Keys)
            {
                if (!updatedEntities.ContainsKey(entityName))
                {
                    result.RemovedEntities.Add(entityName);
                }
            }

            foreach (var entityName in updatedEntities.Keys.Intersect(originalEntities.Keys, StringComparer.OrdinalIgnoreCase))
            {
                CompareEntity(originalEntities[entityName], updatedEntities[entityName], entityName, result);
            }

            return result;
        }

        private static void CompareEntity(Entity original, Entity updated, string entityName, ModelComparisonResult result)
        {
            var originalProperties = original.Properties
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var updatedProperties = updated.Properties
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var propertyName in updatedProperties.Keys)
            {
                if (!originalProperties.ContainsKey(propertyName))
                {
                    result.AddedProperties.Add($"{entityName}.{propertyName}");
                }
            }

            foreach (var propertyName in originalProperties.Keys)
            {
                if (!updatedProperties.ContainsKey(propertyName))
                {
                    result.RemovedProperties.Add($"{entityName}.{propertyName}");
                }
            }

            foreach (var propertyName in updatedProperties.Keys.Intersect(originalProperties.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var originalProperty = originalProperties[propertyName];
                var updatedProperty = updatedProperties[propertyName];

                if (!string.Equals(originalProperty.DataType, updatedProperty.DataType, StringComparison.OrdinalIgnoreCase) ||
                    originalProperty.IsNullable != updatedProperty.IsNullable)
                {
                    result.ChangedProperties.Add($"{entityName}.{propertyName}");
                }
            }

            var originalRelationships = original.Relationship
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => r.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var updatedRelationships = updated.Relationship
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => r.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var relationship in updatedRelationships)
            {
                if (!originalRelationships.Contains(relationship))
                {
                    result.AddedRelationships.Add($"{entityName}->{relationship}");
                }
            }

            foreach (var relationship in originalRelationships)
            {
                if (!updatedRelationships.Contains(relationship))
                {
                    result.RemovedRelationships.Add($"{entityName}->{relationship}");
                }
            }
        }
    }

    public class ModelComparisonResult
    {
        public List<string> AddedEntities { get; } = new List<string>();
        public List<string> RemovedEntities { get; } = new List<string>();
        public List<string> AddedProperties { get; } = new List<string>();
        public List<string> RemovedProperties { get; } = new List<string>();
        public List<string> ChangedProperties { get; } = new List<string>();
        public List<string> AddedRelationships { get; } = new List<string>();
        public List<string> RemovedRelationships { get; } = new List<string>();

        public bool HasDifferences =>
            AddedEntities.Count > 0 ||
            RemovedEntities.Count > 0 ||
            AddedProperties.Count > 0 ||
            RemovedProperties.Count > 0 ||
            ChangedProperties.Count > 0 ||
            AddedRelationships.Count > 0 ||
            RemovedRelationships.Count > 0;
    }
}
