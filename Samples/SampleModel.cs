using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Metadata.Framework.Generic;

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
        private static EnterpriseBIPlatformData _data;
        private static readonly object SyncRoot = new object();

        public static Cubes Cubes
        {
            get { return EnsureLoaded().Cubes; }
        }
        public static Dimensions Dimensions
        {
            get { return EnsureLoaded().Dimensions; }
        }
        public static Facts Facts
        {
            get { return EnsureLoaded().Facts; }
        }
        public static Measures Measures
        {
            get { return EnsureLoaded().Measures; }
        }
        public static Systems Systems
        {
            get { return EnsureLoaded().Systems; }
        }
        public static SystemCubes SystemCubes
        {
            get { return EnsureLoaded().SystemCubes; }
        }
        public static SystemDimensions SystemDimensions
        {
            get { return EnsureLoaded().SystemDimensions; }
        }
        public static SystemFacts SystemFacts
        {
            get { return EnsureLoaded().SystemFacts; }
        }
        public static SystemTypes SystemTypes
        {
            get { return EnsureLoaded().SystemTypes; }
        }

        internal static void Install(EnterpriseBIPlatformData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            lock (SyncRoot)
            {
                if (_data != null)
                {
                    throw new InvalidOperationException("EnterpriseBIPlatform is already loaded. Double install is not allowed.");
                }

                _data = data;
            }
        }

        private static EnterpriseBIPlatformData EnsureLoaded()
        {
            var data = _data;
            if (data == null)
            {
                throw new InvalidOperationException("EnterpriseBIPlatform is not loaded. Call EnterpriseBIPlatformModel.LoadFromXml/LoadFromSql first.");
            }

            return data;
        }
    }

    public static class EnterpriseBIPlatformModel
    {
        public static void LoadFromXml(string workspacePath)
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

            var modelPath = ResolveModelPath(fullWorkspacePath);
            var modelResult = new Reader().Read(modelPath);
            EnsureNoErrors(modelResult.Errors, "model XML load");

            var instanceResult = new InstanceReader().ReadWorkspace(fullWorkspacePath, modelResult.Model);
            EnsureNoErrors(instanceResult.Errors, "instance XML load");

            var data = BuildData(instanceResult.ModelInstance);
            EnterpriseBIPlatform.Install(data);
        }

        public static void LoadFromSql(DbConnection connection, string schemaName = "dbo")
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

            var modelResult = new Reader().ReadFromDatabase(connection.ConnectionString, schemaName);
            EnsureNoErrors(modelResult.Errors, "SQL model load");

            var instance = new DatabaseInstanceReader().Read(connection.ConnectionString, modelResult.Model, schemaName);
            var data = BuildData(instance);
            EnterpriseBIPlatform.Install(data);
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
                var cubeId = GetOptionalRelationshipId(record, "Measure", "Cube", id);
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
                var systemTypeId = GetOptionalRelationshipId(record, "System", "SystemType", id);
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
                var cubeId = GetOptionalRelationshipId(record, "SystemCube", "Cube", id);
                var systemId = GetOptionalRelationshipId(record, "SystemCube", "System", id);
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
                var dimensionId = GetOptionalRelationshipId(record, "SystemDimension", "Dimension", id);
                var systemId = GetOptionalRelationshipId(record, "SystemDimension", "System", id);
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
                var factId = GetOptionalRelationshipId(record, "SystemFact", "Fact", id);
                var systemId = GetOptionalRelationshipId(record, "SystemFact", "System", id);
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
                if (row.CubeId.HasValue)
                {
                    Cube cube;
                    if (!data.Cubes.TryGetId(row.CubeId.Value, out cube))
                    {
                        throw new InvalidOperationException($"Relationship 'Measure->Cube' on row '{row.Id}' references missing target '{row.CubeId.Value}'.");
                    }

                    row.Cube = cube;
                }
            }
            foreach (var row in data.Systems)
            {
                if (row.SystemTypeId.HasValue)
                {
                    SystemType systemType;
                    if (!data.SystemTypes.TryGetId(row.SystemTypeId.Value, out systemType))
                    {
                        throw new InvalidOperationException($"Relationship 'System->SystemType' on row '{row.Id}' references missing target '{row.SystemTypeId.Value}'.");
                    }

                    row.SystemType = systemType;
                }
            }
            foreach (var row in data.SystemCubes)
            {
                if (row.CubeId.HasValue)
                {
                    Cube cube;
                    if (!data.Cubes.TryGetId(row.CubeId.Value, out cube))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemCube->Cube' on row '{row.Id}' references missing target '{row.CubeId.Value}'.");
                    }

                    row.Cube = cube;
                }
                if (row.SystemId.HasValue)
                {
                    System system;
                    if (!data.Systems.TryGetId(row.SystemId.Value, out system))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemCube->System' on row '{row.Id}' references missing target '{row.SystemId.Value}'.");
                    }

                    row.System = system;
                }
            }
            foreach (var row in data.SystemDimensions)
            {
                if (row.DimensionId.HasValue)
                {
                    Dimension dimension;
                    if (!data.Dimensions.TryGetId(row.DimensionId.Value, out dimension))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemDimension->Dimension' on row '{row.Id}' references missing target '{row.DimensionId.Value}'.");
                    }

                    row.Dimension = dimension;
                }
                if (row.SystemId.HasValue)
                {
                    System system;
                    if (!data.Systems.TryGetId(row.SystemId.Value, out system))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemDimension->System' on row '{row.Id}' references missing target '{row.SystemId.Value}'.");
                    }

                    row.System = system;
                }
            }
            foreach (var row in data.SystemFacts)
            {
                if (row.FactId.HasValue)
                {
                    Fact fact;
                    if (!data.Facts.TryGetId(row.FactId.Value, out fact))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemFact->Fact' on row '{row.Id}' references missing target '{row.FactId.Value}'.");
                    }

                    row.Fact = fact;
                }
                if (row.SystemId.HasValue)
                {
                    System system;
                    if (!data.Systems.TryGetId(row.SystemId.Value, out system))
                    {
                        throw new InvalidOperationException($"Relationship 'SystemFact->System' on row '{row.Id}' references missing target '{row.SystemId.Value}'.");
                    }

                    row.System = system;
                }
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

        private static int? GetOptionalRelationshipId(RecordInstance record, string sourceEntity, string targetEntity, int rowId)
        {
            var matches = record.Relationships
                .Where(item => item != null && item.Entity != null && string.Equals(item.Entity.Name, targetEntity, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
            {
                return null;
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

        private static void EnsureNoErrors(IList<string> errors, string context)
        {
            if (errors == null || errors.Count == 0)
            {
                return;
            }

            var message = string.Join(Environment.NewLine, errors.Select(error => " - " + error));
            throw new InvalidOperationException($"{context} failed:{Environment.NewLine}{message}");
        }

        private static string ResolveModelPath(string workspacePath)
        {
            var candidates = new[]
            {
                Path.Combine(workspacePath, "metadata", "model.xml"),
                Path.Combine(workspacePath, "SampleModel.xml"),
                Path.Combine(workspacePath, "model.xml"),
            };

            var match = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(match))
            {
                throw new FileNotFoundException($"Could not find model XML under workspace '{workspacePath}'.");
            }

            return Path.GetFullPath(match);
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
        internal Measure(int id, string measureName, string mDX, int? cubeId)
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
        public int? CubeId { get; }
        public Cube Cube { get; internal set; }
    }

    public sealed class System
    {
        internal System(int id, string systemName, string version, string deploymentDate, int? systemTypeId)
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
        public int? SystemTypeId { get; }
        public SystemType SystemType { get; internal set; }
    }

    public sealed class SystemCube
    {
        internal SystemCube(int id, string processingMode, int? cubeId, int? systemId)
        {
            Id = id;
            ProcessingMode = processingMode;
            CubeId = cubeId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string ProcessingMode { get; }
        public int? CubeId { get; }
        public Cube Cube { get; internal set; }
        public int? SystemId { get; }
        public System System { get; internal set; }
    }

    public sealed class SystemDimension
    {
        internal SystemDimension(int id, string conformanceLevel, int? dimensionId, int? systemId)
        {
            Id = id;
            ConformanceLevel = conformanceLevel;
            DimensionId = dimensionId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string ConformanceLevel { get; }
        public int? DimensionId { get; }
        public Dimension Dimension { get; internal set; }
        public int? SystemId { get; }
        public System System { get; internal set; }
    }

    public sealed class SystemFact
    {
        internal SystemFact(int id, string loadPattern, int? factId, int? systemId)
        {
            Id = id;
            LoadPattern = loadPattern;
            FactId = factId;
            SystemId = systemId;
        }

        public int Id { get; }
        public string LoadPattern { get; }
        public int? FactId { get; }
        public Fact Fact { get; internal set; }
        public int? SystemId { get; }
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
