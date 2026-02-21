
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Metadata.Framework.Generic;

namespace Metadata.Framework.Transformations
{
    public class ModelToCSharpConverter
    {
        private sealed class EntityShape
        {
            public string Name = string.Empty;
            public string ListName = string.Empty;
            public List<Property> ScalarProperties = new List<Property>();
            public List<RelationshipDefinition> Relationships = new List<RelationshipDefinition>();
            public string CanonicalNameProperty = string.Empty;
        }

        public string Generate(Model model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var modelName = string.IsNullOrWhiteSpace(model.Name) ? "GeneratedModelRoot" : model.Name;
            var loaderName = modelName + "Model";
            var dataName = modelName + "Data";
            var entities = BuildEntityShapes(model);

            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Data.Common;");
            builder.AppendLine("using System.IO;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using Metadata.Framework.Generic;");
            builder.AppendLine();
            builder.AppendLine("namespace GeneratedModel");
            builder.AppendLine("{");

            AppendDataClass(builder, dataName, entities);
            AppendRootSingletonClass(builder, modelName, loaderName, dataName, entities);
            AppendLoaderClass(builder, modelName, loaderName, dataName, entities);

            foreach (var entity in entities)
            {
                AppendRowClass(builder, entity);
            }

            foreach (var entity in entities)
            {
                AppendListClass(builder, entity);
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static List<EntityShape> BuildEntityShapes(Model model)
        {
            var entities = new List<EntityShape>();
            foreach (var entity in model.Entities)
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                var relationshipNames = entity.Relationship
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Entity))
                    .Select(item => item.Entity)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var scalarProperties = entity.Properties
                    .Where(property =>
                    {
                        if (property == null || string.IsNullOrWhiteSpace(property.Name))
                        {
                            return false;
                        }

                        if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        if (property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                        {
                            var candidateRelationship = property.Name.Substring(0, property.Name.Length - 2);
                            if (relationshipNames.Contains(candidateRelationship))
                            {
                                return false;
                            }
                        }

                        return true;
                    })
                    .ToList();

                var relationships = entity.Relationship
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Entity))
                    .ToList();

                var canonicalNameProperty = string.Empty;
                if (!entity.Properties.Any(p => string.Equals(p.Name, "Name", StringComparison.OrdinalIgnoreCase)))
                {
                    var candidate = entity.Name + "Name";
                    if (entity.Properties.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                    {
                        canonicalNameProperty = candidate;
                    }
                }

                entities.Add(new EntityShape
                {
                    Name = entity.Name,
                    ListName = CSharpGenerationUtilities.ToPluralName(entity),
                    ScalarProperties = scalarProperties,
                    Relationships = relationships,
                    CanonicalNameProperty = canonicalNameProperty,
                });
            }

            var collectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in entities)
            {
                if (!collectionNames.Add(entity.ListName))
                {
                    throw new InvalidOperationException(
                        $"Duplicate collection name '{entity.ListName}' generated from entity plural names.");
                }
            }

            return entities;
        }

        private static void AppendDataClass(StringBuilder builder, string dataName, IList<EntityShape> entities)
        {
            builder.AppendLine($"    internal sealed class {dataName}");
            builder.AppendLine("    {");

            var constructorArguments = string.Join(",\n            ", entities.Select(entity => $"{entity.ListName} {ToCamel(entity.ListName)}"));
            builder.AppendLine($"        internal {dataName}(");
            builder.AppendLine("            " + constructorArguments + ")");
            builder.AppendLine("        {");
            foreach (var entity in entities)
            {
                var parameterName = ToCamel(entity.ListName);
                builder.AppendLine($"            {entity.ListName} = {parameterName} ?? throw new ArgumentNullException(nameof({parameterName}));");
            }
            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var entity in entities)
            {
                builder.AppendLine($"        internal {entity.ListName} {entity.ListName} {{ get; }}");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendRootSingletonClass(
            StringBuilder builder,
            string modelName,
            string loaderName,
            string dataName,
            IList<EntityShape> entities)
        {
            builder.AppendLine($"    public static class {modelName}");
            builder.AppendLine("    {");
            builder.AppendLine($"        private static {dataName} _data;");
            builder.AppendLine("        private static readonly object SyncRoot = new object();");
            builder.AppendLine();

            foreach (var entity in entities)
            {
                builder.AppendLine($"        public static {entity.ListName} {entity.ListName}");
                builder.AppendLine("        {");
                builder.AppendLine($"            get {{ return EnsureLoaded().{entity.ListName}; }}");
                builder.AppendLine("        }");
            }

            builder.AppendLine();
            builder.AppendLine($"        internal static void Install({dataName} data)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (data == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(data));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            lock (SyncRoot)");
            builder.AppendLine("            {");
            builder.AppendLine("                if (_data != null)");
            builder.AppendLine("                {");
            builder.AppendLine($"                    throw new InvalidOperationException(\"{modelName} is already loaded. Double install is not allowed.\");");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                _data = data;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        private static {dataName} EnsureLoaded()");
            builder.AppendLine("        {");
            builder.AppendLine("            var data = _data;");
            builder.AppendLine("            if (data == null)");
            builder.AppendLine("            {");
            builder.AppendLine($"                throw new InvalidOperationException(\"{modelName} is not loaded. Call {loaderName}.LoadFromXml/LoadFromSql first.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return data;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendLoaderClass(
            StringBuilder builder,
            string modelName,
            string loaderName,
            string dataName,
            IList<EntityShape> entities)
        {
            builder.AppendLine($"    public static class {loaderName}");
            builder.AppendLine("    {");
            builder.AppendLine("        public static void LoadFromXml(string workspacePath)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(workspacePath))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new ArgumentException(\"Workspace path is required.\", nameof(workspacePath));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var fullWorkspacePath = Path.GetFullPath(workspacePath);");
            builder.AppendLine("            if (!Directory.Exists(fullWorkspacePath))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new DirectoryNotFoundException($\"Workspace path was not found: {fullWorkspacePath}\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var modelPath = ResolveModelPath(fullWorkspacePath);");
            builder.AppendLine("            var modelResult = new Reader().Read(modelPath);");
            builder.AppendLine("            EnsureNoErrors(modelResult.Errors, \"model XML load\");");
            builder.AppendLine();
            builder.AppendLine("            var instanceResult = new InstanceReader().ReadWorkspace(fullWorkspacePath, modelResult.Model);");
            builder.AppendLine("            EnsureNoErrors(instanceResult.Errors, \"instance XML load\");");
            builder.AppendLine();
            builder.AppendLine("            var data = BuildData(instanceResult.ModelInstance);");
            builder.AppendLine($"            {modelName}.Install(data);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public static void LoadFromSql(DbConnection connection, string schemaName = \"dbo\")");
            builder.AppendLine("        {");
            builder.AppendLine("            if (connection == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(connection));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(connection.ConnectionString))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new InvalidOperationException(\"Connection string must not be empty.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(schemaName))");
            builder.AppendLine("            {");
            builder.AppendLine("                schemaName = \"dbo\";");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var modelResult = new Reader().ReadFromDatabase(connection.ConnectionString, schemaName);");
            builder.AppendLine("            EnsureNoErrors(modelResult.Errors, \"SQL model load\");");
            builder.AppendLine();
            builder.AppendLine("            var instance = new DatabaseInstanceReader().Read(connection.ConnectionString, modelResult.Model, schemaName);");
            builder.AppendLine("            var data = BuildData(instance);");
            builder.AppendLine($"            {modelName}.Install(data);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        private static {dataName} BuildData(ModelInstance instance)");
            builder.AppendLine("        {");
            foreach (var entity in entities)
            {
                var rowsVariable = ToCamel(entity.Name) + "Rows";
                var listVariable = ToCamel(entity.ListName);
                builder.AppendLine($"            var {rowsVariable} = Build{entity.Name}Rows(instance);");
                builder.AppendLine($"            var {listVariable} = new {entity.ListName}({rowsVariable});");
            }

            builder.AppendLine();
            builder.AppendLine($"            var data = new {dataName}(");
            builder.AppendLine("                " + string.Join(",\n                ", entities.Select(entity => ToCamel(entity.ListName))) + ");");
            builder.AppendLine();
            builder.AppendLine("            ResolveNavigations(data);");
            builder.AppendLine("            return data;");
            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var entity in entities)
            {
                AppendBuildRowsMethod(builder, entity);
            }

            AppendResolveNavigationsMethod(builder, dataName, entities);
            AppendLoaderHelpers(builder);
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendBuildRowsMethod(StringBuilder builder, EntityShape entity)
        {
            builder.AppendLine($"        private static List<{entity.Name}> Build{entity.Name}Rows(ModelInstance instance)");
            builder.AppendLine("        {");
            builder.AppendLine($"            var rows = new List<{entity.Name}>();");
            builder.AppendLine($"            foreach (var record in GetEntityRecords(instance, \"{entity.Name}\"))");
            builder.AppendLine("            {");
            builder.AppendLine($"                var id = ParseRequiredId(\"{entity.Name}\", record.Id);");

            var constructorArguments = new List<string> { "id" };
            foreach (var property in entity.ScalarProperties)
            {
                var variableName = ToCamel(property.Name);
                if (property.IsNullable)
                {
                    builder.AppendLine($"                var {variableName} = GetOptionalPropertyValue(record, \"{property.Name}\");");
                }
                else
                {
                    builder.AppendLine($"                var {variableName} = GetRequiredPropertyValue(record, \"{entity.Name}\", \"{property.Name}\", id);");
                }

                constructorArguments.Add(variableName);
            }

            foreach (var relationship in entity.Relationships)
            {
                var relationshipVariable = ToCamel(relationship.Entity) + "Id";
                builder.AppendLine($"                var {relationshipVariable} = GetOptionalRelationshipId(record, \"{entity.Name}\", \"{relationship.Entity}\", id);");
                constructorArguments.Add(relationshipVariable);
            }

            builder.AppendLine($"                rows.Add(new {entity.Name}({string.Join(", ", constructorArguments)}));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return rows;");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        private static void AppendResolveNavigationsMethod(StringBuilder builder, string dataName, IEnumerable<EntityShape> entities)
        {
            var entityByName = entities.ToDictionary(entity => entity.Name, StringComparer.OrdinalIgnoreCase);
            builder.AppendLine($"        private static void ResolveNavigations({dataName} data)");
            builder.AppendLine("        {");
            foreach (var entity in entities)
            {
                if (entity.Relationships.Count == 0)
                {
                    continue;
                }

                builder.AppendLine($"            foreach (var row in data.{entity.ListName})");
                builder.AppendLine("            {");
                foreach (var relationship in entity.Relationships)
                {
                    var targetVar = ToCamel(relationship.Entity);
                    EntityShape targetEntity;
                    if (!entityByName.TryGetValue(relationship.Entity, out targetEntity))
                    {
                        throw new InvalidOperationException(
                            $"Entity '{entity.Name}' references unknown relationship target '{relationship.Entity}'.");
                    }
                    builder.AppendLine($"                if (row.{relationship.Entity}Id.HasValue)");
                    builder.AppendLine("                {");
                    builder.AppendLine($"                    {relationship.Entity} {targetVar};");
                    builder.AppendLine($"                    if (!data.{targetEntity.ListName}.TryGetId(row.{relationship.Entity}Id.Value, out {targetVar}))");
                    builder.AppendLine("                    {");
                    builder.AppendLine($"                        throw new InvalidOperationException($\"Relationship '{entity.Name}->{relationship.Entity}' on row '{{row.Id}}' references missing target '{{row.{relationship.Entity}Id.Value}}'.\");");
                    builder.AppendLine("                    }");
                    builder.AppendLine();
                    builder.AppendLine($"                    row.{relationship.Entity} = {targetVar};");
                    builder.AppendLine("                }");
                }

                builder.AppendLine("            }");
            }

            builder.AppendLine("        }");
            builder.AppendLine();
        }

        private static void AppendLoaderHelpers(StringBuilder builder)
        {
            builder.AppendLine("        private static IEnumerable<RecordInstance> GetEntityRecords(ModelInstance instance, string entityName)");
            builder.AppendLine("        {");
            builder.AppendLine("            var entityInstance = instance.Entities.FirstOrDefault(item =>");
            builder.AppendLine("                item != null && item.Entity != null && string.Equals(item.Entity.Name, entityName, StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("            if (entityInstance == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                return Enumerable.Empty<RecordInstance>();");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return entityInstance.Records");
            builder.AppendLine("                .Where(item => item != null)");
            builder.AppendLine("                .OrderBy(item => ParseRequiredId(entityName, item.Id))");
            builder.AppendLine("                .ToArray();");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine("        private static int ParseRequiredId(string entityName, string idText)");
            builder.AppendLine("        {");
            builder.AppendLine("            int id;");
            builder.AppendLine("            if (!int.TryParse(idText, out id) || id <= 0)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new InvalidOperationException($\"Entity '{entityName}' contains invalid Id '{idText}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return id;");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine("        private static string GetRequiredPropertyValue(RecordInstance record, string entityName, string propertyName, int rowId)");
            builder.AppendLine("        {");
            builder.AppendLine("            var property = record.Properties.FirstOrDefault(item =>");
            builder.AppendLine("                item != null && item.Property != null && string.Equals(item.Property.Name, propertyName, StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("            if (property == null || string.IsNullOrWhiteSpace(property.Value))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new InvalidOperationException($\"Entity '{entityName}' row '{rowId}' is missing required property '{propertyName}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return property.Value;");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine("        private static string GetOptionalPropertyValue(RecordInstance record, string propertyName)");
            builder.AppendLine("        {");
            builder.AppendLine("            var property = record.Properties.FirstOrDefault(item =>");
            builder.AppendLine("                item != null && item.Property != null && string.Equals(item.Property.Name, propertyName, StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("            return property != null ? property.Value : null;");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine("        private static int? GetOptionalRelationshipId(RecordInstance record, string sourceEntity, string targetEntity, int rowId)");
            builder.AppendLine("        {");
            builder.AppendLine("            var matches = record.Relationships");
            builder.AppendLine("                .Where(item => item != null && item.Entity != null && string.Equals(item.Entity.Name, targetEntity, StringComparison.OrdinalIgnoreCase))");
            builder.AppendLine("                .ToArray();");
            builder.AppendLine("            if (matches.Length == 0)");
            builder.AppendLine("            {");
            builder.AppendLine("                return null;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            if (matches.Length > 1)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new InvalidOperationException($\"Entity '{sourceEntity}' row '{rowId}' has multiple '{targetEntity}' relationships.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var rawValue = matches[0].Value;");
            builder.AppendLine("            int relationshipId;");
            builder.AppendLine("            if (!int.TryParse(rawValue, out relationshipId) || relationshipId <= 0)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new InvalidOperationException($\"Entity '{sourceEntity}' row '{rowId}' has invalid relationship id '{rawValue}' for '{targetEntity}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return relationshipId;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static void EnsureNoErrors(IList<string> errors, string context)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (errors == null || errors.Count == 0)");
            builder.AppendLine("            {");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var message = string.Join(Environment.NewLine, errors.Select(error => \" - \" + error));");
            builder.AppendLine("            throw new InvalidOperationException($\"{context} failed:{Environment.NewLine}{message}\");");
            builder.AppendLine("        }");
            builder.AppendLine();

            builder.AppendLine("        private static string ResolveModelPath(string workspacePath)");
            builder.AppendLine("        {");
            builder.AppendLine("            var candidates = new[]");
            builder.AppendLine("            {");
            builder.AppendLine("                Path.Combine(workspacePath, \"metadata\", \"model.xml\"),");
            builder.AppendLine("                Path.Combine(workspacePath, \"SampleModel.xml\"),");
            builder.AppendLine("                Path.Combine(workspacePath, \"model.xml\"),");
            builder.AppendLine("            };");
            builder.AppendLine();
            builder.AppendLine("            var match = candidates.FirstOrDefault(File.Exists);");
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(match))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new FileNotFoundException($\"Could not find model XML under workspace '{workspacePath}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return Path.GetFullPath(match);");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        private static void AppendRowClass(StringBuilder builder, EntityShape entity)
        {
            builder.AppendLine($"    public sealed class {entity.Name}");
            builder.AppendLine("    {");

            var constructorParameters = new List<string> { "int id" };
            constructorParameters.AddRange(entity.ScalarProperties.Select(property => $"string {ToCamel(property.Name)}"));
            constructorParameters.AddRange(entity.Relationships.Select(relationship => $"int? {ToCamel(relationship.Entity)}Id"));

            builder.AppendLine($"        internal {entity.Name}({string.Join(", ", constructorParameters)})");
            builder.AppendLine("        {");
            builder.AppendLine("            Id = id;");
            foreach (var property in entity.ScalarProperties)
            {
                builder.AppendLine($"            {property.Name} = {ToCamel(property.Name)};");
            }

            foreach (var relationship in entity.Relationships)
            {
                builder.AppendLine($"            {relationship.Entity}Id = {ToCamel(relationship.Entity)}Id;");
            }

            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public int Id { get; }");
            foreach (var property in entity.ScalarProperties)
            {
                builder.AppendLine($"        public string {property.Name} {{ get; }}");
            }

            if (!string.IsNullOrWhiteSpace(entity.CanonicalNameProperty))
            {
                builder.AppendLine($"        public string Name {{ get {{ return {entity.CanonicalNameProperty}; }} }}");
            }

            foreach (var relationship in entity.Relationships)
            {
                builder.AppendLine($"        public int? {relationship.Entity}Id {{ get; }}");
                builder.AppendLine($"        public {relationship.Entity} {relationship.Entity} {{ get; internal set; }}");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendListClass(StringBuilder builder, EntityShape entity)
        {
            builder.AppendLine($"    public sealed class {entity.ListName} : IEnumerable<{entity.Name}>");
            builder.AppendLine("    {");
            builder.AppendLine($"        private readonly List<{entity.Name}> _rows;");
            builder.AppendLine($"        private readonly Dictionary<int, {entity.Name}> _rowsById;");
            builder.AppendLine();
            builder.AppendLine($"        internal {entity.ListName}(IEnumerable<{entity.Name}> rows)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (rows == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new ArgumentNullException(nameof(rows));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            _rows = rows.OrderBy(item => item.Id).ToList();");
            builder.AppendLine($"            _rowsById = new Dictionary<int, {entity.Name}>();");
            builder.AppendLine("            foreach (var row in _rows)");
            builder.AppendLine("            {");
            builder.AppendLine("                if (_rowsById.ContainsKey(row.Id))");
            builder.AppendLine("                {");
            builder.AppendLine($"                    throw new InvalidOperationException($\"Duplicate Id '{{row.Id}}' in {entity.Name} list.\");");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                _rowsById[row.Id] = row;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public {entity.Name} GetId(int id)");
            builder.AppendLine("        {");
            builder.AppendLine($"            {entity.Name} row;");
            builder.AppendLine("            if (!_rowsById.TryGetValue(id, out row))");
            builder.AppendLine("            {");
            builder.AppendLine($"                throw new KeyNotFoundException($\"{entity.Name} id '{{id}}' was not found.\");");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return row;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public bool TryGetId(int id, out {entity.Name} row)");
            builder.AppendLine("        {");
            builder.AppendLine("            return _rowsById.TryGetValue(id, out row);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public IEnumerator<{entity.Name}> GetEnumerator()");
            builder.AppendLine("        {");
            builder.AppendLine("            return _rows.GetEnumerator();");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        IEnumerator IEnumerable.GetEnumerator()");
            builder.AppendLine("        {");
            builder.AppendLine("            return GetEnumerator();");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static string ToCamel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "value";
            }

            if (value.Length == 1)
            {
                return value.ToLowerInvariant();
            }

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
