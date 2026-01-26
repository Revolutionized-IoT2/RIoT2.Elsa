namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTConfigurationService
    {
        string Id { get; }
        string? OrchestratorBaseUrl { get; set; }
        string MQTT_Password { get;  }
        string MQTT_Username { get; }
        string MQTT_ServerUrl { get; }
        public string? WorkflowBaseUrl { get; set; }
    }
}
