using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Metadata.Framework.Generic
{
    [DataContract]
    public class ImportPlan
    {
        [DataMember(Name = "version")]
        public int Version { get; set; } = 1;

        [DataMember(Name = "resolutions")]
        public List<ImportResolution> Resolutions { get; set; } = new List<ImportResolution>();
    }

    [DataContract]
    public class ImportResolution
    {
        [DataMember(Name = "issueId")]
        public string IssueId { get; set; } = string.Empty;

        [DataMember(Name = "resolution")]
        public string Resolution { get; set; } = string.Empty;

        [DataMember(Name = "targetPath")]
        public string TargetPath { get; set; } = string.Empty;

        [DataMember(Name = "customLogic")]
        public string CustomLogic { get; set; } = string.Empty;

        [DataMember(Name = "notes")]
        public string Notes { get; set; } = string.Empty;
    }

    [DataContract]
    public class ImportIssueReport
    {
        [DataMember(Name = "generatedAtUtc")]
        public string GeneratedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [DataMember(Name = "issues")]
        public List<ImportIssue> Issues { get; set; } = new List<ImportIssue>();

        [DataMember(Name = "warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [DataMember(Name = "summary")]
        public ImportIssueSummary Summary { get; set; } = new ImportIssueSummary();

        public int UnresolvedCount => Issues.Count(i => i.RequiresDecision && string.IsNullOrWhiteSpace(i.Resolution));
    }

    [DataContract]
    public class ImportIssueSummary
    {
        [DataMember(Name = "totalIssues")]
        public int TotalIssues { get; set; }

        [DataMember(Name = "unresolvedIssues")]
        public int UnresolvedIssues { get; set; }

        [DataMember(Name = "dataLossRiskIssues")]
        public int DataLossRiskIssues { get; set; }

        [DataMember(Name = "unresolvedDataLossRiskIssues")]
        public int UnresolvedDataLossRiskIssues { get; set; }

        [DataMember(Name = "byKind")]
        public List<ImportIssueKindSummary> ByKind { get; set; } = new List<ImportIssueKindSummary>();

        [DataMember(Name = "lines")]
        public List<string> Lines { get; set; } = new List<string>();
    }

    [DataContract]
    public class ImportIssueKindSummary
    {
        [DataMember(Name = "kind")]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "count")]
        public int Count { get; set; }

        [DataMember(Name = "unresolvedCount")]
        public int UnresolvedCount { get; set; }

        [DataMember(Name = "dataLossRiskCount")]
        public int DataLossRiskCount { get; set; }
    }

    [DataContract]
    public class ImportIssue
    {
        [DataMember(Name = "id")]
        public string Id { get; set; } = string.Empty;

        [DataMember(Name = "kind")]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "message")]
        public string Message { get; set; } = string.Empty;

        [DataMember(Name = "sourcePath")]
        public string SourcePath { get; set; } = string.Empty;

        [DataMember(Name = "candidateTargets")]
        public List<string> CandidateTargets { get; set; } = new List<string>();

        [DataMember(Name = "requiresDecision")]
        public bool RequiresDecision { get; set; } = true;

        [DataMember(Name = "dataLossRisk")]
        public bool DataLossRisk { get; set; }

        [DataMember(Name = "riskReason")]
        public string RiskReason { get; set; } = string.Empty;

        [DataMember(Name = "availableResolutions")]
        public List<string> AvailableResolutions { get; set; } = new List<string>
        {
            "ignore",
            "map",
            "custom_logic"
        };

        [DataMember(Name = "resolution")]
        public string Resolution { get; set; } = string.Empty;

        [DataMember(Name = "targetPath")]
        public string TargetPath { get; set; } = string.Empty;

        [DataMember(Name = "customLogic")]
        public string CustomLogic { get; set; } = string.Empty;
    }

    public enum ImportResolutionType
    {
        None = 0,
        Ignore = 1,
        Map = 2,
        CustomLogic = 3
    }

    public static class ImportIssueEngine
    {
        private const string EntityMissingInImport = "entity_missing_in_import";
        private const string EntityNewInImport = "entity_new_in_import";
        private const string PropertyMissingInImport = "property_missing_in_import";
        private const string PropertyNewInImport = "property_new_in_import";
        private const string RelationshipMissingInImport = "relationship_missing_in_import";
        private const string RelationshipNewInImport = "relationship_new_in_import";

        public static ImportIssueReport Build(Model baselineModel, Model importedModel)
        {
            if (baselineModel == null)
            {
                throw new ArgumentNullException(nameof(baselineModel));
            }

            if (importedModel == null)
            {
                throw new ArgumentNullException(nameof(importedModel));
            }

            var report = new ImportIssueReport();
            var issues = new List<ImportIssue>();

            var baselineEntities = baselineModel.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            var importedEntities = importedModel.Entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            var missingEntities = baselineEntities.Keys
                .Except(importedEntities.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var newEntities = importedEntities.Keys
                .Except(baselineEntities.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var missing in missingEntities)
            {
                var candidates = SuggestEntityCandidates(missing, newEntities)
                    .Select(EntityPath)
                    .ToList();
                issues.Add(CreateIssue(
                    EntityMissingInImport,
                    EntityPath(missing),
                    candidates,
                    $"Entity '{missing}' exists in baseline but not in import.",
                    true,
                    "Missing baseline entity may drop metadata shape and dependent instance rows."));
            }

            foreach (var added in newEntities)
            {
                issues.Add(CreateIssue(
                    EntityNewInImport,
                    EntityPath(added),
                    new List<string>(),
                    $"Entity '{added}' exists in import but not in baseline.",
                    false,
                    "Additive entity; no direct loss in baseline unless ignored intentionally."));
            }

            var commonEntities = baselineEntities.Keys
                .Intersect(importedEntities.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

            foreach (var entityName in commonEntities)
            {
                var baselineEntity = baselineEntities[entityName];
                var importedEntity = importedEntities[entityName];

                var baselineProperties = baselineEntity.Properties
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                var importedProperties = importedEntity.Properties
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                var missingProperties = baselineProperties.Keys
                    .Except(importedProperties.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var newProperties = importedProperties.Keys
                    .Except(baselineProperties.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var missingProperty in missingProperties)
                {
                    var candidates = SuggestPropertyCandidates(missingProperty, newProperties)
                        .Select(p => PropertyPath(entityName, p))
                        .ToList();
                    issues.Add(CreateIssue(
                        PropertyMissingInImport,
                        PropertyPath(entityName, missingProperty),
                        candidates,
                        $"Property '{entityName}.{missingProperty}' exists in baseline but not in import.",
                        true,
                        "Missing baseline property can drop field values on import/export."));
                }

                foreach (var newProperty in newProperties)
                {
                    issues.Add(CreateIssue(
                        PropertyNewInImport,
                        PropertyPath(entityName, newProperty),
                        new List<string>(),
                        $"Property '{entityName}.{newProperty}' exists in import but not in baseline.",
                        false,
                        "Additive property; no direct loss in baseline unless ignored intentionally."));
                }

                var baselineRelationships = baselineEntity.Relationship
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Entity))
                    .Select(r => r.Entity)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var importedRelationships = importedEntity.Relationship
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Entity))
                    .Select(r => r.Entity)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var missingRelationships = baselineRelationships
                    .Except(importedRelationships, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var newRelationships = importedRelationships
                    .Except(baselineRelationships, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var missingRelationship in missingRelationships)
                {
                    var candidates = SuggestEntityCandidates(missingRelationship, newRelationships)
                        .Select(r => RelationshipPath(entityName, r))
                        .ToList();
                    issues.Add(CreateIssue(
                        RelationshipMissingInImport,
                        RelationshipPath(entityName, missingRelationship),
                        candidates,
                        $"Relationship '{entityName} -> {missingRelationship}' exists in baseline but not in import.",
                        true,
                        "Missing baseline relationship can orphan references and lose linkage semantics."));
                }

                foreach (var newRelationship in newRelationships)
                {
                    issues.Add(CreateIssue(
                        RelationshipNewInImport,
                        RelationshipPath(entityName, newRelationship),
                        new List<string>(),
                        $"Relationship '{entityName} -> {newRelationship}' exists in import but not in baseline.",
                        false,
                        "Additive relationship; no direct loss in baseline unless ignored intentionally."));
                }
            }

            report.Issues = issues
                .OrderBy(i => i.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            UpdateSummary(report);
            return report;
        }

        public static void AttachPlanSelections(ImportIssueReport report, ImportPlan plan, IList<string> warnings = null)
        {
            if (report == null || plan == null)
            {
                return;
            }

            var planByIssueId = plan.Resolutions
                .Where(r => !string.IsNullOrWhiteSpace(r.IssueId))
                .GroupBy(r => r.IssueId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            foreach (var issue in report.Issues)
            {
                if (!planByIssueId.TryGetValue(issue.Id, out var resolution))
                {
                    continue;
                }

                issue.Resolution = NormalizeResolution(resolution.Resolution);
                issue.TargetPath = resolution.TargetPath ?? string.Empty;
                issue.CustomLogic = resolution.CustomLogic ?? string.Empty;
            }

            if (warnings == null)
            {
                UpdateSummary(report);
                return;
            }

            var issueIds = new HashSet<string>(report.Issues.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var resolution in plan.Resolutions)
            {
                if (string.IsNullOrWhiteSpace(resolution.IssueId))
                {
                    continue;
                }

                if (!issueIds.Contains(resolution.IssueId))
                {
                    warnings.Add($"Plan entry '{resolution.IssueId}' did not match any current import issue.");
                }
            }

            UpdateSummary(report);
        }

        public static List<string> ApplyPlan(ImportIssueReport report, ImportPlan plan, Model importedModel)
        {
            var warnings = new List<string>();
            if (report == null || plan == null || importedModel == null)
            {
                return warnings;
            }

            var issuesById = report.Issues
                .ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

            // Apply entity maps first so downstream property/relationship paths can target the renamed entities.
            ApplyMappedResolutionsOfKind(
                ImportResolutionType.Map,
                EntityMissingInImport,
                issuesById,
                plan,
                importedModel,
                warnings);
            ApplyMappedResolutionsOfKind(
                ImportResolutionType.Map,
                PropertyMissingInImport,
                issuesById,
                plan,
                importedModel,
                warnings);
            ApplyMappedResolutionsOfKind(
                ImportResolutionType.Map,
                RelationshipMissingInImport,
                issuesById,
                plan,
                importedModel,
                warnings);

            AttachPlanSelections(report, plan, warnings);
            return warnings;
        }

        private static void ApplyMappedResolutionsOfKind(
            ImportResolutionType type,
            string issueKind,
            IDictionary<string, ImportIssue> issuesById,
            ImportPlan plan,
            Model importedModel,
            List<string> warnings)
        {
            foreach (var resolution in plan.Resolutions)
            {
                if (!issuesById.TryGetValue(resolution.IssueId, out var issue))
                {
                    continue;
                }

                if (!string.Equals(issue.Kind, issueKind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ParseResolutionType(resolution.Resolution) != type)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resolution.TargetPath))
                {
                    warnings.Add($"Map resolution for issue '{issue.Id}' is missing targetPath.");
                    continue;
                }

                if (!TryApplyMap(issue, resolution.TargetPath, importedModel, warnings))
                {
                    warnings.Add($"Failed to apply map resolution for issue '{issue.Id}'.");
                }
            }
        }

        private static bool TryApplyMap(
            ImportIssue issue,
            string targetPath,
            Model importedModel,
            List<string> warnings)
        {
            switch (issue.Kind)
            {
                case EntityMissingInImport:
                    return TryApplyEntityMap(issue.SourcePath, targetPath, importedModel, warnings);
                case PropertyMissingInImport:
                    return TryApplyPropertyMap(issue.SourcePath, targetPath, importedModel, warnings);
                case RelationshipMissingInImport:
                    return TryApplyRelationshipMap(issue.SourcePath, targetPath, importedModel, warnings);
                default:
                    warnings.Add($"Map resolution is not supported for issue kind '{issue.Kind}'.");
                    return false;
            }
        }

        private static bool TryApplyEntityMap(
            string sourcePath,
            string targetPath,
            Model importedModel,
            List<string> warnings)
        {
            var sourceEntity = ParsePath(sourcePath).Item2;
            var targetEntity = ParsePath(targetPath).Item2;
            if (string.IsNullOrWhiteSpace(sourceEntity) || string.IsNullOrWhiteSpace(targetEntity))
            {
                warnings.Add($"Invalid entity map paths source='{sourcePath}' target='{targetPath}'.");
                return false;
            }

            var importedTargetEntity = importedModel.Entities.FirstOrDefault(e =>
                string.Equals(e.Name, targetEntity, StringComparison.OrdinalIgnoreCase));
            if (importedTargetEntity == null)
            {
                warnings.Add($"Map target entity '{targetEntity}' was not found in imported model.");
                return false;
            }

            if (importedModel.Entities.Any(e =>
                !ReferenceEquals(e, importedTargetEntity) &&
                string.Equals(e.Name, sourceEntity, StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add($"Cannot map '{targetEntity}' to '{sourceEntity}' because '{sourceEntity}' already exists.");
                return false;
            }

            importedTargetEntity.Name = sourceEntity;
            foreach (var entity in importedModel.Entities)
            {
                foreach (var relationship in entity.Relationship)
                {
                    if (relationship == null)
                    {
                        continue;
                    }

                    if (string.Equals(relationship.Entity, targetEntity, StringComparison.OrdinalIgnoreCase))
                    {
                        relationship.Entity = sourceEntity;
                    }
                }
            }

            return true;
        }

        private static bool TryApplyPropertyMap(
            string sourcePath,
            string targetPath,
            Model importedModel,
            List<string> warnings)
        {
            var source = ParsePath(sourcePath);
            var target = ParsePath(targetPath);
            if (!string.Equals(source.Item1, "property", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.Item1, "property", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Invalid property map paths source='{sourcePath}' target='{targetPath}'.");
                return false;
            }

            var targetEntity = importedModel.Entities.FirstOrDefault(e =>
                string.Equals(e.Name, target.Item2, StringComparison.OrdinalIgnoreCase));
            if (targetEntity == null)
            {
                warnings.Add($"Map target entity '{target.Item2}' was not found in imported model.");
                return false;
            }

            var targetProperty = targetEntity.Properties.FirstOrDefault(p =>
                string.Equals(p.Name, target.Item3, StringComparison.OrdinalIgnoreCase));
            if (targetProperty == null)
            {
                warnings.Add($"Map target property '{target.Item2}.{target.Item3}' was not found in imported model.");
                return false;
            }

            if (targetEntity.Properties.Any(p =>
                !ReferenceEquals(p, targetProperty) &&
                string.Equals(p.Name, source.Item3, StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add($"Cannot map '{target.Item2}.{target.Item3}' to '{source.Item2}.{source.Item3}' because target name already exists.");
                return false;
            }

            targetProperty.Name = source.Item3;
            return true;
        }

        private static bool TryApplyRelationshipMap(
            string sourcePath,
            string targetPath,
            Model importedModel,
            List<string> warnings)
        {
            var source = ParsePath(sourcePath);
            var target = ParsePath(targetPath);
            if (!string.Equals(source.Item1, "relationship", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.Item1, "relationship", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Invalid relationship map paths source='{sourcePath}' target='{targetPath}'.");
                return false;
            }

            var targetEntity = importedModel.Entities.FirstOrDefault(e =>
                string.Equals(e.Name, target.Item2, StringComparison.OrdinalIgnoreCase));
            if (targetEntity == null)
            {
                warnings.Add($"Map target entity '{target.Item2}' was not found in imported model.");
                return false;
            }

            var targetRelationship = targetEntity.Relationship.FirstOrDefault(r =>
                r != null &&
                string.Equals(r.Entity, target.Item3, StringComparison.OrdinalIgnoreCase));
            if (targetRelationship == null)
            {
                warnings.Add($"Map target relationship '{target.Item2}->{target.Item3}' was not found in imported model.");
                return false;
            }

            if (targetEntity.Relationship.Any(r =>
                !ReferenceEquals(r, targetRelationship) &&
                r != null &&
                string.Equals(r.Entity, source.Item3, StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add($"Cannot map relationship '{target.Item2}->{target.Item3}' to '{source.Item2}->{source.Item3}' because target relationship already exists.");
                return false;
            }

            targetRelationship.Entity = source.Item3;
            return true;
        }

        private static ImportIssue CreateIssue(
            string kind,
            string sourcePath,
            List<string> candidateTargets,
            string message,
            bool dataLossRisk,
            string riskReason)
        {
            var issue = new ImportIssue
            {
                Kind = kind,
                SourcePath = sourcePath,
                CandidateTargets = candidateTargets ?? new List<string>(),
                Message = message,
                RequiresDecision = true,
                DataLossRisk = dataLossRisk,
                RiskReason = riskReason ?? string.Empty
            };
            issue.Id = ComputeIssueId(issue.Kind, issue.SourcePath, issue.CandidateTargets);
            return issue;
        }

        private static void UpdateSummary(ImportIssueReport report)
        {
            if (report == null)
            {
                return;
            }

            var summary = new ImportIssueSummary();
            var issues = report.Issues ?? new List<ImportIssue>();

            summary.TotalIssues = issues.Count;
            summary.UnresolvedIssues = issues.Count(i => i.RequiresDecision && string.IsNullOrWhiteSpace(i.Resolution));
            summary.DataLossRiskIssues = issues.Count(i => i.DataLossRisk);
            summary.UnresolvedDataLossRiskIssues = issues.Count(i =>
                i.DataLossRisk &&
                i.RequiresDecision &&
                string.IsNullOrWhiteSpace(i.Resolution));

            summary.ByKind = issues
                .GroupBy(i => i.Kind ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ImportIssueKindSummary
                {
                    Kind = g.Key,
                    Count = g.Count(),
                    UnresolvedCount = g.Count(i => i.RequiresDecision && string.IsNullOrWhiteSpace(i.Resolution)),
                    DataLossRiskCount = g.Count(i => i.DataLossRisk)
                })
                .ToList();

            summary.Lines = BuildSummaryLines(summary);
            report.Summary = summary;
        }

        private static List<string> BuildSummaryLines(ImportIssueSummary summary)
        {
            var lines = new List<string>();
            lines.Add($"Import issues: total={summary.TotalIssues}, unresolved={summary.UnresolvedIssues}.");
            lines.Add($"Data-loss risk: total={summary.DataLossRiskIssues}, unresolved={summary.UnresolvedDataLossRiskIssues}.");
            foreach (var entry in summary.ByKind)
            {
                lines.Add($"Kind '{entry.Kind}': total={entry.Count}, unresolved={entry.UnresolvedCount}, dataLossRisk={entry.DataLossRiskCount}.");
            }

            return lines;
        }

        private static string ComputeIssueId(string kind, string sourcePath, IEnumerable<string> candidates)
        {
            var input = kind + "|" + sourcePath + "|" + string.Join(",", candidates ?? Array.Empty<string>());
            using (var sha1 = SHA1.Create())
            {
                var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static IEnumerable<string> SuggestEntityCandidates(string sourceName, IEnumerable<string> candidates)
        {
            return candidates
                .OrderBy(c => ComputeEditDistance(sourceName, c))
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Take(5);
        }

        private static IEnumerable<string> SuggestPropertyCandidates(string sourceName, IEnumerable<string> candidates)
        {
            return SuggestEntityCandidates(sourceName, candidates);
        }

        private static int ComputeEditDistance(string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;

            var distances = new int[left.Length + 1, right.Length + 1];
            for (int i = 0; i <= left.Length; i++)
            {
                distances[i, 0] = i;
            }

            for (int j = 0; j <= right.Length; j++)
            {
                distances[0, j] = j;
            }

            for (int i = 1; i <= left.Length; i++)
            {
                for (int j = 1; j <= right.Length; j++)
                {
                    var cost = char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1;
                    distances[i, j] = Math.Min(
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost);
                }
            }

            return distances[left.Length, right.Length];
        }

        private static string EntityPath(string entityName)
        {
            return "entity/" + entityName;
        }

        private static string PropertyPath(string entityName, string propertyName)
        {
            return "entity/" + entityName + "/property/" + propertyName;
        }

        private static string RelationshipPath(string entityName, string relatedEntityName)
        {
            return "entity/" + entityName + "/relationship/" + relatedEntityName;
        }

        private static Tuple<string, string, string> ParsePath(string path)
        {
            // Shapes:
            // entity/{entity}
            // entity/{entity}/property/{property}
            // entity/{entity}/relationship/{relatedEntity}
            var segments = (path ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2 && string.Equals(segments[0], "entity", StringComparison.OrdinalIgnoreCase))
            {
                return Tuple.Create("entity", segments[1], string.Empty);
            }

            if (segments.Length == 4 &&
                string.Equals(segments[0], "entity", StringComparison.OrdinalIgnoreCase))
            {
                return Tuple.Create(segments[2], segments[1], segments[3]);
            }

            return Tuple.Create(string.Empty, string.Empty, string.Empty);
        }

        private static ImportResolutionType ParseResolutionType(string resolution)
        {
            var normalized = NormalizeResolution(resolution);
            if (string.Equals(normalized, "ignore", StringComparison.OrdinalIgnoreCase))
            {
                return ImportResolutionType.Ignore;
            }

            if (string.Equals(normalized, "map", StringComparison.OrdinalIgnoreCase))
            {
                return ImportResolutionType.Map;
            }

            if (string.Equals(normalized, "custom_logic", StringComparison.OrdinalIgnoreCase))
            {
                return ImportResolutionType.CustomLogic;
            }

            return ImportResolutionType.None;
        }

        private static string NormalizeResolution(string resolution)
        {
            return (resolution ?? string.Empty).Trim().ToLowerInvariant();
        }
    }

    public static class ImportPlanStore
    {
        public static ImportPlan Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ImportPlan();
            }

            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(ImportPlan));
                var plan = serializer.ReadObject(stream) as ImportPlan;
                return plan ?? new ImportPlan();
            }
        }

        public static void Save(string path, ImportPlan plan)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(ImportPlan));
                serializer.WriteObject(stream, plan ?? new ImportPlan());
            }
        }
    }

    public static class ImportIssueReportStore
    {
        public static void Save(string path, ImportIssueReport report)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(ImportIssueReport));
                serializer.WriteObject(stream, report ?? new ImportIssueReport());
            }
        }
    }
}
