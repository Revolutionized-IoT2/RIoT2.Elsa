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
            Id = Require("RIOT2_WORKFLOW_ID");
            // Username/password stay optional so brokers that allow anonymous clients keep working.
            MQTT_Password = Environment.GetEnvironmentVariable("RIOT2_MQTT_PASSWORD") ?? "";
            MQTT_ServerUrl = Require("RIOT2_MQTT_IP");
            MQTT_Username = Environment.GetEnvironmentVariable("RIOT2_MQTT_USERNAME") ?? "";
            WorkflowBaseUrl = RequireAbsoluteHttpUrl("RIOT2_WORKFLOW_URL");
            WorkflowGrpcBaseUrl = Environment.GetEnvironmentVariable("RIOT2_WORKFLOW_GRPC_URL");
            if (string.IsNullOrWhiteSpace(WorkflowGrpcBaseUrl))
                throw new InvalidOperationException("RIOT2_WORKFLOW_GRPC_URL must contain the externally reachable gRPC URL.");
            if (!Uri.TryCreate(WorkflowGrpcBaseUrl, UriKind.Absolute, out var grpcUrl) ||
                (grpcUrl.Scheme != Uri.UriSchemeHttp && grpcUrl.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("RIOT2_WORKFLOW_GRPC_URL must be an absolute HTTP or HTTPS URL.");
        }

        private static string Require(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{name} must be configured.");
            return value;
        }

        private static string RequireAbsoluteHttpUrl(string name)
        {
            var value = Require(name);
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException($"{name} must be an absolute HTTP or HTTPS URL.");
            return value;
        }
    }
}
