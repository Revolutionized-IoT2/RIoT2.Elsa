using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    public class RIoTConfigurationService : IRIoTConfigurationService
    {
        public string Id { get; private set; }
        public string? OrchestratorBaseUrl { get; set; }
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl { get; private set; }
        public string MQTT_Password { get; private set; }
        public string MQTT_Username { get; private set; }
        public string MQTT_ServerUrl { get; private set; }

        public RIoTConfigurationService() 
        {
            Id = Environment.GetEnvironmentVariable("RIOT2_WORKFLOW_ID") ?? "";
            MQTT_Password = Environment.GetEnvironmentVariable("RIOT2_MQTT_PASSWORD") ?? "";
            MQTT_ServerUrl = Environment.GetEnvironmentVariable("RIOT2_MQTT_IP") ?? "";
            MQTT_Username = Environment.GetEnvironmentVariable("RIOT2_MQTT_USERNAME") ?? "";
            WorkflowBaseUrl = Environment.GetEnvironmentVariable("RIOT2_WORKFLOW_URL") ?? "";
            WorkflowGrpcBaseUrl = Environment.GetEnvironmentVariable("RIOT2_WORKFLOW_GRPC_URL");
            if (string.IsNullOrWhiteSpace(WorkflowGrpcBaseUrl))
                throw new InvalidOperationException("RIOT2_WORKFLOW_GRPC_URL must contain the externally reachable gRPC URL.");
            if (!Uri.TryCreate(WorkflowGrpcBaseUrl, UriKind.Absolute, out var grpcUrl) ||
                (grpcUrl.Scheme != Uri.UriSchemeHttp && grpcUrl.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("RIOT2_WORKFLOW_GRPC_URL must be an absolute HTTP or HTTPS URL.");
        }
    }
}
