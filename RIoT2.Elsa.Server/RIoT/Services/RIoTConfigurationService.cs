using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTConfigurationService : IRIoTConfigurationService
    {
        public string Id { get; private set; }
        public string? OrchestratorBaseUrl { get; set; }
        public string MQTT_Password { get; private set; }
        public string MQTT_Username { get; private set; }
        public string MQTT_ServerUrl { get; private set; }

        public RIoTConfigurationService() 
        {
            Id = Environment.GetEnvironmentVariable("RIOT2_ORCHESTRATOR_ID") ?? "";
            MQTT_Password = Environment.GetEnvironmentVariable("RIOT2_MQTT_PASSWORD") ?? "";    
            MQTT_Username = Environment.GetEnvironmentVariable("RIOT2_MQTT_IP") ?? "";
            MQTT_ServerUrl = Environment.GetEnvironmentVariable("RIOT2_MQTT_USERNAME") ?? "";
        }
    }
}
