using System.Text.Json;
using Grpc.Core;
using RIoT2.Core.Utils;
using RIoT2.Elsa.Server.RIoT.Grpc;
using RIoT2.Elsa.Workflows.Services;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    /// <summary>
    /// gRPC counterpart of the "POST /riot/trigger/{id}" HTTP endpoint, and the transport the
    /// orchestrator's outbox uses in the remote workflow mode. A request with a <c>message_id</c> seen in
    /// the last ten minutes is answered with success and not run again (<see cref="TriggerDeduplicator"/>).
    /// </summary>
    public class RIoTTriggerGrpcService : RIoTTriggerService.RIoTTriggerServiceBase
    {
        private readonly RIoTTriggers _triggers;
        private readonly ILogger<RIoTTriggerGrpcService> _logger;

        public RIoTTriggerGrpcService(RIoTTriggers triggers, ILogger<RIoTTriggerGrpcService> logger)
        {
            _triggers = triggers;
            _logger = logger;
        }

        public override async Task<TriggerResponse> Trigger(TriggerRequest request, ServerCallContext context)
        {
            object? data = null;

            if (!string.IsNullOrWhiteSpace(request.Data))
            {
                try
                {
                    data = JsonSerializer.Deserialize<JsonElement>(request.Data);
                }
                catch (JsonException error)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Trigger data must be valid JSON."), error.Message);
                }
            }

            // The orchestrator caps the deadline at the trigger's time-to-live; a request that waited
            // past it must not start a workflow.
            if (context != null && context.Deadline <= DateTime.UtcNow)
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "The trigger arrived after its deadline."));

            // Everything logged while the workflow starts, Elsa's own lines included, carries the id.
            using var scope = _logger.BeginIdScope(LogScope.MessageId, request.MessageId);
            // The call's cancellation is not passed on: a run whose caller timed out still completes and is remembered.
            var ran = await _triggers.TriggerOnceAsync(request.MessageId, request.Id, data, "grpc");
            if (!ran)
                _logger.LogInformation("Trigger {MessageId} for {Id} was already run; not run again", request.MessageId, request.Id);

            return new TriggerResponse { Success = true };
        }
    }
}
