using Microsoft.Extensions.Options;
using RIoT2.Core.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTConfigurationService : IRIoTConfigurationService
    {
        public string Id { get; }
        public string? OrchestratorBaseUrl { get; set; }
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl { get; }
        public MqttConfiguration Mqtt { get; }

        public RIoTConfigurationService(IOptions<WorkflowOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var settings = options.Value;
            Id = settings.Id;
            WorkflowBaseUrl = settings.Url;
            WorkflowGrpcBaseUrl = settings.GrpcUrl;
            Mqtt = settings.Mqtt.ToConfiguration(settings.Id);
        }
    }
}
