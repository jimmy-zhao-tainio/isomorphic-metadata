using System.Collections.Generic;
using System.Linq;

namespace GeneratedModel
{
    public static class SampleModelExtensions
    {
        public static IEnumerable<System> SystemsWithCube(this Model model, string cubeName)
        {
            if (model == null || string.IsNullOrWhiteSpace(cubeName))
            {
                return Enumerable.Empty<System>();
            }

            return model.SystemCube
                        .Where(link => link.Cube != null && string.Equals(link.Cube.CubeName, cubeName, System.StringComparison.OrdinalIgnoreCase))
                        .Select(link => link.System)
                        .Where(system => system != null)
                        .Distinct();
        }
    }
}
