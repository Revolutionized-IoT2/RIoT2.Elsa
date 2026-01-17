namespace RIoT2.Elsa.Server.RIoT.Models
{
    public class ReportTemplate
    {
        public string? NodeId { get; set; }
        public string? Node { get; set; }
        public string? DeviceId { get; set; }
        public string? Device { get; set; }
        public List<string>? FilterOptions { get; set; }
        public string? Name { get; set; }
        public string? Id{ get; set; }
        public ValueType? Type { get; set; }
        public string? Address { get; set; }
        public string? RefreshSchedule { get; set; }
        public bool MaintainHistory { get; set; }
    }
}
