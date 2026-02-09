using RIoT2.Core.Interfaces;
using RIoT2.Elsa.Studio.Models;

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

        public Studio.Models.RIoTTemplateItem Create(TemplateType type)
        {
            return new Studio.Models.RIoTTemplateItem
            {
                Id = this.Id,
                Name = this.Name,
                Model = this.Model,
                Type = this.Type,
                Node = this.Node,
                Device = this.Device,
                TemplateType = type
            };
        }
    }
}
