using System;
using System.IO;
using Metadata.Framework.Generic;

namespace Metadata.Framework.ConsoleHarness
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] : Path.Combine("Samples", "SampleModel.xml");
            if (!File.Exists(path))
            {
                Console.WriteLine($"Metadata file not found: {path}");
                return;
            }

            var reader = new Reader();
            var result = reader.Read(path);
            var model = result.Model;

            if (result.Errors.Count > 0)
            {
                Console.WriteLine("Errors detected while reading metadata:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Model: {model.Name}");
            foreach (var entity in model.Entities)
            {
                Console.WriteLine($"  Entity: {entity.Name}");
                foreach (var property in entity.Properties)
                {
                    var nullableFlag = property.IsNullable ? "?" : string.Empty;
                    Console.WriteLine($"    Property: {property.Name} ({property.DataType}{nullableFlag})");
                }

                if (entity.Relationship.Count == 0)
                {
                    continue;
                }

                Console.WriteLine("    Relationships:");
                foreach (var relationship in entity.Relationship)
                {
                    Console.WriteLine($"      -> {relationship.Name}");
                }
            }
        }
    }
}
