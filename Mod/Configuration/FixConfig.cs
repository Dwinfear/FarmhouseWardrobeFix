using System.Collections.Generic;

namespace FarmhouseWardrobeFix
{
    public sealed class FixConfig
    {
        public string RemovalKey { get; set; } = "F4";
        public List<RemovedObjectEntry> RemovedObjects { get; set; } = new List<RemovedObjectEntry>();
    }

    public sealed class RemovedObjectEntry
    {
        public string Scene { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
    }
}
