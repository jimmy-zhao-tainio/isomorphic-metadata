using System.Collections.Generic;

namespace Metadata.Framework.Generic
{
    public class InstanceReadResult
    {
        public ModelInstance ModelInstance { get; } = new ModelInstance();
        public List<string> Errors { get; } = new List<string>();
    }
}
