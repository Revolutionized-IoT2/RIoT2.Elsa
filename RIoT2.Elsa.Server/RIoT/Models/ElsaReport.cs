namespace RIoT2.Elsa.Server.RIoT.Models
{
    //remove this once report uses object instead of valuemodel
    public class ElsaReport
    {
        public string Id { get; set; } = string.Empty!;

        public long TimeStamp { get; set; }

        public string Filter { get; set; } = string.Empty;

        public object Value { get; set; } = null!;
    }
}
