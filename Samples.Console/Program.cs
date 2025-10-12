using System;
using System.Collections;
using System.IO;
using System.Linq;
using Metadata.Framework.Generic;

namespace Samples.ConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var modelPath = ResolveModelPath(args);
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Sample model not found at '{modelPath}'.");
                return;
            }

            var reader = new Reader();
            var modelResult = reader.Read(modelPath);
            if (modelResult.Errors.Count > 0)
            {
                Console.WriteLine("Errors detected while reading sample model metadata:");
                foreach (var error in modelResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }

            var instancePath = ResolveInstancePath(args);
            if (!File.Exists(instancePath))
            {
                Console.WriteLine($"Sample instance not found at '{instancePath}'.");
                return;
            }

            var instanceReader = new InstanceReader();
            var instanceResult = instanceReader.Read(instancePath, modelResult.Model);
            if (instanceResult.Errors.Count > 0)
            {
                Console.WriteLine("Errors detected while reading sample instance:");
                foreach (var error in instanceResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }

            var typedModel = ReflectionModelMaterializer.Materialize<GeneratedModel.Model>(instanceResult.ModelInstance);

            Console.WriteLine($"Model: {typedModel.Name}");
            DisplayCollections(typedModel);
        }

        private static string ResolveModelPath(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                return Path.GetFullPath(args[0]);
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "Samples", "SampleModel.xml");
            var candidate = Path.GetFullPath(relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            return Path.Combine(baseDirectory, "Samples", "SampleModel.xml");
        }

        private static string ResolveInstancePath(string[] args)
        {
            if (args != null && args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                return Path.GetFullPath(args[1]);
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "Samples", "SampleInstance.xml");
            var candidate = Path.GetFullPath(relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            return Path.Combine(baseDirectory, "Samples", "SampleInstance.xml");
        }

        private static void DisplayCollections(GeneratedModel.Model model)
        {
            var modelType = model.GetType();
            foreach (var property in modelType.GetProperties().OrderBy(p => p.Name))
            {
                if (property.PropertyType == typeof(string))
                {
                    continue;
                }

                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                var collection = property.GetValue(model) as IEnumerable;
                if (collection == null)
                {
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"{property.Name}:");
                foreach (var item in collection)
                {
                    DisplayEntity(item);
                }
            }
        }

        private static void DisplayEntity(object entity)
        {
            if (entity == null)
            {
                Console.WriteLine("  (null entity)");
                return;
            }

            var entityType = entity.GetType();
            Console.WriteLine($"  {entityType.Name}:");

            foreach (var property in entityType.GetProperties())
            {
                if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
                {
                    var value = property.GetValue(entity);
                    Console.WriteLine($"    {property.Name}: {value ?? "(null)"}");
                    continue;
                }

                var related = property.GetValue(entity);
                if (related == null)
                {
                    Console.WriteLine($"    {property.Name}: (null)");
                    continue;
                }

                var relatedId = GetIdValue(related);
                Console.WriteLine($"    {property.Name}: {related.GetType().Name} ({relatedId})");
            }
        }

        private static string GetIdValue(object entity)
        {
            if (entity == null)
            {
                return "(null)";
            }

            var idProperty = entity.GetType().GetProperty("Id");
            var value = idProperty?.GetValue(entity);
            return value?.ToString() ?? "(no id)";
        }
    }
}
