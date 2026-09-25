using System.Text.Json;
using Elsa.Workflows.Runtime;
using Grpc.Core;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Grpc;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    /// <summary>
    /// gRPC counterpart of the "POST /riot/trigger/{id}" HTTP endpoint. Provides gRPC as an
    /// optional alternative transport for starting/resuming workflows associated with a RIoT event.
    /// </summary>
    public class RIoTTriggerGrpcService : RIoTTriggerService.RIoTTriggerServiceBase
    {
        private readonly IStimulusSender _stimulusSender;

        public RIoTTriggerGrpcService(IStimulusSender stimulusSender)
        {
            _stimulusSender = stimulusSender;
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

            await RIoTEndpoints.TriggerAsync(request.Id, data, _stimulusSender);

            return new TriggerResponse { Success = true };
        }
    }
}
