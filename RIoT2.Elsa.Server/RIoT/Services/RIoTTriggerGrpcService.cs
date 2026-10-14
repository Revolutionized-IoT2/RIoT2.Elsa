using System.Text.Json;
using Elsa.Workflows.Runtime;
using Grpc.Core;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Grpc;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    /// <summary>
    /// gRPC counterpart of the "POST /riot/trigger/{id}" HTTP endpoint, and the transport the
    /// orchestrator's outbox uses. A request with a <c>message_id</c> seen in the last ten minutes is
    /// answered with success and not run again (<see cref="TriggerDeduplicator"/>).
    /// </summary>
    public class RIoTTriggerGrpcService : RIoTTriggerService.RIoTTriggerServiceBase
    {
        private readonly IStimulusSender _stimulusSender;
        private readonly TriggerDeduplicator _deduplicator;
        private readonly ILogger<RIoTTriggerGrpcService> _logger;

        public RIoTTriggerGrpcService(IStimulusSender stimulusSender, TriggerDeduplicator deduplicator, ILogger<RIoTTriggerGrpcService> logger)
        {
            _stimulusSender = stimulusSender;
            _deduplicator = deduplicator;
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

            var ran = await _deduplicator.RunOnceAsync(request.MessageId,
                () => RIoTEndpoints.TriggerAsync(request.Id, data, _stimulusSender));
            if (!ran)
                _logger.LogInformation("Trigger {MessageId} for {Id} was already run; not run again", request.MessageId, request.Id);

            return new TriggerResponse { Success = true };
        }
    }
}
