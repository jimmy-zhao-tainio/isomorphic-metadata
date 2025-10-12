using System.Collections.Generic;

namespace Metadata.Framework.Generic
{
    public class ReadResult
    {
        public Model Model { get; set; } = new Model();
        public List<string> Errors { get; } = new List<string>();
    }
}
