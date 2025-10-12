using System.Collections.Generic;

namespace Metadata.Framework.Generic
{
    public class Model
    {
        public string Name { get; set; } = "";
        public List<Entity> Entities { get; set; } = new List<Entity>();
    }

    public class Entity
    {
        public string Name { get; set; } = "";
        public List<Property> Properties { get; set; } = new List<Property>();
        public List<Entity> Relationship { get; set; } = new List<Entity>();
    }

    public class Property
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsNullable { get; set; }
    }
}
