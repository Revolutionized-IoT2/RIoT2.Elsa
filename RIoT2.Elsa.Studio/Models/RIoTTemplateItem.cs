namespace RIoT2.Elsa.Studio.Models
{
    public class RIoTTemplateItem
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Core.ValueType Type { get; set; }
        public string? Node { get; set; }
        public string? Device { get; set; }
        public string? Value { get; set; }
        public object? Model { get; set; }
    }
}