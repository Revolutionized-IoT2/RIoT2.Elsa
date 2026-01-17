using RIoT2.Elsa.Server.RIoT.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTConfigurationService
    {
        string Id { get; }
        string? OrchestratorBaseUrl { get; set; }
        string MQTT_Password { get;  }
        string MQTT_Username { get; }
        string MQTT_ServerUrl { get; }
    }
}
