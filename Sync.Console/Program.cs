using System;
using System.Collections.Generic;
using System.IO;
using Metadata.Framework.Generic;
using Metadata.Framework.Transformations;

namespace Metadata.Framework.SyncConsole
{
    internal static class Program
    {
        private const string DefaultConnectionString = "Server=localhost;Database=EnterpriseBIPlatform;Trusted_Connection=True;TrustServerCertificate=True;";
        private const string DefaultSchema = "dbo";

        private static void Main(string[] args)
        {
            var connectionString = args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : DefaultConnectionString;

            var schema = args != null && args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : DefaultSchema;

            Console.WriteLine($"Reading database schema from {connectionString} (schema: {schema})...");

            var reader = new Reader();
            var readResult = reader.ReadFromDatabase(connectionString, schema);
            if (readResult.Errors.Count > 0)
            {
                Console.WriteLine("Errors detected while reading database schema:");
                foreach (var error in readResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }

            Console.WriteLine("Schema read successfully.");

            Console.WriteLine("Reading table data...");
            var instanceReader = new DatabaseInstanceReader();
            var modelInstance = instanceReader.Read(connectionString, readResult.Model, schema);
            Console.WriteLine("Data read successfully.");

            var samplesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Samples");
            var sampleModelPath = Path.GetFullPath(Path.Combine(samplesDirectory, "SampleModel.xml"));
            var sampleInstancePath = Path.GetFullPath(Path.Combine(samplesDirectory, "SampleInstance.xml"));

            Model existingModel = null;
            if (File.Exists(sampleModelPath))
            {
                Console.WriteLine("Comparing with existing XML model...");
                using (var stream = File.OpenRead(sampleModelPath))
                {
                    var existingResult = reader.Read(stream);
                    if (existingResult.Errors.Count == 0)
                    {
                        existingModel = existingResult.Model;
                        var comparer = new ModelComparer();
                        var comparison = comparer.Compare(existingModel, readResult.Model);
                        if (comparison.HasDifferences)
                        {
                            Console.WriteLine("Differences detected between existing XML model and database schema:");
                            PrintComparison(comparison);
                        }
                        else
                        {
                            Console.WriteLine("Existing XML model matches database schema.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to compare with existing XML model due to errors:");
                        foreach (var error in existingResult.Errors)
                        {
                            Console.WriteLine($"  - {error}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No existing XML model found for comparison.");
            }

            Console.WriteLine($"Writing XML model to {sampleModelPath}...");
            XmlModelWriter.Write(readResult.Model, sampleModelPath);

            Console.WriteLine($"Writing XML instance to {sampleInstancePath}...");
            XmlInstanceWriter.Write(modelInstance, sampleInstancePath);

            Console.WriteLine("Regenerating code and scripts...");
            var converter = new ModelToCSharpConverter();
            var generatedCode = converter.Generate(readResult.Model);
            File.WriteAllText(Path.Combine(samplesDirectory, "SampleModel.cs"), generatedCode);

            var schemaGenerator = new SqlServerSchemaGenerator();
            File.WriteAllText(Path.Combine(samplesDirectory, "SampleModel.sql"), schemaGenerator.Generate(readResult.Model));

            var dataGenerator = new SqlServerDataGenerator();
            File.WriteAllText(Path.Combine(samplesDirectory, "SampleInstance.sql"), dataGenerator.Generate(readResult.Model, modelInstance));

            Console.WriteLine("Sync complete.");
        }

        private static void PrintComparison(ModelComparisonResult comparison)
        {
            void PrintList(string heading, List<string> items)
            {
                if (items.Count == 0)
                {
                    return;
                }

                Console.WriteLine($"  {heading}:");
                foreach (var item in items)
                {
                    Console.WriteLine($"    - {item}");
                }
            }

            PrintList("Added entities", comparison.AddedEntities);
            PrintList("Removed entities", comparison.RemovedEntities);
            PrintList("Added properties", comparison.AddedProperties);
            PrintList("Removed properties", comparison.RemovedProperties);
            PrintList("Changed properties", comparison.ChangedProperties);
            PrintList("Added relationships", comparison.AddedRelationships);
            PrintList("Removed relationships", comparison.RemovedRelationships);
        }
    }
}


