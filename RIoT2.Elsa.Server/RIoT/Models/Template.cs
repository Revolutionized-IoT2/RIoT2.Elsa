using RIoT2.Core.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Models
{
    public class Template : ITemplate, INodeTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public object? Model { get; set; } = null;
        public Core.ValueType Type { get; set; } = Core.ValueType.Entity;
        public string NodeId { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
    }
}
