using System;
using System.IO;
using Metadata.Framework.Generic;
using Metadata.Framework.Transformations;

namespace Metadata.Framework.Transformations.ConsoleHarness
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var sourcePath = ResolveModelPath(args);
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"Metadata model not found at '{sourcePath}'.");
                return;
            }

            var reader = new Reader();
            var result = reader.Read(sourcePath);
            if (result.Errors.Count > 0)
            {
                Console.WriteLine("Errors detected while reading metadata:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }

            var converter = new ModelToCSharpConverter();
            var generatedCode = converter.Generate(result.Model);

            var outputPath = ResolveModelOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, generatedCode);

            Console.WriteLine("Generated C# classes written to:");
            Console.WriteLine($"  {outputPath}");

            var schemaGenerator = new SqlServerSchemaGenerator();
            var schemaScript = schemaGenerator.Generate(result.Model);
            var schemaPath = ResolveSchemaOutputPath();
            File.WriteAllText(schemaPath, schemaScript);

            Console.WriteLine("Generated SQL Server schema written to:");
            Console.WriteLine($"  {schemaPath}");

            var instancePath = ResolveInstancePath(args);
            if (File.Exists(instancePath))
            {
                var instanceReader = new InstanceReader();
                var instanceResult = instanceReader.Read(instancePath, result.Model);
                if (instanceResult.Errors.Count == 0)
                {
                    var dataGenerator = new SqlServerDataGenerator();
                    var dataScript = dataGenerator.Generate(result.Model, instanceResult.ModelInstance);
                    var dataPath = ResolveDataOutputPath();
                    File.WriteAllText(dataPath, dataScript);

                    Console.WriteLine("Generated SQL Server data script written to:");
                    Console.WriteLine($"  {dataPath}");
                }
                else
                {
                    Console.WriteLine("Skipping data script generation due to instance read errors:");
                    foreach (var error in instanceResult.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                }
            }
            else
            {
                Console.WriteLine("No sample instance found; skipping data script generation.");
            }
        }

        private static string ResolveModelPath(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                return Path.GetFullPath(args[0]);
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", "SampleModel.xml");
            var candidate = Path.GetFullPath(relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            return Path.Combine(baseDirectory, "Samples", "SampleModel.xml");
        }

        private static string ResolveModelOutputPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", "SampleModel.cs");
            return Path.GetFullPath(relativePath);
        }

        private static string ResolveSchemaOutputPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", "SampleModel.sql");
            return Path.GetFullPath(relativePath);
        }

        private static string ResolveDataOutputPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", "SampleInstance.sql");
            return Path.GetFullPath(relativePath);
        }

        private static string ResolveInstancePath(string[] args)
        {
            if (args != null && args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                return Path.GetFullPath(args[1]);
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", "SampleInstance.xml");
            var candidate = Path.GetFullPath(relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            return Path.Combine(baseDirectory, "Samples", "SampleInstance.xml");
        }
    }
}

