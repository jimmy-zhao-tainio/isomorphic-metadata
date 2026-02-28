using System;
using System.Linq;
using Model = EnterpriseBIPlatform.EnterpriseBIPlatform;

namespace Samples.ConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Model: EnterpriseBIPlatform");
            Console.WriteLine();
            Console.WriteLine("Systems:");
            foreach (var system in Model.Systems)
            {
                Console.WriteLine($"  {system.SystemName} [{system.SystemType.TypeName}]");

                foreach (var link in Model.SystemCubes.Where(x => x.SystemId == system.Id))
                {
                    var mode = string.IsNullOrEmpty(link.ProcessingMode) ? "n/a" : link.ProcessingMode;
                    Console.WriteLine($"    Cube: {link.Cube.CubeName} (mode: {mode})");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Measures:");
            foreach (var measure in Model.Measures)
            {
                Console.WriteLine($"  Measure Id={measure.Id}, Name={measure.MeasureName}, Cube={measure.Cube.CubeName}");
            }

            Console.WriteLine();
            Console.WriteLine("Lookup example:");
            try
            {
                var measure1 = Model.Measures.First(item => string.Equals(item.Id, "1", StringComparison.Ordinal));
                Console.WriteLine($"  Measure 1 cube = {measure1.Cube.CubeName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Lookup failed: {ex.Message}");
            }
        }
    }
}
