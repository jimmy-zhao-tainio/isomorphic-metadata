using System;
using System.Collections.Generic;

namespace GeneratedModel
{
    public class Model
    {
        public Model()
        {
            Cube = new List<Cube>();
            Dimension = new List<Dimension>();
            Fact = new List<Fact>();
            Measure = new List<Measure>();
            System = new List<System>();
            SystemCube = new List<SystemCube>();
            SystemDimension = new List<SystemDimension>();
            SystemFact = new List<SystemFact>();
            SystemType = new List<SystemType>();
        }

        public string Name { get; set; }
        public List<Cube> Cube { get; set; }
        public List<Dimension> Dimension { get; set; }
        public List<Fact> Fact { get; set; }
        public List<Measure> Measure { get; set; }
        public List<System> System { get; set; }
        public List<SystemCube> SystemCube { get; set; }
        public List<SystemDimension> SystemDimension { get; set; }
        public List<SystemFact> SystemFact { get; set; }
        public List<SystemType> SystemType { get; set; }
    }

    public class Cube
    {
        public string Id { get; set; }
        public string CubeName { get; set; }
        public string Purpose { get; set; }
        public string RefreshMode { get; set; }
    }

    public class Dimension
    {
        public string Id { get; set; }
        public string DimensionName { get; set; }
        public bool IsConformed { get; set; }
        public string HierarchyCount { get; set; }
    }

    public class Fact
    {
        public string Id { get; set; }
        public string FactName { get; set; }
        public string Grain { get; set; }
        public string MeasureCount { get; set; }
        public string BusinessArea { get; set; }
    }

    public class Measure
    {
        public string Id { get; set; }
        public string MeasureName { get; set; }
        public string MDX { get; set; }
        public string CubeId { get; set; }
        public Cube Cube { get; set; }
    }

    public class System
    {
        public string Id { get; set; }
        public string SystemName { get; set; }
        public string Version { get; set; }
        public string DeploymentDate { get; set; }
        public string SystemTypeId { get; set; }
        public SystemType SystemType { get; set; }
    }

    public class SystemCube
    {
        public string Id { get; set; }
        public string ProcessingMode { get; set; }
        public string SystemId { get; set; }
        public string CubeId { get; set; }
        public Cube Cube { get; set; }
        public System System { get; set; }
    }

    public class SystemDimension
    {
        public string Id { get; set; }
        public string ConformanceLevel { get; set; }
        public string SystemId { get; set; }
        public string DimensionId { get; set; }
        public Dimension Dimension { get; set; }
        public System System { get; set; }
    }

    public class SystemFact
    {
        public string Id { get; set; }
        public string LoadPattern { get; set; }
        public string SystemId { get; set; }
        public string FactId { get; set; }
        public Fact Fact { get; set; }
        public System System { get; set; }
    }

    public class SystemType
    {
        public string Id { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
    }

}
