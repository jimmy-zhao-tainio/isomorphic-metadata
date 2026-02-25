using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace GeneratedModel
{
    internal sealed class EnterpriseBIPlatformData
    {
        internal EnterpriseBIPlatformData(
            Cubes cubes,
            Dimensions dimensions,
            Facts facts,
            Measures measures,
            Systems systems,
            SystemCubes systemCubes,
            SystemDimensions systemDimensions,
            SystemFacts systemFacts,
            SystemTypes systemTypes)
        {
            Cubes = cubes ?? throw new ArgumentNullException(nameof(cubes));
            Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
            Facts = facts ?? throw new ArgumentNullException(nameof(facts));
            Measures = measures ?? throw new ArgumentNullException(nameof(measures));
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));
            SystemCubes = systemCubes ?? throw new ArgumentNullException(nameof(systemCubes));
            SystemDimensions = systemDimensions ?? throw new ArgumentNullException(nameof(systemDimensions));
            SystemFacts = systemFacts ?? throw new ArgumentNullException(nameof(systemFacts));
            SystemTypes = systemTypes ?? throw new ArgumentNullException(nameof(systemTypes));
        }

        internal Cubes Cubes { get; }
        internal Dimensions Dimensions { get; }
        internal Facts Facts { get; }
        internal Measures Measures { get; }
        internal Systems Systems { get; }
        internal SystemCubes SystemCubes { get; }
        internal SystemDimensions SystemDimensions { get; }
        internal SystemFacts SystemFacts { get; }
        internal SystemTypes SystemTypes { get; }
    }

    public static class EnterpriseBIPlatform
    {
        private static readonly EnterpriseBIPlatformInstance _builtIn = EnterpriseBIPlatformInstanceFactory.CreateBuiltIn();

        public static Cubes Cubes => _builtIn.Cubes;
        public static Dimensions Dimensions => _builtIn.Dimensions;
        public static Facts Facts => _builtIn.Facts;
        public static Measures Measures => _builtIn.Measures;
        public static Systems Systems => _builtIn.Systems;
        public static SystemCubes SystemCubes => _builtIn.SystemCubes;
        public static SystemDimensions SystemDimensions => _builtIn.SystemDimensions;
        public static SystemFacts SystemFacts => _builtIn.SystemFacts;
        public static SystemTypes SystemTypes => _builtIn.SystemTypes;
        public static EnterpriseBIPlatformInstance BuiltIn => _builtIn;
    }

    public sealed class EnterpriseBIPlatformInstance
    {
        private readonly EnterpriseBIPlatformData _data;

        internal EnterpriseBIPlatformInstance(EnterpriseBIPlatformData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public Cubes Cubes => _data.Cubes;
        public Dimensions Dimensions => _data.Dimensions;
        public Facts Facts => _data.Facts;
        public Measures Measures => _data.Measures;
        public Systems Systems => _data.Systems;
        public SystemCubes SystemCubes => _data.SystemCubes;
        public SystemDimensions SystemDimensions => _data.SystemDimensions;
        public SystemFacts SystemFacts => _data.SystemFacts;
        public SystemTypes SystemTypes => _data.SystemTypes;
    }

    internal static class EnterpriseBIPlatformInstanceFactory
    {
        internal static EnterpriseBIPlatformInstance CreateBuiltIn()
        {
            return EnterpriseBIPlatformModel.CreateBuiltIn();
        }
    }

    public static class EnterpriseBIPlatformModel
    {
        public static EnterpriseBIPlatformInstance LoadFromXmlWorkspace(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentException("Workspace path is required.", nameof(workspacePath));
            }

            var fullWorkspacePath = Path.GetFullPath(workspacePath);
            if (!Directory.Exists(fullWorkspacePath))
            {
                throw new DirectoryNotFoundException($"Workspace path was not found: {fullWorkspacePath}");
            }

            var instance = ReadXmlWorkspaceInstance(fullWorkspacePath);
            var data = BuildData(instance);
            return new EnterpriseBIPlatformInstance(data);
        }

        public static EnterpriseBIPlatformInstance LoadFromSql(DbConnection connection, string schemaName = "dbo")
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                throw new InvalidOperationException("Connection string must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(schemaName))
            {
                schemaName = "dbo";
            }

            var instance = ReadSqlInstance(connection, schemaName);
            var data = BuildData(instance);
            return new EnterpriseBIPlatformInstance(data);
        }

        public static void SaveToXmlWorkspace(EnterpriseBIPlatformInstance model, string workspacePath)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentException("Workspace path is required.", nameof(workspacePath));
            }

            var fullWorkspacePath = Path.GetFullPath(workspacePath);
            var instanceDirectory = Path.Combine(fullWorkspacePath, "metadata", "instance");
            Directory.CreateDirectory(instanceDirectory);

            foreach (var staleFile in Directory.GetFiles(instanceDirectory, "*.xml", SearchOption.TopDirectoryOnly))
            {
                File.Delete(staleFile);
            }

            SaveShard(instanceDirectory, "Cube", "Cubes", model.Cubes.Select(row =>
            {
                EnsureRequiredProperty("Cube", row.Id, "CubeName", row.CubeName);
                return new XElement(
                    "Cube",
                    new XAttribute("Id", row.Id),
                    ToPropertyElement("CubeName", row.CubeName),
                    ToPropertyElement("Purpose", row.Purpose),
                    ToPropertyElement("RefreshMode", row.RefreshMode));
            }));

            SaveShard(instanceDirectory, "Dimension", "Dimensions", model.Dimensions.Select(row =>
            {
                EnsureRequiredProperty("Dimension", row.Id, "DimensionName", row.DimensionName);
                EnsureRequiredProperty("Dimension", row.Id, "IsConformed", row.IsConformed);
                return new XElement(
                    "Dimension",
                    new XAttribute("Id", row.Id),
                    ToPropertyElement("DimensionName", row.DimensionName),
                    ToPropertyElement("IsConformed", row.IsConformed),
                    ToPropertyElement("HierarchyCount", row.HierarchyCount));
            }));

            SaveShard(instanceDirectory, "Fact", "Facts", model.Facts.Select(row =>
            {
                EnsureRequiredProperty("Fact", row.Id, "FactName", row.FactName);
                return new XElement(
                    "Fact",
                    new XAttribute("Id", row.Id),
                    ToPropertyElement("FactName", row.FactName),
                    ToPropertyElement("Grain", row.Grain),
                    ToPropertyElement("MeasureCount", row.MeasureCount),
                    ToPropertyElement("BusinessArea", row.BusinessArea));
            }));

            SaveShard(instanceDirectory, "Measure", "Measures", model.Measures.Select(row =>
            {
                EnsureRequiredProperty("Measure", row.Id, "MeasureName", row.MeasureName);
                EnsureRequiredRelationshipId("Measure", row.Id, "CubeId", row.CubeId);
                return new XElement(
                    "Measure",
                    new XAttribute("Id", row.Id),
                    new XAttribute("CubeId", row.CubeId),
                    ToPropertyElement("MeasureName", row.MeasureName),
                    ToPropertyElement("MDX", row.MDX));
            }));

            SaveShard(instanceDirectory, "System", "Systems", model.Systems.Select(row =>
            {
                EnsureRequiredProperty("System", row.Id, "SystemName", row.SystemName);
                EnsureRequiredRelationshipId("System", row.Id, "SystemTypeId", row.SystemTypeId);
                return new XElement(
                    "System",
                    new XAttribute("Id", row.Id),
                    new XAttribute("SystemTypeId", row.SystemTypeId),
                    ToPropertyElement("SystemName", row.SystemName),
                    ToPropertyElement("Version", row.Version),
                    ToPropertyElement("DeploymentDate", row.DeploymentDate));
            }));

            SaveShard(instanceDirectory, "SystemCube", "SystemCubes", model.SystemCubes.Select(row =>
            {
                EnsureRequiredRelationshipId("SystemCube", row.Id, "CubeId", row.CubeId);
                EnsureRequiredRelationshipId("SystemCube", row.Id, "SystemId", row.SystemId);
                return new XElement(
                    "SystemCube",
                    new XAttribute("Id", row.Id),
                    new XAttribute("CubeId", row.CubeId),
                    new XAttribute("SystemId", row.SystemId),
                    ToPropertyElement("ProcessingMode", row.ProcessingMode));
            }));

            SaveShard(instanceDirectory, "SystemDimension", "SystemDimensions", model.SystemDimensions.Select(row =>
            {
                EnsureRequiredRelationshipId("SystemDimension", row.Id, "DimensionId", row.DimensionId);
                EnsureRequiredRelationshipId("SystemDimension", row.Id, "SystemId", row.SystemId);
                return new XElement(
                    "SystemDimension",
                    new XAttribute("Id", row.Id),
                    new XAttribute("DimensionId", row.DimensionId),
                    new XAttribute("SystemId", row.SystemId),
                    ToPropertyElement("ConformanceLevel", row.ConformanceLevel));
            }));

            SaveShard(instanceDirectory, "SystemFact", "SystemFacts", model.SystemFacts.Select(row =>
            {
                EnsureRequiredRelationshipId("SystemFact", row.Id, "FactId", row.FactId);
                EnsureRequiredRelationshipId("SystemFact", row.Id, "SystemId", row.SystemId);
                return new XElement(
                    "SystemFact",
                    new XAttribute("Id", row.Id),
                    new XAttribute("FactId", row.FactId),
                    new XAttribute("SystemId", row.SystemId),
                    ToPropertyElement("LoadPattern", row.LoadPattern));
            }));

            SaveShard(instanceDirectory, "SystemType", "SystemTypes", model.SystemTypes.Select(row =>
            {
                EnsureRequiredProperty("SystemType", row.Id, "TypeName", row.TypeName);
                return new XElement(
                    "SystemType",
                    new XAttribute("Id", row.Id),
                    ToPropertyElement("TypeName", row.TypeName),
                    ToPropertyElement("Description", row.Description));
            }));
        }

        internal static EnterpriseBIPlatformInstance CreateBuiltIn()
        {
            return new EnterpriseBIPlatformInstance(CreateBuiltInData());
        }

        private static void SaveShard(string instanceDirectory, string entityName, string pluralName, IEnumerable<XElement> rows)
        {
            var root = new XElement("EnterpriseBIPlatform");
            var container = new XElement(pluralName);
            foreach (var row in rows.OrderBy(item => ParseRequiredId(entityName, item.Attribute("Id")?.Value)))
            {
                container.Add(row);
            }

            root.Add(container);
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root);
            WriteXmlDocument(Path.Combine(instanceDirectory, entityName + ".xml"), document);
        }

        private static XElement ToPropertyElement(string name, string value)
        {
            return value == null ? null : new XElement(name, value);
        }

        private static void EnsureRequiredProperty(string entityName, int id, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Entity '{entityName}' row '{id}' is missing required property '{propertyName}'.");
            }
        }

        private static void EnsureRequiredRelationshipId(string entityName, int id, string relationshipName, int relationshipId)
        {
            if (relationshipId <= 0)
            {
                throw new InvalidOperationException($"Entity '{entityName}' row '{id}' is missing required relationship '{relationshipName}'.");
            }
        }

        private static void WriteXmlDocument(string path, XDocument document)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new global::System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = XmlWriter.Create(stream, settings))
            {
                document.Save(writer);
            }
        }

        private static EnterpriseBIPlatformData CreateBuiltInData()
        {
            var cubes = new Cubes(new[]
            {
                new Cube(1, "Sales Performance", "Monthly revenue and margin tracking.", "Scheduled"),
                new Cube(2, "Finance Overview", "Quarterly financial statements.", "Manual"),
            });
            var dimensions = new Dimensions(new[]
            {
                new Dimension(1, "Customer", "True", "2"),
                new Dimension(2, "Product", "False", "1"),
            });
            var facts = new Facts(new[]
            {
                new Fact(1, "SalesFact", "Order Line", "12", string.Empty),
            });
            var measures = new Measures(new[]
            {
                new Measure(1, "number_of_things", "count", 1),
            });
            var systems = new Systems(new[]
            {
                new System(1, "Enterprise Analytics Platform", "2.1", "2024-11-15", 1),
                new System(2, "Analytics Sandbox", "1.4", "2023-06-01", 2),
            });
            var systemCubes = new SystemCubes(new[]
            {
                new SystemCube(1, "InMemory", 1, 1),
                new SystemCube(2, "DirectQuery", 2, 2),
            });
            var systemDimensions = new SystemDimensions(new[]
            {
                new SystemDimension(1, "Enterprise", 1, 1),
                new SystemDimension(2, "Sandbox", 2, 2),
            });
            var systemFacts = new SystemFacts(new[]
            {
                new SystemFact(1, "Incremental", 1, 1),
            });
            var systemTypes = new SystemTypes(new[]
            {
                new SystemType(1, "Internal", "Managed within the corporate data center."),
                new SystemType(2, "External", "Hosted by a third-party vendor."),
            });

            var data = new EnterpriseBIPlatformData(
                cubes,
                dimensions,
                facts,
                measures,
                systems,
                systemCubes,
                systemDimensions,
                systemFacts,
                systemTypes);
            ResolveNavigations(data);
            return data;
        }

        private static readonly string[] EntityNames =
        {
            "Cube",
            "Dimension",
            "Fact",
            "Measure",
            "System",
            "SystemCube",
            "SystemDimension",
            "SystemFact",
            "SystemType",
        };

        private static readonly Dictionary<string, string[]> PropertyNamesByEntity =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Cube"] = new[] { "CubeName", "Purpose", "RefreshMode" },
                ["Dimension"] = new[] { "DimensionName", "IsConformed", "HierarchyCount" },
                ["Fact"] = new[] { "FactName", "Grain", "MeasureCount", "BusinessArea" },
                ["Measure"] = new[] { "MeasureName", "MDX" },
                ["System"] = new[] { "SystemName", "Version", "DeploymentDate" },
                ["SystemCube"] = new[] { "ProcessingMode" },
                ["SystemDimension"] = new[] { "ConformanceLevel" },
                ["SystemFact"] = new[] { "LoadPattern" },
                ["SystemType"] = new[] { "TypeName", "Description" },
            };

        private static readonly Dictionary<string, string[]> RelationshipTargetsByEntity =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Cube"] = Array.Empty<string>(),
                ["Dimension"] = Array.Empty<string>(),
                ["Fact"] = Array.Empty<string>(),
                ["Measure"] = new[] { "Cube" },
                ["System"] = new[] { "SystemType" },
                ["SystemCube"] = new[] { "Cube", "System" },
                ["SystemDimension"] = new[] { "Dimension", "System" },
                ["SystemFact"] = new[] { "Fact", "System" },
                ["SystemType"] = Array.Empty<string>(),
            };

        private static ModelInstance ReadXmlWorkspaceInstance(string workspacePath)
        {
            var instance = CreateEmptyInstance();
            var entitiesByName = BuildEntityInstanceLookup(instance);
            var instanceDirectory = Path.Combine(workspacePath, "metadata", "instance");
            var shardFiles = Directory.Exists(instanceDirectory)
                ? Directory.GetFiles(instanceDirectory, "*.xml")
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();

            if (shardFiles.Length > 0)
            {
                foreach (var shardPath in shardFiles)
                {
                    var document = XDocument.Load(shardPath, LoadOptions.None);
                    ParseInstanceDocument(document, entitiesByName, sourceName: Path.GetFileName(shardPath));
                }

                return instance;
            }

            var monolithicPath = ResolveInstancePath(workspacePath);
            if (string.IsNullOrWhiteSpace(monolithicPath))
            {
                throw new FileNotFoundException($"Could not find instance XML under workspace '{workspacePath}'.");
            }

            var monolithicDocument = XDocument.Load(monolithicPath, LoadOptions.None);
            ParseInstanceDocument(monolithicDocument, entitiesByName, sourceName: Path.GetFileName(monolithicPath));
            return instance;
        }

        private static ModelInstance ReadSqlInstance(DbConnection connection, string schemaName)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var instance = CreateEmptyInstance();
            var entitiesByName = BuildEntityInstanceLookup(instance);
            var shouldClose = false;
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                shouldClose = true;
            }

            try
            {
                foreach (var entityName in EntityNames)
                {
                    ReadSqlEntityRows(connection, schemaName, entityName, entitiesByName[entityName]);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }

            return instance;
        }

        private static void ReadSqlEntityRows(
            DbConnection connection,
            string schemaName,
            string entityName,
            EntityInstance entityInstance)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    $"SELECT * FROM [{EscapeSqlIdentifier(schemaName)}].[{EscapeSqlIdentifier(entityName)}] ORDER BY [Id]";
                using (var reader = command.ExecuteReader())
                {
                    var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        columnNames.Add(reader.GetName(i));
                    }

                    if (!columnNames.Contains("Id"))
                    {
                        throw new InvalidOperationException($"Table '{schemaName}.{entityName}' does not include required column 'Id'.");
                    }

                    var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (reader.Read())
                    {
                        var idObject = reader["Id"];
                        if (idObject == DBNull.Value)
                        {
                            throw new InvalidOperationException($"Table '{schemaName}.{entityName}' contains null Id values.");
                        }

                        var idText = Convert.ToString(idObject);
                        var rowId = ParseRequiredId(entityName, idText);
                        if (!seenIds.Add(rowId.ToString()))
                        {
                            throw new InvalidOperationException($"Table '{schemaName}.{entityName}' contains duplicate Id '{idText}'.");
                        }

                        var record = new RecordInstance
                        {
                            Id = rowId.ToString(),
                        };

                        foreach (var propertyName in PropertyNamesByEntity[entityName])
                        {
                            if (!columnNames.Contains(propertyName))
                            {
                                continue;
                            }

                            var value = reader[propertyName];
                            if (value == DBNull.Value)
                            {
                                continue;
                            }

                            record.Properties.Add(new PropertyValue
                            {
                                Property = new PropertyRef { Name = propertyName },
                                Value = Convert.ToString(value),
                            });
                        }

                        foreach (var relationshipTarget in RelationshipTargetsByEntity[entityName])
                        {
                            var columnName = relationshipTarget + "Id";
                            if (!columnNames.Contains(columnName))
                            {
                                throw new InvalidOperationException(
                                    $"Table '{schemaName}.{entityName}' is missing required relationship column '{columnName}'.");
                            }

                            var relationshipValue = reader[columnName];
                            if (relationshipValue == DBNull.Value)
                            {
                                throw new InvalidOperationException(
                                    $"Table '{schemaName}.{entityName}' has null relationship value for '{columnName}' on row '{rowId}'.");
                            }

                            var relationshipIdText = Convert.ToString(relationshipValue);
                            var relatedId = ParseRequiredId(entityName, relationshipIdText);
                            record.Relationships.Add(new RelationshipValue
                            {
                                Entity = new EntityRef { Name = relationshipTarget },
                                Value = relatedId.ToString(),
                            });
                        }

                        entityInstance.Records.Add(record);
                    }
                }
            }
        }

        private static string EscapeSqlIdentifier(string value)
        {
            return value.Replace("]", "]]");
        }

        private static string ResolveInstancePath(string workspacePath)
        {
            var candidates = new[]
            {
                Path.Combine(workspacePath, "metadata", "instance.xml"),
                Path.Combine(workspacePath, "instance.xml"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static void ParseInstanceDocument(
            XDocument document,
            IDictionary<string, EntityInstance> entitiesByName,
            string sourceName)
        {
            var root = document.Root ?? throw new InvalidOperationException($"Instance XML '{sourceName}' has no root element.");
            if (!string.Equals(root.Name.LocalName, "EnterpriseBIPlatform", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Instance XML '{sourceName}' root must be 'EnterpriseBIPlatform', found '{root.Name.LocalName}'.");
            }

            foreach (var container in root.Elements())
            {
                var entityName = ResolveEntityNameFromContainer(container.Name.LocalName);
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    throw new InvalidOperationException(
                        $"Instance XML '{sourceName}' contains unknown container '{container.Name.LocalName}'.");
                }

                var entityRecords = entitiesByName[entityName].Records;
                foreach (var rowElement in container.Elements())
                {
                    if (!string.Equals(rowElement.Name.LocalName, entityName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Container '{container.Name.LocalName}' in '{sourceName}' contains row '{rowElement.Name.LocalName}', expected '{entityName}'.");
                    }

                    var record = ParseRecordElement(entityName, rowElement, sourceName);
                    if (entityRecords.Any(existing => string.Equals(existing.Id, record.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException(
                            $"Instance XML '{sourceName}' has duplicate Id '{record.Id}' for entity '{entityName}'.");
                    }

                    entityRecords.Add(record);
                }
            }
        }

        private static string ResolveEntityNameFromContainer(string containerName)
        {
            return EntityNames.FirstOrDefault(entityName =>
                string.Equals(GetPluralName(entityName), containerName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        private static string GetPluralName(string entityName)
        {
            return entityName + "s";
        }

        private static RecordInstance ParseRecordElement(string entityName, XElement rowElement, string sourceName)
        {
            var idText = (string)rowElement.Attribute("Id");
            var rowId = ParseRequiredId(entityName, idText);
            var record = new RecordInstance
            {
                Id = rowId.ToString(),
            };

            var relationshipTargets = RelationshipTargetsByEntity[entityName];
            var relationshipColumns = relationshipTargets.ToDictionary(
                target => target + "Id",
                target => target,
                StringComparer.OrdinalIgnoreCase);
            var seenRelationshipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in rowElement.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var attributeName = attribute.Name.LocalName;
                if (!relationshipColumns.TryGetValue(attributeName, out var targetEntity))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' has unsupported attribute '{attributeName}'.");
                }

                if (!seenRelationshipColumns.Add(attributeName))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' has duplicate relationship attribute '{attributeName}'.");
                }

                var relatedId = ParseRequiredId(entityName, attribute.Value);
                record.Relationships.Add(new RelationshipValue
                {
                    Entity = new EntityRef { Name = targetEntity },
                    Value = relatedId.ToString(),
                });
            }

            foreach (var expectedRelationship in relationshipTargets)
            {
                if (!record.Relationships.Any(relationship =>
                        string.Equals(relationship.Entity.Name, expectedRelationship, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' is missing required relationship '{expectedRelationship}Id'.");
                }
            }

            var allowedProperties = new HashSet<string>(PropertyNamesByEntity[entityName], StringComparer.OrdinalIgnoreCase);
            var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var childElement in rowElement.Elements())
            {
                var propertyName = childElement.Name.LocalName;
                if (relationshipColumns.ContainsKey(propertyName))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' contains relationship element '{propertyName}'. Relationships must be attributes.");
                }

                if (!allowedProperties.Contains(propertyName))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' has unknown property '{propertyName}'.");
                }

                if (!seenProperties.Add(propertyName))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityName}' row '{rowId}' in '{sourceName}' has duplicate property '{propertyName}'.");
                }

                record.Properties.Add(new PropertyValue
                {
                    Property = new PropertyRef { Name = propertyName },
                    Value = childElement.Value,
                });
            }

            return record;
        }

        private static ModelInstance CreateEmptyInstance()
        {
            var instance = new ModelInstance();
            foreach (var entityName in EntityNames)
            {
                instance.Entities.Add(new EntityInstance
                {
                    Entity = new EntityRef
                    {
                        Name = entityName,
                    },
                });
            }

            return instance;
        }

        private static Dictionary<string, EntityInstance> BuildEntityInstanceLookup(ModelInstance instance)
        {
            return instance.Entities.ToDictionary(
                entity => entity.Entity.Name,
                entity => entity,
                StringComparer.OrdinalIgnoreCase);
        }

        private static EnterpriseBIPlatformData BuildData(ModelInstance instance)
        {
            var cubeRows = BuildCubeRows(instance);
            var cubes = new Cubes(cubeRows);
            var dimensionRows = BuildDimensionRows(instance);
            var dimensions = new Dimensions(dimensionRows);
            var factRows = BuildFactRows(instance);
            var facts = new Facts(factRows);
            var measureRows = BuildMeasureRows(instance);
            var measures = new Measures(measureRows);
            var systemRows = BuildSystemRows(instance);
            var systems = new Systems(systemRows);
            var systemCubeRows = BuildSystemCubeRows(instance);
            var systemCubes = new SystemCubes(systemCubeRows);
            var systemDimensionRows = BuildSystemDimensionRows(instance);
            var systemDimensions = new SystemDimensions(systemDimensionRows);
            var systemFactRows = BuildSystemFactRows(instance);
            var systemFacts = new SystemFacts(systemFactRows);
            var systemTypeRows = BuildSystemTypeRows(instance);
            var systemTypes = new SystemTypes(systemTypeRows);

            var data = new EnterpriseBIPlatformData(
                cubes,
                dimensions,
                facts,
                measures,
                systems,
                systemCubes,
                systemDimensions,
                systemFacts,
                systemTypes);

            ResolveNavigations(data);
            return data;
        }

        private static List<Cube> BuildCubeRows(ModelInstance instance)
        {
            var rows = new List<Cube>();
            foreach (var record in GetEntityRecords(instance, "Cube"))
            {
                var id = ParseRequiredId("Cube", record.Id);
                var cubeName = GetRequiredPropertyValue(record, "Cube", "CubeName", id);
                var purpose = GetOptionalPropertyValue(record, "Purpose");
                var refreshMode = GetOptionalPropertyValue(record, "RefreshMode");
                rows.Add(new Cube(id, cubeName, purpose, refreshMode));
            }

            return rows;
        }

        private static List<Dimension> BuildDimensionRows(ModelInstance instance)
        {
            var rows = new List<Dimension>();
            foreach (var record in GetEntityRecords(instance, "Dimension"))
            {
                var id = ParseRequiredId("Dimension", record.Id);
                var dimensionName = GetRequiredPropertyValue(record, "Dimension", "DimensionName", id);
                var isConformed = GetRequiredPropertyValue(record, "Dimension", "IsConformed", id);
                var hierarchyCount = GetOptionalPropertyValue(record, "HierarchyCount");
                rows.Add(new Dimension(id, dimensionName, isConformed, hierarchyCount));
            }

            return rows;
        }

        private static List<Fact> BuildFactRows(ModelInstance instance)
        {
            var rows = new List<Fact>();
            foreach (var record in GetEntityRecords(instance, "Fact"))
            {
                var id = ParseRequiredId("Fact", record.Id);
                var factName = GetRequiredPropertyValue(record, "Fact", "FactName", id);
                var grain = GetOptionalPropertyValue(record, "Grain");
                var measureCount = GetOptionalPropertyValue(record, "MeasureCount");
                var businessArea = GetOptionalPropertyValue(record, "BusinessArea");
                rows.Add(new Fact(id, factName, grain, measureCount, businessArea));
            }

            return rows;
        }

        private static List<Measure> BuildMeasureRows(ModelInstance instance)
        {
            var rows = new List<Measure>();
            foreach (var record in GetEntityRecords(instance, "Measure"))
            {
                var id = ParseRequiredId("Measure", record.Id);
                var measureName = GetRequiredPropertyValue(record, "Measure", "MeasureName", id);
                var mDX = GetOptionalPropertyValue(record, "MDX");
                var cubeId = GetRequiredRelationshipId(record, "Measure", "Cube", id);
                rows.Add(new Measure(id, measureName, mDX, cubeId));
            }

            return rows;
        }

        private static List<System> BuildSystemRows(ModelInstance instance)
        {
            var rows = new List<System>();
            foreach (var record in GetEntityRecords(instance, "System"))
            {
                var id = ParseRequiredId("System", record.Id);
                var systemName = GetRequiredPropertyValue(record, "System", "SystemName", id);
                var version = GetOptionalPropertyValue(record, "Version");
                var deploymentDate = GetOptionalPropertyValue(record, "DeploymentDate");
                var systemTypeId = GetRequiredRelationshipId(record, "System", "SystemType", id);
                rows.Add(new System(id, systemName, version, deploymentDate, systemTypeId));
            }

            return rows;
        }

        private static List<SystemCube> BuildSystemCubeRows(ModelInstance instance)
        {
            var rows = new List<SystemCube>();
            foreach (var record in GetEntityRecords(instance, "SystemCube"))
            {
                var id = ParseRequiredId("SystemCube", record.Id);
                var processingMode = GetOptionalPropertyValue(record, "ProcessingMode");
                var cubeId = GetRequiredRelationshipId(record, "SystemCube", "Cube", id);
                var systemId = GetRequiredRelationshipId(record, "SystemCube", "System", id);
                rows.Add(new SystemCube(id, processingMode, cubeId, systemId));
            }

            return rows;
        }

        private static List<SystemDimension> BuildSystemDimensionRows(ModelInstance instance)
        {
            var rows = new List<SystemDimension>();
            foreach (var record in GetEntityRecords(instance, "SystemDimension"))
            {
                var id = ParseRequiredId("SystemDimension", record.Id);
                var conformanceLevel = GetOptionalPropertyValue(record, "ConformanceLevel");
                var dimensionId = GetRequiredRelationshipId(record, "SystemDimension", "Dimension", id);
                var systemId = GetRequiredRelationshipId(record, "SystemDimension", "System", id);
                rows.Add(new SystemDimension(id, conformanceLevel, dimensionId, systemId));
            }

            return rows;
        }

        private static List<SystemFact> BuildSystemFactRows(ModelInstance instance)
        {
            var rows = new List<SystemFact>();
            foreach (var record in GetEntityRecords(instance, "SystemFact"))
            {
                var id = ParseRequiredId("SystemFact", record.Id);
                var loadPattern = GetOptionalPropertyValue(record, "LoadPattern");
                var factId = GetRequiredRelationshipId(record, "SystemFact", "Fact", id);
                var systemId = GetRequiredRelationshipId(record, "SystemFact", "System", id);
                rows.Add(new SystemFact(id, loadPattern, factId, systemId));
            }

            return rows;
        }

        private static List<SystemType> BuildSystemTypeRows(ModelInstance instance)
        {
            var rows = new List<SystemType>();
            foreach (var record in GetEntityRecords(instance, "SystemType"))
            {
                var id = ParseRequiredId("SystemType", record.Id);
                var typeName = GetRequiredPropertyValue(record, "SystemType", "TypeName", id);
                var description = GetOptionalPropertyValue(record, "Description");
                rows.Add(new SystemType(id, typeName, description));
            }

            return rows;
        }

        private static void ResolveNavigations(EnterpriseBIPlatformData data)
        {
            foreach (var row in data.Measures)
            {
                Cube cube;
                if (!data.Cubes.TryGetId(row.CubeId, out cube))
                {
                    throw new InvalidOperationException($"Relationship 'Measure->Cube' on row '{row.Id}' references missing target '{row.CubeId}'.");
                }

                row.Cube = cube;
            }
            foreach (var row in data.Systems)
            {
                SystemType systemType;
                if (!data.SystemTypes.TryGetId(row.SystemTypeId, out systemType))
                {
                    throw new InvalidOperationException($"Relationship 'System->SystemType' on row '{row.Id}' references missing target '{row.SystemTypeId}'.");
                }

                row.SystemType = systemType;
            }
            foreach (var row in data.SystemCubes)
            {
                Cube cube;
                if (!data.Cubes.TryGetId(row.CubeId, out cube))
                {
                    throw new InvalidOperationException($"Relationship 'SystemCube->Cube' on row '{row.Id}' references missing target '{row.CubeId}'.");
                }

                row.Cube = cube;

                System system;
                if (!data.Systems.TryGetId(row.SystemId, out system))
                {
                    throw new InvalidOperationException($"Relationship 'SystemCube->System' on row '{row.Id}' references missing target '{row.SystemId}'.");
                }

                row.System = system;
            }
            foreach (var row in data.SystemDimensions)
            {
                Dimension dimension;
                if (!data.Dimensions.TryGetId(row.DimensionId, out dimension))
                {
                    throw new InvalidOperationException($"Relationship 'SystemDimension->Dimension' on row '{row.Id}' references missing target '{row.DimensionId}'.");
                }

                row.Dimension = dimension;

                System system;
                if (!data.Systems.TryGetId(row.SystemId, out system))
                {
                    throw new InvalidOperationException($"Relationship 'SystemDimension->System' on row '{row.Id}' references missing target '{row.SystemId}'.");
                }

                row.System = system;
            }
            foreach (var row in data.SystemFacts)
            {
                Fact fact;
                if (!data.Facts.TryGetId(row.FactId, out fact))
                {
                    throw new InvalidOperationException($"Relationship 'SystemFact->Fact' on row '{row.Id}' references missing target '{row.FactId}'.");
                }

                row.Fact = fact;

                System system;
                if (!data.Systems.TryGetId(row.SystemId, out system))
                {
                    throw new InvalidOperationException($"Relationship 'SystemFact->System' on row '{row.Id}' references missing target '{row.SystemId}'.");
                }

                row.System = system;
            }
        }

        private static IEnumerable<RecordInstance> GetEntityRecords(ModelInstance instance, string entityName)
        {
            var entityInstance = instance.Entities.FirstOrDefault(item =>
                item != null && item.Entity != null && string.Equals(item.Entity.Name, entityName, StringComparison.OrdinalIgnoreCase));
            if (entityInstance == null)
            {
                return Enumerable.Empty<RecordInstance>();
            }

            return entityInstance.Records
                .Where(item => item != null)
                .OrderBy(item => ParseRequiredId(entityName, item.Id))
                .ToArray();
        }

        private static int ParseRequiredId(string entityName, string idText)
        {
            int id;
            if (!int.TryParse(idText, out id) || id <= 0)
            {
                throw new InvalidOperationException($"Entity '{entityName}' contains invalid Id '{idText}'.");
            }

            return id;
        }

        private static string GetRequiredPropertyValue(RecordInstance record, string entityName, string propertyName, int rowId)
        {
            var property = record.Properties.FirstOrDefault(item =>
                item != null && item.Property != null && string.Equals(item.Property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property == null || string.IsNullOrWhiteSpace(property.Value))
            {
                throw new InvalidOperationException($"Entity '{entityName}' row '{rowId}' is missing required property '{propertyName}'.");
            }

            return property.Value;
        }

        private static string GetOptionalPropertyValue(RecordInstance record, string propertyName)
        {
            var property = record.Properties.FirstOrDefault(item =>
                item != null && item.Property != null && string.Equals(item.Property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return property != null ? property.Value : null;
        }

        private static int GetRequiredRelationshipId(RecordInstance record, string sourceEntity, string targetEntity, int rowId)
        {
            var matches = record.Relationships
                .Where(item => item != null && item.Entity != null && string.Equals(item.Entity.Name, targetEntity, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new InvalidOperationException($"Entity '{sourceEntity}' row '{rowId}' is missing required relationship '{targetEntity}'.");
            }

            if (matches.Length > 1)
            {
                throw new InvalidOperationException($"Entity '{sourceEntity}' row '{rowId}' has multiple '{targetEntity}' relationships.");
            }

            var rawValue = matches[0].Value;
            int relationshipId;
            if (!int.TryParse(rawValue, out relationshipId) || relationshipId <= 0)
            {
                throw new InvalidOperationException($"Entity '{sourceEntity}' row '{rowId}' has invalid relationship id '{rawValue}' for '{targetEntity}'.");
            }

            return relationshipId;
        }

        private sealed class ModelInstance
        {
            public List<EntityInstance> Entities { get; } = new List<EntityInstance>();
        }

        private sealed class EntityInstance
        {
            public EntityRef Entity { get; set; } = new EntityRef();
            public List<RecordInstance> Records { get; } = new List<RecordInstance>();
        }

        private sealed class RecordInstance
        {
            public string Id { get; set; } = string.Empty;
            public List<PropertyValue> Properties { get; } = new List<PropertyValue>();
            public List<RelationshipValue> Relationships { get; } = new List<RelationshipValue>();
        }

        private sealed class PropertyValue
        {
            public PropertyRef Property { get; set; } = new PropertyRef();
            public string Value { get; set; } = string.Empty;
        }

        private sealed class RelationshipValue
        {
            public EntityRef Entity { get; set; } = new EntityRef();
            public string Value { get; set; } = string.Empty;
        }

        private sealed class EntityRef
        {
            public string Name { get; set; } = string.Empty;
        }

        private sealed class PropertyRef
        {
            public string Name { get; set; } = string.Empty;
        }

    }

    public sealed class Cube
    {
        internal Cube(int id, string cubeName, string purpose, string refreshMode)
        {
            Id = id;
            CubeName = cubeName;
            Purpose = purpose;
            RefreshMode = refreshMode;
        }

        public int Id { get; }
        public string CubeName { get; }
        public string Purpose { get; }
        public string RefreshMode { get; }
        public string Name { get { return CubeName; } }
    }

    public sealed class Dimension
    {
        internal Dimension(int id, string dimensionName, string isConformed, string hierarchyCount)
        {
            Id = id;
            DimensionName = dimensionName;
            IsConformed = isConformed;
            HierarchyCount = hierarchyCount;
        }

        public int Id { get; }
        public string DimensionName { get; }
        public string IsConformed { get; }
        public string HierarchyCount { get; }
        public string Name { get { return DimensionName; } }
    }

    public sealed class Fact
    {
        internal Fact(int id, string factName, string grain, string measureCount, string businessArea)
        {
            Id = id;
            FactName = factName;
            Grain = grain;
            MeasureCount = measureCount;
            BusinessArea = businessArea;
        }

        public int Id { get; }
        public string FactName { get; }
        public string Grain { get; }
        public string MeasureCount { get; }
        public string BusinessArea { get; }
        public string Name { get { return FactName; } }
    }

    public sealed class Measure
    {
        internal Measure(int id, string measureName, string mDX, int cubeId)
        {
            Id = id;
            MeasureName = measureName;
            MDX = mDX;
            CubeId = cubeId;
        }

        public int Id { get; }
        public string MeasureName { get; }
        public string MDX { get; }
        public string Name { get { return MeasureName; } }
        public int CubeId { get; }
        public Cube Cube { get; internal set; }
    }

    public sealed class System
    {
        internal System(int id, string systemName, string version, string deploymentDate, int systemTypeId)
        {
            Id = id;
            SystemName = systemName;
            Version = version;
            DeploymentDate = deploymentDate;
            SystemTypeId = systemTypeId;
        }

        public int Id { get; }
        public string SystemName { get; }
        public string Version { get; }
        public string DeploymentDate { get; }
        public string Name { get { return SystemName; } }
        public int SystemTypeId { get; }
        public SystemType SystemType { get; internal set; }
    }

    public sealed class SystemCube
    {
        internal SystemCube(int id, string processingMode, int cubeId, int systemId)
        {
            Id = id;
            ProcessingMode = processingMode;
            CubeId = cubeId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string ProcessingMode { get; }
        public int CubeId { get; }
        public Cube Cube { get; internal set; }
        public int SystemId { get; }
        public System System { get; internal set; }
    }

    public sealed class SystemDimension
    {
        internal SystemDimension(int id, string conformanceLevel, int dimensionId, int systemId)
        {
            Id = id;
            ConformanceLevel = conformanceLevel;
            DimensionId = dimensionId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string ConformanceLevel { get; }
        public int DimensionId { get; }
        public Dimension Dimension { get; internal set; }
        public int SystemId { get; }
        public System System { get; internal set; }
    }

    public sealed class SystemFact
    {
        internal SystemFact(int id, string loadPattern, int factId, int systemId)
        {
            Id = id;
            LoadPattern = loadPattern;
            FactId = factId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string LoadPattern { get; }
        public int FactId { get; }
        public Fact Fact { get; internal set; }
        public int SystemId { get; }
        public System System { get; internal set; }
    }

    public sealed class SystemType
    {
        internal SystemType(int id, string typeName, string description)
        {
            Id = id;
            TypeName = typeName;
            Description = description;
        }

        public int Id { get; }
        public string TypeName { get; }
        public string Description { get; }
    }

    public sealed class Cubes : IEnumerable<Cube>
    {
        private readonly List<Cube> _rows;
        private readonly Dictionary<int, Cube> _rowsById;

        internal Cubes(IEnumerable<Cube> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, Cube>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in Cube list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public Cube GetId(int id)
        {
            Cube row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"Cube id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out Cube row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<Cube> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class Dimensions : IEnumerable<Dimension>
    {
        private readonly List<Dimension> _rows;
        private readonly Dictionary<int, Dimension> _rowsById;

        internal Dimensions(IEnumerable<Dimension> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, Dimension>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in Dimension list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public Dimension GetId(int id)
        {
            Dimension row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"Dimension id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out Dimension row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<Dimension> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class Facts : IEnumerable<Fact>
    {
        private readonly List<Fact> _rows;
        private readonly Dictionary<int, Fact> _rowsById;

        internal Facts(IEnumerable<Fact> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, Fact>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in Fact list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public Fact GetId(int id)
        {
            Fact row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"Fact id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out Fact row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<Fact> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class Measures : IEnumerable<Measure>
    {
        private readonly List<Measure> _rows;
        private readonly Dictionary<int, Measure> _rowsById;

        internal Measures(IEnumerable<Measure> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, Measure>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in Measure list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public Measure GetId(int id)
        {
            Measure row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"Measure id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out Measure row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<Measure> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class Systems : IEnumerable<System>
    {
        private readonly List<System> _rows;
        private readonly Dictionary<int, System> _rowsById;

        internal Systems(IEnumerable<System> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, System>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in System list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public System GetId(int id)
        {
            System row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"System id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out System row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<System> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class SystemCubes : IEnumerable<SystemCube>
    {
        private readonly List<SystemCube> _rows;
        private readonly Dictionary<int, SystemCube> _rowsById;

        internal SystemCubes(IEnumerable<SystemCube> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, SystemCube>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in SystemCube list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public SystemCube GetId(int id)
        {
            SystemCube row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"SystemCube id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out SystemCube row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<SystemCube> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class SystemDimensions : IEnumerable<SystemDimension>
    {
        private readonly List<SystemDimension> _rows;
        private readonly Dictionary<int, SystemDimension> _rowsById;

        internal SystemDimensions(IEnumerable<SystemDimension> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, SystemDimension>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in SystemDimension list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public SystemDimension GetId(int id)
        {
            SystemDimension row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"SystemDimension id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out SystemDimension row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<SystemDimension> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class SystemFacts : IEnumerable<SystemFact>
    {
        private readonly List<SystemFact> _rows;
        private readonly Dictionary<int, SystemFact> _rowsById;

        internal SystemFacts(IEnumerable<SystemFact> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, SystemFact>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in SystemFact list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public SystemFact GetId(int id)
        {
            SystemFact row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"SystemFact id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out SystemFact row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<SystemFact> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class SystemTypes : IEnumerable<SystemType>
    {
        private readonly List<SystemType> _rows;
        private readonly Dictionary<int, SystemType> _rowsById;

        internal SystemTypes(IEnumerable<SystemType> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _rows = rows.OrderBy(item => item.Id).ToList();
            _rowsById = new Dictionary<int, SystemType>();
            foreach (var row in _rows)
            {
                if (_rowsById.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"Duplicate Id '{row.Id}' in SystemType list.");
                }

                _rowsById[row.Id] = row;
            }
        }

        public SystemType GetId(int id)
        {
            SystemType row;
            if (!_rowsById.TryGetValue(id, out row))
            {
                throw new KeyNotFoundException($"SystemType id '{id}' was not found.");
            }

            return row;
        }

        public bool TryGetId(int id, out SystemType row)
        {
            return _rowsById.TryGetValue(id, out row);
        }

        public IEnumerator<SystemType> GetEnumerator()
        {
            return _rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}
