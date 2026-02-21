using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metadata.Framework.Generic
{
    public static class ReflectionModelMaterializer
    {
        public static TModel Materialize<TModel>(ModelInstance instance, string entityNamespace = null)
            where TModel : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var model = Materialize(instance, typeof(TModel), entityNamespace);
            return (TModel)model;
        }

        public static object Materialize(ModelInstance instance, Type modelType, string entityNamespace = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            var model = Activator.CreateInstance(modelType);
            AssignModelName(instance, model, modelType);

            if (string.IsNullOrEmpty(entityNamespace))
            {
                entityNamespace = modelType.Namespace ?? string.Empty;
            }

            var collectionContexts = BuildCollectionContexts(model, modelType);
            var entityStore = InitializeEntityStore(collectionContexts);

            PopulateEntities(instance, collectionContexts, entityStore, entityNamespace);
            ResolveRelationships(instance, collectionContexts, entityStore);

            return model;
        }

        private static void AssignModelName(ModelInstance instance, object model, Type modelType)
        {
            var nameProperty = modelType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            if (nameProperty != null && nameProperty.CanWrite)
            {
                nameProperty.SetValue(model, instance.Model?.Name ?? string.Empty);
            }
        }

        private static Dictionary<string, EntityCollectionContext> BuildCollectionContexts(object model, Type modelType)
        {
            var contexts = new Dictionary<string, EntityCollectionContext>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsCollectionProperty(property, out var itemType))
                {
                    continue;
                }

                var collection = property.GetValue(model) as IList;
                if (collection == null && property.CanWrite)
                {
                    collection = (IList)Activator.CreateInstance(property.PropertyType);
                    property.SetValue(model, collection);
                }

                if (collection == null)
                {
                    collection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                    property.SetValue(model, collection);
                }

                var context = new EntityCollectionContext
                {
                    EntityName = itemType.Name,
                    CollectionProperty = property,
                    Collection = collection,
                    ItemType = itemType,
                    PropertyLookup = itemType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase)
                };

                contexts[context.EntityName] = context;
            }

            return contexts;
        }

        private static Dictionary<string, Dictionary<string, object>> InitializeEntityStore(
            Dictionary<string, EntityCollectionContext> contexts)
        {
            return contexts.Keys.ToDictionary(
                key => key,
                key => new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void PopulateEntities(
            ModelInstance instance,
            Dictionary<string, EntityCollectionContext> contexts,
            Dictionary<string, Dictionary<string, object>> entityStore,
            string entityNamespace)
        {
            foreach (var entityInstance in instance.Entities)
            {
                var entityName = entityInstance.Entity?.Name;
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    continue;
                }

                if (!contexts.TryGetValue(entityName, out var context))
                {
                    continue;
                }

                foreach (var record in entityInstance.Records)
                {
                    var entityObject = Activator.CreateInstance(context.ItemType);
                    SetScalarProperties(entityObject, context, record);
                    context.Collection.Add(entityObject);

                    if (!string.IsNullOrWhiteSpace(record.Id))
                    {
                        entityStore[entityName][record.Id] = entityObject;
                    }
                }
            }
        }

        private static void SetScalarProperties(object entityObject, EntityCollectionContext context, RecordInstance record)
        {
            if (context.PropertyLookup.TryGetValue("Id", out var idProperty) && idProperty.CanWrite)
            {
                idProperty.SetValue(entityObject, record.Id);
            }

            foreach (var propertyValue in record.Properties)
            {
                if (propertyValue.Property == null || string.IsNullOrWhiteSpace(propertyValue.Property.Name))
                {
                    continue;
                }

                if (!context.PropertyLookup.TryGetValue(propertyValue.Property.Name, out var propertyInfo))
                {
                    continue;
                }

                if (!propertyInfo.CanWrite)
                {
                    continue;
                }

                var convertedValue = ConvertValue(propertyInfo.PropertyType, propertyValue.Value);
                propertyInfo.SetValue(entityObject, convertedValue);
            }
        }

        private static void ResolveRelationships(
            ModelInstance instance,
            Dictionary<string, EntityCollectionContext> contexts,
            Dictionary<string, Dictionary<string, object>> entityStore)
        {
            foreach (var entityInstance in instance.Entities)
            {
                var entityName = entityInstance.Entity?.Name;
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    continue;
                }

                if (!contexts.TryGetValue(entityName, out var context))
                {
                    continue;
                }

                foreach (var record in entityInstance.Records)
                {
                    if (!entityStore[entityName].TryGetValue(record.Id, out var currentObject))
                    {
                        continue;
                    }

                    var current = currentObject;

                    foreach (var relationship in record.Relationships)
                    {
                        var relatedName = relationship.Entity?.Name;
                        if (string.IsNullOrWhiteSpace(relatedName))
                        {
                            continue;
                        }

                        if (!entityStore.TryGetValue(relatedName, out var relatedStore))
                        {
                            continue;
                        }

                        if (!relatedStore.TryGetValue(relationship.Value, out var related))
                        {
                            continue;
                        }

                        var referenceProperty = FindReferenceProperty(context.PropertyLookup, relatedName);
                        if (referenceProperty == null || !referenceProperty.CanWrite)
                        {
                            continue;
                        }

                        referenceProperty.SetValue(current, related);
                    }
                }
            }
        }

        private static PropertyInfo FindReferenceProperty(
            Dictionary<string, PropertyInfo> propertyLookup,
            string relatedEntityName)
        {
            // Prefer direct name match first.
            if (propertyLookup.TryGetValue(relatedEntityName, out var property))
            {
                return property;
            }

            // Fall back to matching by property type name.
            return propertyLookup.Values.FirstOrDefault(p =>
                !p.PropertyType.IsValueType &&
                p.PropertyType != typeof(string) &&
                string.Equals(p.PropertyType.Name, relatedEntityName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCollectionProperty(PropertyInfo property, out Type itemType)
        {
            itemType = null;

            if (!property.PropertyType.IsGenericType)
            {
                return false;
            }

            var genericDefinition = property.PropertyType.GetGenericTypeDefinition();
            if (genericDefinition != typeof(List<>))
            {
                return false;
            }

            itemType = property.PropertyType.GetGenericArguments()[0];
            return typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
        }

        private static object ConvertValue(Type propertyType, string value)
        {
            if (propertyType == typeof(string))
            {
                return value ?? string.Empty;
            }

            return value;
        }

        private class EntityCollectionContext
        {
            public string EntityName { get; set; }
            public PropertyInfo CollectionProperty { get; set; }
            public IList Collection { get; set; }
            public Type ItemType { get; set; }
            public Dictionary<string, PropertyInfo> PropertyLookup { get; set; }

            public EntityCollectionContext()
            {
                EntityName = string.Empty;
                PropertyLookup = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
