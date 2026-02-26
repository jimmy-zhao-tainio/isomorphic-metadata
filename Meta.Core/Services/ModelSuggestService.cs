using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Meta.Core.Domain;

namespace Meta.Core.Services;

public sealed class ModelSuggestReport
{
    public string WorkspaceRootPath { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public List<BusinessKeyCandidate> BusinessKeys { get; } = new();
    public List<LookupRelationshipSuggestion> EligibleRelationshipSuggestions { get; } = new();
    public List<LookupRelationshipSuggestion> BlockedRelationshipCandidates { get; } = new();
}

public enum LookupCandidateStatus
{
    Eligible,
    Blocked,
}

public sealed class PropertyProfileStats
{
    public string EntityName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public bool IsStringLike { get; set; }
    public int RowCount { get; set; }
    public int NonNullCount { get; set; }
    public int NullCount { get; set; }
    public int BlankStringCount { get; set; }
    public int NonBlankCount { get; set; }
    public int DistinctNonNullCount { get; set; }
    public int DistinctNonBlankCount { get; set; }
    public bool IsUniqueOverNonNull { get; set; }
    public bool IsUniqueOverNonBlank { get; set; }
}

public sealed class BusinessKeyUsage
{
    public string SourceEntity { get; set; } = string.Empty;
    public string SourceProperty { get; set; } = string.Empty;
    public int SourceRowCount { get; set; }
    public int SourceDistinctNonBlankCount { get; set; }
    public int MatchedSourceRowCount { get; set; }
    public int SourceComparableRowCount { get; set; }
}

public sealed class BusinessKeyCandidate
{
    public PropertyProfileStats Target { get; set; } = new();
    public double Score { get; set; }
    public List<string> Reasons { get; } = new();
    public List<string> Blockers { get; } = new();
    public List<BusinessKeyUsage> UsedBy { get; } = new();
}

public sealed class LookupRelationshipSuggestion
{
    public string Kind { get; set; } = "PromotePropertyToRelationshipUsingLookupKey";
    public LookupCandidateStatus Status { get; set; }
    public PropertyProfileStats Source { get; set; } = new();
    public PropertyProfileStats TargetLookup { get; set; } = new();
    public double Score { get; set; }
    public int SourceComparableRowCount { get; set; }
    public int SourceDistinctComparableValueCount { get; set; }
    public int MatchedSourceRowCount { get; set; }
    public int MatchedDistinctSourceValueCount { get; set; }
    public int UnmatchedSourceRowCount { get; set; }
    public int UnmatchedDistinctValueCount { get; set; }
    public IReadOnlyList<string> UnmatchedDistinctValuesSample { get; set; } = Array.Empty<string>();
    public int TargetComparableRowCount { get; set; }
    public int TargetDistinctComparableValueCount { get; set; }
    public bool TargetComparableIsUnique { get; set; }
    public bool SourceShowsReuse { get; set; }
    public List<string> Evidence { get; } = new();
    public List<string> Blockers { get; } = new();
}

public static class ModelSuggestService
{
    public static ModelSuggestReport Analyze(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var model = workspace.Model ?? throw new InvalidDataException("Workspace model is missing.");
        var profiles = BuildPropertyProfiles(workspace);

        var businessKeys = BuildBusinessKeyCandidates(profiles);
        var relationshipCandidates = BuildLookupRelationshipCandidates(profiles);
        var eligible = relationshipCandidates
            .Where(item => item.Status == LookupCandidateStatus.Eligible)
            .ToList();
        var blocked = relationshipCandidates
            .Where(item => item.Status == LookupCandidateStatus.Blocked)
            .ToList();

        AttachBusinessKeyUsage(businessKeys, eligible);

        var report = new ModelSuggestReport
        {
            WorkspaceRootPath = workspace.WorkspaceRootPath ?? string.Empty,
            ModelName = model.Name ?? string.Empty,
        };
        report.BusinessKeys.AddRange(businessKeys);
        report.EligibleRelationshipSuggestions.AddRange(eligible);
        report.BlockedRelationshipCandidates.AddRange(blocked);
        return report;
    }

    private static List<PropertyProfile> BuildPropertyProfiles(Workspace workspace)
    {
        var model = workspace.Model ?? throw new InvalidDataException("Workspace model is missing.");
        var instance = workspace.Instance;
        var result = new List<PropertyProfile>();

        foreach (var entity in model.Entities
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Name, StringComparer.Ordinal))
        {
            var rows = instance?.RecordsByEntity.TryGetValue(entity.Name, out var entityRows) == true
                ? entityRows
                : new List<GenericRecord>();
            var orderedRows = rows
                .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            foreach (var property in entity.Properties
                         .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Name, StringComparer.Ordinal))
            {
                result.Add(ProfileProperty(entity.Name, property, orderedRows));
            }
        }

        return result;
    }

    private static PropertyProfile ProfileProperty(
        string entityName,
        GenericProperty property,
        IReadOnlyList<GenericRecord> rows)
    {
        var dataType = NormalizeDataType(property.DataType);
        var nonNullCount = 0;
        var blankCount = 0;
        var nonNullDistinct = new HashSet<string>(StringComparer.Ordinal);
        var nonBlankDistinct = new HashSet<string>(StringComparer.Ordinal);
        var nonBlankCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!row.Values.TryGetValue(property.Name, out var rawValue))
            {
                continue;
            }

            var value = rawValue ?? string.Empty;
            nonNullCount++;
            nonNullDistinct.Add(value);

            if (IsBlank(value))
            {
                blankCount++;
                continue;
            }

            nonBlankDistinct.Add(value);
            nonBlankCounts.TryGetValue(value, out var existing);
            nonBlankCounts[value] = existing + 1;
        }

        var rowCount = rows.Count;
        var nullCount = rowCount - nonNullCount;
        var nonBlankCount = nonNullCount - blankCount;

        return new PropertyProfile(
            Stats: new PropertyProfileStats
            {
                EntityName = entityName,
                PropertyName = property.Name,
                DataType = dataType,
                IsRequired = !property.IsNullable,
                IsStringLike = IsStringLike(dataType),
                RowCount = rowCount,
                NonNullCount = nonNullCount,
                NullCount = nullCount,
                BlankStringCount = blankCount,
                NonBlankCount = nonBlankCount,
                DistinctNonNullCount = nonNullDistinct.Count,
                DistinctNonBlankCount = nonBlankDistinct.Count,
                IsUniqueOverNonNull = nonNullCount > 0 && nonNullDistinct.Count == nonNullCount,
                IsUniqueOverNonBlank = nonBlankCount > 0 && nonBlankDistinct.Count == nonBlankCount,
            },
            ComparableValueCounts: nonBlankCounts);
    }

    private static List<BusinessKeyCandidate> BuildBusinessKeyCandidates(IReadOnlyList<PropertyProfile> profiles)
    {
        var candidates = new List<BusinessKeyCandidate>();
        foreach (var profile in profiles)
        {
            var stats = profile.Stats;
            if (stats.RowCount < 2 || !IsComparableScalar(stats.DataType) || stats.NonNullCount == 0)
            {
                continue;
            }

            if (!stats.IsUniqueOverNonNull)
            {
                continue;
            }

            var candidate = new BusinessKeyCandidate
            {
                Target = CloneStats(stats),
                Score = ScoreBusinessKey(stats),
            };
            candidate.Reasons.Add("Values are unique in target entity.");
            if (stats.NullCount == 0)
            {
                candidate.Reasons.Add("No null values.");
            }
            else
            {
                candidate.Blockers.Add("Contains null values.");
            }

            if (stats.BlankStringCount == 0)
            {
                candidate.Reasons.Add("No blank values.");
            }
            else
            {
                candidate.Blockers.Add("Contains blank values.");
            }

            if (!stats.IsRequired)
            {
                candidate.Reasons.Add("Property is optional in model definition.");
            }

            candidates.Add(candidate);
        }

        return candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Target.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target.EntityName, StringComparer.Ordinal)
            .ThenBy(item => item.Target.PropertyName, StringComparer.Ordinal)
            .ToList();
    }

    private static List<LookupRelationshipSuggestion> BuildLookupRelationshipCandidates(IReadOnlyList<PropertyProfile> profiles)
    {
        var profilesByPropertyName = profiles
            .GroupBy(item => item.Stats.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Stats.EntityName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Stats.EntityName, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var candidates = new List<LookupRelationshipSuggestion>();
        foreach (var source in profiles
                     .OrderBy(item => item.Stats.EntityName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Stats.PropertyName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Stats.EntityName, StringComparer.Ordinal)
                     .ThenBy(item => item.Stats.PropertyName, StringComparer.Ordinal))
        {
            var sourceStats = source.Stats;
            if (!profilesByPropertyName.TryGetValue(sourceStats.PropertyName, out var sameNameProfiles))
            {
                continue;
            }

            foreach (var target in sameNameProfiles)
            {
                var targetStats = target.Stats;
                if (string.Equals(sourceStats.EntityName, targetStats.EntityName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (sourceStats.RowCount < 2 || targetStats.RowCount < 2)
                {
                    continue;
                }

                if (sourceStats.DistinctNonBlankCount == 0 || targetStats.DistinctNonBlankCount == 0)
                {
                    continue;
                }

                var typeCompatible = IsTypeCompatibleStrict(sourceStats.DataType, targetStats.DataType);
                var coverage = BuildCoverageMetrics(source, target);
                var blockers = BuildRelationshipBlockers(sourceStats, targetStats, coverage, typeCompatible, target.ComparableValueCounts);

                var suggestion = new LookupRelationshipSuggestion
                {
                    Status = blockers.Count == 0 ? LookupCandidateStatus.Eligible : LookupCandidateStatus.Blocked,
                    Source = CloneStats(sourceStats),
                    TargetLookup = CloneStats(targetStats),
                    SourceComparableRowCount = sourceStats.NonBlankCount,
                    SourceDistinctComparableValueCount = sourceStats.DistinctNonBlankCount,
                    MatchedSourceRowCount = coverage.MatchedSourceRowCount,
                    MatchedDistinctSourceValueCount = coverage.MatchedDistinctCount,
                    UnmatchedSourceRowCount = coverage.UnmatchedSourceRowCount,
                    UnmatchedDistinctValueCount = coverage.UnmatchedDistinctCount,
                    UnmatchedDistinctValuesSample = coverage.UnmatchedDistinctSample,
                    TargetComparableRowCount = targetStats.NonBlankCount,
                    TargetDistinctComparableValueCount = targetStats.DistinctNonBlankCount,
                    TargetComparableIsUnique = targetStats.IsUniqueOverNonBlank,
                    SourceShowsReuse = sourceStats.NonBlankCount > sourceStats.DistinctNonBlankCount,
                    Score = ScoreRelationshipCandidate(sourceStats, targetStats, coverage, typeCompatible, blockers.Count),
                };
                suggestion.Blockers.AddRange(blockers);

                suggestion.Evidence.Add("Exact property-name match.");
                suggestion.Evidence.Add(typeCompatible
                    ? "Compatible scalar type."
                    : "Incompatible scalar type.");
                suggestion.Evidence.Add(
                    $"Source values matched target key: {coverage.MatchedSourceRowCount.ToString(CultureInfo.InvariantCulture)}/{sourceStats.NonBlankCount.ToString(CultureInfo.InvariantCulture)} rows (distinct {coverage.MatchedDistinctCount.ToString(CultureInfo.InvariantCulture)}/{sourceStats.DistinctNonBlankCount.ToString(CultureInfo.InvariantCulture)}).");
                suggestion.Evidence.Add(
                    targetStats.IsUniqueOverNonBlank
                        ? "Target lookup key values are unique over non-blank values."
                        : "Target lookup key values are not unique over non-blank values.");

                candidates.Add(suggestion);
            }
        }

        return candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Source.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Source.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TargetLookup.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TargetLookup.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Source.EntityName, StringComparer.Ordinal)
            .ThenBy(item => item.Source.PropertyName, StringComparer.Ordinal)
            .ThenBy(item => item.TargetLookup.EntityName, StringComparer.Ordinal)
            .ThenBy(item => item.TargetLookup.PropertyName, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> BuildRelationshipBlockers(
        PropertyProfileStats source,
        PropertyProfileStats target,
        CoverageMetrics coverage,
        bool typeCompatible,
        IReadOnlyDictionary<string, int> targetComparableCounts)
    {
        var blockers = new List<string>();

        if (!typeCompatible)
        {
            blockers.Add("Incompatible scalar type between source and target lookup key.");
        }

        if (targetComparableCounts.Values.Any(count => count > 1))
        {
            blockers.Add("Target lookup key is not unique.");
        }

        if (target.NullCount > 0 || target.BlankStringCount > 0)
        {
            blockers.Add("Target lookup key has null/blank values.");
        }

        if (source.NullCount > 0 || source.BlankStringCount > 0)
        {
            blockers.Add("Source contains null/blank; required relationship cannot be created.");
        }

        if (coverage.UnmatchedDistinctCount > 0)
        {
            blockers.Add("Source values not fully resolvable against target key.");
        }

        return blockers;
    }

    private static void AttachBusinessKeyUsage(
        IReadOnlyList<BusinessKeyCandidate> businessKeys,
        IReadOnlyList<LookupRelationshipSuggestion> eligibleSuggestions)
    {
        var byTarget = eligibleSuggestions
            .GroupBy(item => MakeProfileKey(item.TargetLookup.EntityName, item.TargetLookup.PropertyName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Source.EntityName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Source.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Source.EntityName, StringComparer.Ordinal)
                    .ThenBy(item => item.Source.PropertyName, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var key in businessKeys)
        {
            if (!byTarget.TryGetValue(MakeProfileKey(key.Target.EntityName, key.Target.PropertyName), out var uses))
            {
                continue;
            }

            foreach (var use in uses)
            {
                key.UsedBy.Add(new BusinessKeyUsage
                {
                    SourceEntity = use.Source.EntityName,
                    SourceProperty = use.Source.PropertyName,
                    SourceRowCount = use.Source.RowCount,
                    SourceDistinctNonBlankCount = use.SourceDistinctComparableValueCount,
                    MatchedSourceRowCount = use.MatchedSourceRowCount,
                    SourceComparableRowCount = use.SourceComparableRowCount,
                });
            }

            key.Reasons.Add($"Reused by other entities with same property name ({key.UsedBy.Count.ToString(CultureInfo.InvariantCulture)} occurrences).");
        }
    }

    private static CoverageMetrics BuildCoverageMetrics(PropertyProfile source, PropertyProfile target)
    {
        var matchedSourceRowCount = 0;
        var matchedDistinct = 0;
        var unmatchedDistinctValues = new List<string>();

        foreach (var pair in source.ComparableValueCounts.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (target.ComparableValueCounts.ContainsKey(pair.Key))
            {
                matchedSourceRowCount += pair.Value;
                matchedDistinct++;
            }
            else
            {
                unmatchedDistinctValues.Add(pair.Key);
            }
        }

        var unmatchedSourceRows = source.Stats.NonBlankCount - matchedSourceRowCount;
        return new CoverageMetrics(
            MatchedSourceRowCount: matchedSourceRowCount,
            UnmatchedSourceRowCount: unmatchedSourceRows,
            MatchedDistinctCount: matchedDistinct,
            UnmatchedDistinctCount: unmatchedDistinctValues.Count,
            UnmatchedDistinctSample: unmatchedDistinctValues
                .OrderBy(item => item, StringComparer.Ordinal)
                .Take(5)
                .ToArray());
    }

    private static double ScoreBusinessKey(PropertyProfileStats stats)
    {
        var score = 0.55d;
        if (stats.NullCount == 0)
        {
            score += 0.20d;
        }

        if (stats.BlankStringCount == 0)
        {
            score += 0.15d;
        }

        if (stats.IsRequired)
        {
            score += 0.05d;
        }

        if (stats.RowCount >= 5)
        {
            score += 0.05d;
        }

        return Math.Round(Math.Min(1.0d, score), 3, MidpointRounding.AwayFromZero);
    }

    private static double ScoreRelationshipCandidate(
        PropertyProfileStats source,
        PropertyProfileStats target,
        CoverageMetrics coverage,
        bool typeCompatible,
        int blockerCount)
    {
        var coverageRatio = source.NonBlankCount == 0
            ? 0.0d
            : coverage.MatchedSourceRowCount / (double)source.NonBlankCount;

        var score = 0.20d;
        if (typeCompatible)
        {
            score += 0.20d;
        }

        score += 0.35d * coverageRatio;
        score += target.IsUniqueOverNonBlank ? 0.15d : 0.02d;
        if (source.NonBlankCount > source.DistinctNonBlankCount)
        {
            score += 0.10d;
        }

        score -= blockerCount * 0.12d;
        if (score < 0)
        {
            score = 0;
        }

        return Math.Round(Math.Min(1.0d, score), 3, MidpointRounding.AwayFromZero);
    }

    private static PropertyProfileStats CloneStats(PropertyProfileStats source)
    {
        return new PropertyProfileStats
        {
            EntityName = source.EntityName,
            PropertyName = source.PropertyName,
            DataType = source.DataType,
            IsRequired = source.IsRequired,
            IsStringLike = source.IsStringLike,
            RowCount = source.RowCount,
            NonNullCount = source.NonNullCount,
            NullCount = source.NullCount,
            BlankStringCount = source.BlankStringCount,
            NonBlankCount = source.NonBlankCount,
            DistinctNonNullCount = source.DistinctNonNullCount,
            DistinctNonBlankCount = source.DistinctNonBlankCount,
            IsUniqueOverNonNull = source.IsUniqueOverNonNull,
            IsUniqueOverNonBlank = source.IsUniqueOverNonBlank,
        };
    }

    private static string NormalizeDataType(string? dataType)
    {
        var value = (dataType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "string";
        }

        return value.ToLowerInvariant() switch
        {
            "boolean" => "bool",
            "int32" => "int",
            "int64" => "long",
            _ => value,
        };
    }

    private static bool IsComparableScalar(string dataType)
    {
        return dataType.ToLowerInvariant() is "string" or "bool" or "byte" or "short" or "int" or "long" or
            "decimal" or "double" or "float" or "datetime" or "datetime2" or "date" or "time" or "guid";
    }

    private static bool IsTypeCompatibleStrict(string sourceType, string targetType)
    {
        return string.Equals(NormalizeDataType(sourceType), NormalizeDataType(targetType), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStringLike(string dataType)
    {
        return string.Equals(dataType, "string", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlank(string value)
    {
        return string.IsNullOrEmpty(value);
    }

    private static string MakeProfileKey(string entityName, string propertyName)
    {
        return entityName + "|" + propertyName;
    }

    private sealed record PropertyProfile(
        PropertyProfileStats Stats,
        IReadOnlyDictionary<string, int> ComparableValueCounts);

    private sealed record CoverageMetrics(
        int MatchedSourceRowCount,
        int UnmatchedSourceRowCount,
        int MatchedDistinctCount,
        int UnmatchedDistinctCount,
        IReadOnlyList<string> UnmatchedDistinctSample);
}
