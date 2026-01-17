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
                var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<RIoTTrigger>();
                string stimulus = id;

                var input = new Dictionary<string, object>
                {
                    ["EventData"] = data
                };

                var metadata = new StimulusMetadata()
                {
                    Input = input,   
                };

                // This will:
                // 1. Start any workflows where CustomEventTrigger.CanStartWorkflow = true
                // 2. Resume any suspended workflows waiting at a CustomEventTrigger bookmark
                await stimulusSender.SendAsync(activityTypeName, stimulus, metadata);

                return Results.Ok(); 
            })
            .WithName("RunTrigger")
            .WithTags("RIoT");

            return endpoints;
        }
    }
}
