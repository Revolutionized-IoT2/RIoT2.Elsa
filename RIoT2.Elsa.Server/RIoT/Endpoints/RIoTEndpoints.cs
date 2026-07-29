using Elsa.Workflows.Helpers;
using Elsa.Workflows.Runtime;
using Microsoft.AspNetCore.Mvc;
using RIoT2.Elsa.Server.RIoT.Activities;

namespace RIoT2.Elsa.Server.RIoT.Endpoints
{
    public static class RIoTEndpoints
    {
        public static IEndpointRouteBuilder MapRIoTEndpoints(
        this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/riot");

            // Get report by ID
            group.MapPost("/trigger/{id}", static async (string id, [FromBody] object data,
                IStimulusSender stimulusSender) =>
            {
                await TriggerAsync(id, data, stimulusSender);

                return Results.Ok(); 
            })
            .WithName("RunTrigger")
            .WithTags("RIoT");

            return endpoints;
        }

        /// <summary>
        /// Sends a stimulus for the RIoT trigger activity, starting or resuming workflows
        /// associated with the given event id. Shared by the HTTP endpoint (POST /riot/trigger/{id})
        /// and the gRPC <c>RIoTTriggerService.Trigger</c> method, so both transports behave identically.
        /// </summary>
        internal static async Task TriggerAsync(string id, object? data, IStimulusSender stimulusSender)
        {
            var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<RIoTTrigger>();
            string stimulus = id;

            var input = new Dictionary<string, object>
            {
                ["EventData"] = data!
            };

            var metadata = new StimulusMetadata()
            {
                Input = input,
            };

            // This will:
            // 1. Start any workflows where CustomEventTrigger.CanStartWorkflow = true
            // 2. Resume any suspended workflows waiting at a CustomEventTrigger bookmark
            await stimulusSender.SendAsync(activityTypeName, stimulus, metadata);
        }
    }
}
