using System.Collections.Generic;

namespace Metadata.Framework.Generic
{
    public class ModelInstance
    {
        public Model Model { get; set; } = new Model();
        public List<EntityInstance> Entities { get; } = new List<EntityInstance>();
    }

    public class EntityInstance
    {
        public Entity Entity { get; set; } = new Entity();
        public List<RecordInstance> Records { get; } = new List<RecordInstance>();
    }

    public class RecordInstance
    {
        public string Id { get; set; } = "";
        public List<PropertyValue> Properties { get; } = new List<PropertyValue>();
        public List<RelationshipValue> Relationships { get; } = new List<RelationshipValue>();
    }

    public class PropertyValue
    {
        public Property Property { get; set; } = new Property();
        public string Value { get; set; } = "";
    }

    public class RelationshipValue
    {
        public Entity Entity { get; set; } = new Entity();
        public string Value { get; set; } = "";
    }
}
