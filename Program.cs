using System;
using System.IO;

namespace Metadata.Framework.ConsoleHarness
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var modelPath = args.Length > 0 ? args[0] : Path.Combine("Samples", "SampleModel.xml");
            Console.WriteLine($"Metadata.Framework console harness is legacy-only. Model path: {modelPath}");
        }
    }
}
