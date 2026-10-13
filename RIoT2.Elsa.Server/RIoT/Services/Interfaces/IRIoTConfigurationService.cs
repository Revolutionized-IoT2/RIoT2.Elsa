using RIoT2.Core.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IRIoTConfigurationService
    {
        string Id { get; }
        string? OrchestratorBaseUrl { get; set; }
        MqttConfiguration Mqtt { get; }
        public string? WorkflowBaseUrl { get; set; }
        string? WorkflowGrpcBaseUrl { get; }
    }
}
