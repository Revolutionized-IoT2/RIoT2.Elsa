using Microsoft.Extensions.Options;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTConfigurationService : IRIoTConfigurationService
    {
        public string Id { get; }
        public string? OrchestratorBaseUrl { get; set; }
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl { get; }
        public string? MQTT_Password { get; }
        public string? MQTT_Username { get; }
        public string MQTT_ServerUrl { get; }

        public RIoTConfigurationService(IOptions<WorkflowOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var settings = options.Value;
            Id = settings.Id;
            WorkflowBaseUrl = settings.Url;
            WorkflowGrpcBaseUrl = settings.GrpcUrl;
            MQTT_ServerUrl = settings.Mqtt.Host;
            // No user means an anonymous MQTT connection.
            MQTT_Username = settings.Mqtt.Username;
            MQTT_Password = settings.Mqtt.Password;
        }
    }
}
