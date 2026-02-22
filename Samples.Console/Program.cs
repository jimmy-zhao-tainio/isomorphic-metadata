using System;
using System.IO;

namespace Samples.ConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var workspacePath = ResolveWorkspacePath(args);
            var modelPath = ResolveModelPath(workspacePath);
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Sample model not found at '{modelPath}'.");
                return;
            }

            var instancePath = ResolveInstancePath(workspacePath);
            if (!File.Exists(instancePath) && !Directory.Exists(Path.Combine(workspacePath, "metadata", "instance")))
            {
                Console.WriteLine($"Sample instance not found at '{instancePath}'.");
                return;
            }

            var model = GeneratedModel.EnterpriseBIPlatformModel.LoadFromXml(workspacePath);

            Console.WriteLine("Model: EnterpriseBIPlatform");
            Console.WriteLine();
            Console.WriteLine("Measures:");
            foreach (var measure in model.Measures)
            {
                Console.WriteLine($"  Measure Id={measure.Id}, Name={measure.Name}, Cube={measure.Cube?.Name ?? "(null)"}");
            }

            Console.WriteLine();
            Console.WriteLine("Lookup example:");
            try
            {
                var measure1 = model.Measures.GetId(1);
                Console.WriteLine($"  Measure 1 cube = {measure1.Cube?.Name ?? "(null)"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Lookup failed: {ex.Message}");
            }
        }

        private static string ResolveWorkspacePath(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                var argPath = Path.GetFullPath(args[0]);
                if (File.Exists(argPath))
                {
                    return Path.GetDirectoryName(argPath);
                }

                return argPath;
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var relativePath = Path.Combine(baseDirectory, "..", "..", "..", "Samples", "SampleModel.xml");
            var candidate = Path.GetFullPath(relativePath);
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }

            return Path.Combine(baseDirectory, "Samples");
        }

        private static string ResolveModelPath(string workspacePath)
        {
            return Path.Combine(workspacePath, "metadata", "model.xml");
        }

        private static string ResolveInstancePath(string workspacePath)
        {
            return Path.Combine(workspacePath, "metadata", "instance", "Measure.xml");
        }
    }
}
