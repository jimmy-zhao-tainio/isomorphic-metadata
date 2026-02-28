using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EnterpriseBIPlatform
{
    public static class EnterpriseBIPlatform
    {
        private static readonly EnterpriseBIPlatformInstance _builtIn = EnterpriseBIPlatformBuiltInFactory.Create();

        public static EnterpriseBIPlatformInstance BuiltIn => _builtIn;
        public static IReadOnlyList<Cube> Cubes => _builtIn.Cubes;
        public static IReadOnlyList<Dimension> Dimensions => _builtIn.Dimensions;
        public static IReadOnlyList<Fact> Facts => _builtIn.Facts;
        public static IReadOnlyList<Measure> Measures => _builtIn.Measures;
        public static IReadOnlyList<System> Systems => _builtIn.Systems;
        public static IReadOnlyList<SystemCube> SystemCubes => _builtIn.SystemCubes;
        public static IReadOnlyList<SystemDimension> SystemDimensions => _builtIn.SystemDimensions;
        public static IReadOnlyList<SystemFact> SystemFacts => _builtIn.SystemFacts;
        public static IReadOnlyList<SystemType> SystemTypes => _builtIn.SystemTypes;
    }

    public sealed class EnterpriseBIPlatformInstance
    {
        internal EnterpriseBIPlatformInstance(
            IReadOnlyList<Cube> cubes,
            IReadOnlyList<Dimension> dimensions,
            IReadOnlyList<Fact> facts,
            IReadOnlyList<Measure> measures,
            IReadOnlyList<System> systems,
            IReadOnlyList<SystemCube> systemCubes,
            IReadOnlyList<SystemDimension> systemDimensions,
            IReadOnlyList<SystemFact> systemFacts,
            IReadOnlyList<SystemType> systemTypes
        )
        {
            Cubes = cubes;
            Dimensions = dimensions;
            Facts = facts;
            Measures = measures;
            Systems = systems;
            SystemCubes = systemCubes;
            SystemDimensions = systemDimensions;
            SystemFacts = systemFacts;
            SystemTypes = systemTypes;
        }

        public IReadOnlyList<Cube> Cubes { get; }
        public IReadOnlyList<Dimension> Dimensions { get; }
        public IReadOnlyList<Fact> Facts { get; }
        public IReadOnlyList<Measure> Measures { get; }
        public IReadOnlyList<System> Systems { get; }
        public IReadOnlyList<SystemCube> SystemCubes { get; }
        public IReadOnlyList<SystemDimension> SystemDimensions { get; }
        public IReadOnlyList<SystemFact> SystemFacts { get; }
        public IReadOnlyList<SystemType> SystemTypes { get; }
    }

    internal static class EnterpriseBIPlatformBuiltInFactory
    {
        internal static EnterpriseBIPlatformInstance Create()
        {
            var cubes = new List<Cube>
            {
                new Cube
                {
                    Id = "1",
                    CubeName = "Sales Performance",
                    Purpose = "Monthly revenue and margin tracking.",
                    RefreshMode = "Scheduled",
                },
                new Cube
                {
                    Id = "2",
                    CubeName = "Finance Overview",
                    Purpose = "Quarterly financial statements.",
                    RefreshMode = "Manual",
                },
                new Cube
                {
                    Id = "3",
                    CubeName = "Ops Cube2",
                    Purpose = "",
                    RefreshMode = "",
                },
                new Cube
                {
                    Id = "4",
                    CubeName = "Ops Cube2",
                    Purpose = "",
                    RefreshMode = "",
                },
                new Cube
                {
                    Id = "5",
                    CubeName = "Ops Cube4",
                    Purpose = "",
                    RefreshMode = "",
                },
            };

            var dimensions = new List<Dimension>
            {
                new Dimension
                {
                    Id = "1",
                    DimensionName = "Customer",
                    HierarchyCount = "2",
                    IsConformed = "True",
                },
                new Dimension
                {
                    Id = "2",
                    DimensionName = "Product",
                    HierarchyCount = "1",
                    IsConformed = "False",
                },
            };

            var facts = new List<Fact>
            {
                new Fact
                {
                    Id = "1",
                    BusinessArea = "",
                    FactName = "SalesFact",
                    Grain = "Order Line",
                    MeasureCount = "12",
                },
            };

            var measures = new List<Measure>
            {
                new Measure
                {
                    Id = "1",
                    MDX = "count",
                    MeasureName = "number_of_things",
                    CubeId = "1",
                },
            };

            var systems = new List<System>
            {
                new System
                {
                    Id = "1",
                    DeploymentDate = "2024-11-15",
                    SystemName = "Enterprise Analytics Platform",
                    Version = "2.1",
                    SystemTypeId = "1",
                },
                new System
                {
                    Id = "2",
                    DeploymentDate = "2023-06-01",
                    SystemName = "Analytics Sandbox",
                    Version = "1.4",
                    SystemTypeId = "2",
                },
            };

            var systemCubes = new List<SystemCube>
            {
                new SystemCube
                {
                    Id = "1",
                    ProcessingMode = "InMemory",
                    CubeId = "1",
                    SystemId = "1",
                },
                new SystemCube
                {
                    Id = "2",
                    ProcessingMode = "DirectQuery",
                    CubeId = "2",
                    SystemId = "2",
                },
            };

            var systemDimensions = new List<SystemDimension>
            {
                new SystemDimension
                {
                    Id = "1",
                    ConformanceLevel = "Enterprise",
                    DimensionId = "1",
                    SystemId = "1",
                },
                new SystemDimension
                {
                    Id = "2",
                    ConformanceLevel = "Sandbox",
                    DimensionId = "2",
                    SystemId = "2",
                },
            };

            var systemFacts = new List<SystemFact>
            {
                new SystemFact
                {
                    Id = "1",
                    LoadPattern = "Incremental",
                    FactId = "1",
                    SystemId = "1",
                },
            };

            var systemTypes = new List<SystemType>
            {
                new SystemType
                {
                    Id = "1",
                    Description = "Managed within the corporate data center.",
                    TypeName = "Internal",
                },
                new SystemType
                {
                    Id = "2",
                    Description = "Hosted by a third-party vendor.",
                    TypeName = "External",
                },
            };

            var cubesById = new Dictionary<string, Cube>(global::System.StringComparer.Ordinal);
            foreach (var row in cubes)
            {
                cubesById[row.Id] = row;
            }

            var dimensionsById = new Dictionary<string, Dimension>(global::System.StringComparer.Ordinal);
            foreach (var row in dimensions)
            {
                dimensionsById[row.Id] = row;
            }

            var factsById = new Dictionary<string, Fact>(global::System.StringComparer.Ordinal);
            foreach (var row in facts)
            {
                factsById[row.Id] = row;
            }

            var measuresById = new Dictionary<string, Measure>(global::System.StringComparer.Ordinal);
            foreach (var row in measures)
            {
                measuresById[row.Id] = row;
            }

            var systemsById = new Dictionary<string, System>(global::System.StringComparer.Ordinal);
            foreach (var row in systems)
            {
                systemsById[row.Id] = row;
            }

            var systemCubesById = new Dictionary<string, SystemCube>(global::System.StringComparer.Ordinal);
            foreach (var row in systemCubes)
            {
                systemCubesById[row.Id] = row;
            }

            var systemDimensionsById = new Dictionary<string, SystemDimension>(global::System.StringComparer.Ordinal);
            foreach (var row in systemDimensions)
            {
                systemDimensionsById[row.Id] = row;
            }

            var systemFactsById = new Dictionary<string, SystemFact>(global::System.StringComparer.Ordinal);
            foreach (var row in systemFacts)
            {
                systemFactsById[row.Id] = row;
            }

            var systemTypesById = new Dictionary<string, SystemType>(global::System.StringComparer.Ordinal);
            foreach (var row in systemTypes)
            {
                systemTypesById[row.Id] = row;
            }

            foreach (var row in measures)
            {
                row.Cube = RequireTarget(
                    cubesById,
                    row.CubeId,
                    "Measure",
                    row.Id,
                    "CubeId");
            }

            foreach (var row in systems)
            {
                row.SystemType = RequireTarget(
                    systemTypesById,
                    row.SystemTypeId,
                    "System",
                    row.Id,
                    "SystemTypeId");
            }

            foreach (var row in systemCubes)
            {
                row.Cube = RequireTarget(
                    cubesById,
                    row.CubeId,
                    "SystemCube",
                    row.Id,
                    "CubeId");
            }

            foreach (var row in systemCubes)
            {
                row.System = RequireTarget(
                    systemsById,
                    row.SystemId,
                    "SystemCube",
                    row.Id,
                    "SystemId");
            }

            foreach (var row in systemDimensions)
            {
                row.Dimension = RequireTarget(
                    dimensionsById,
                    row.DimensionId,
                    "SystemDimension",
                    row.Id,
                    "DimensionId");
            }

            foreach (var row in systemDimensions)
            {
                row.System = RequireTarget(
                    systemsById,
                    row.SystemId,
                    "SystemDimension",
                    row.Id,
                    "SystemId");
            }

            foreach (var row in systemFacts)
            {
                row.Fact = RequireTarget(
                    factsById,
                    row.FactId,
                    "SystemFact",
                    row.Id,
                    "FactId");
            }

            foreach (var row in systemFacts)
            {
                row.System = RequireTarget(
                    systemsById,
                    row.SystemId,
                    "SystemFact",
                    row.Id,
                    "SystemId");
            }

            return new EnterpriseBIPlatformInstance(
                new ReadOnlyCollection<Cube>(cubes),
                new ReadOnlyCollection<Dimension>(dimensions),
                new ReadOnlyCollection<Fact>(facts),
                new ReadOnlyCollection<Measure>(measures),
                new ReadOnlyCollection<System>(systems),
                new ReadOnlyCollection<SystemCube>(systemCubes),
                new ReadOnlyCollection<SystemDimension>(systemDimensions),
                new ReadOnlyCollection<SystemFact>(systemFacts),
                new ReadOnlyCollection<SystemType>(systemTypes)
            );
        }

        private static T RequireTarget<T>(
            Dictionary<string, T> rowsById,
            string targetId,
            string sourceEntityName,
            string sourceId,
            string relationshipName)
            where T : class
        {
            if (string.IsNullOrEmpty(targetId))
            {
                throw new global::System.InvalidOperationException(
                    $"Relationship '{sourceEntityName}.{relationshipName}' on row '{sourceEntityName}:{sourceId}' is empty.");
            }

            if (!rowsById.TryGetValue(targetId, out var target))
            {
                throw new global::System.InvalidOperationException(
                    $"Relationship '{sourceEntityName}.{relationshipName}' on row '{sourceEntityName}:{sourceId}' points to missing Id '{targetId}'.");
            }

            return target;
        }
    }
}
