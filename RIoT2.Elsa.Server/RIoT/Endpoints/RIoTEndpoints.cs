using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RIoT2.Core.Security;
using RIoT2.Elsa.Workflows.Management;
using RIoT2.Elsa.Workflows.Services;

namespace RIoT2.Elsa.Server.RIoT.Endpoints
{
    public static class RIoTEndpoints
    {
        /// <summary>
        /// <c>POST /riot/trigger/{id}</c> and the workflow management API at <c>/riot/workflows</c>, which the orchestrator
        /// calls in the remote workflow mode (ADR 0016). Reads need the Viewer policy, changes Admin, and triggers and
        /// cancellations Operator (passed by the LocalTrust scheme while the security mode is off, design 7.5).
        /// </summary>
        public static IEndpointRouteBuilder MapRIoTEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/riot").WithTags("RIoT");

            group.MapPost("/trigger/{id}", static async (string id, [FromBody] JsonElement data, RIoTTriggers triggers, CancellationToken cancellationToken) =>
            {
                await triggers.TriggerOnceAsync(null, id, data, "http", cancellationToken);
                return Results.Ok();
            })
            .WithName("RunTrigger")
            .RequireAuthorization(SecurityPolicies.Operator);

            var workflows = endpoints.MapGroup("/riot/workflows").WithTags("RIoT");
            MapWorkflows(workflows);
            return endpoints;
        }

        private static void MapWorkflows(RouteGroupBuilder workflows)
        {
            workflows.MapGet("", static (WorkflowManagementService service, CancellationToken ct) => service.ListAsync(ct))
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapGet("/catalog", static (WorkflowManagementService service) => Results.Json(service.GetCatalog()))
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapGet("/runs", static (WorkflowManagementService service, string? definitionId, string? status, int? page, int? pageSize, CancellationToken ct) =>
                    service.ListRunsAsync(definitionId, status, page ?? 0, pageSize ?? 25, ct))
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapGet("/runs/{instanceId}", static async (string instanceId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.GetRunAsync(instanceId, ct) is { } run ? Results.Ok(run) : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapPost("/runs/{instanceId}/cancel", static async (string instanceId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.CancelRunAsync(instanceId, ct) ? Results.NoContent() : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Operator);

            workflows.MapPost("", static async ([FromBody] SaveWorkflowRequest request, WorkflowManagementService service, CancellationToken ct) =>
                    Saved(await service.SaveAsync(null, request.Model, request.Publish, ct)))
                .RequireAuthorization(SecurityPolicies.Admin);
            workflows.MapPost("/import", static async ([FromBody] SaveWorkflowRequest request, WorkflowManagementService service, CancellationToken ct) =>
                    Saved(await service.ImportAsync(request.Model, request.Publish, ct)))
                .RequireAuthorization(SecurityPolicies.Admin);

            workflows.MapGet("/{definitionId}", static async (string definitionId, int? version, WorkflowManagementService service, CancellationToken ct) =>
                    await service.GetAsync(definitionId, version, ct) is { } workflow ? Results.Ok(workflow) : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapGet("/{definitionId}/versions", static (string definitionId, WorkflowManagementService service, CancellationToken ct) =>
                    service.ListVersionsAsync(definitionId, ct))
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapGet("/{definitionId}/export", static async (string definitionId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.ExportAsync(definitionId, ct) is { } export ? Results.Ok(export) : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Viewer);
            workflows.MapPut("/{definitionId}", static async (string definitionId, [FromBody] SaveWorkflowRequest request, WorkflowManagementService service, CancellationToken ct) =>
                    Saved(await service.SaveAsync(definitionId, request.Model, request.Publish, ct)))
                .RequireAuthorization(SecurityPolicies.Admin);
            workflows.MapPost("/{definitionId}/publish", static async (string definitionId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.PublishAsync(definitionId, ct) is { } result ? Saved(result) : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Admin);
            workflows.MapPost("/{definitionId}/retract", static async (string definitionId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.RetractAsync(definitionId, ct) ? Results.NoContent() : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Admin);
            workflows.MapDelete("/{definitionId}", static async (string definitionId, WorkflowManagementService service, CancellationToken ct) =>
                    await service.DeleteAsync(definitionId, ct) ? Results.NoContent() : Results.NotFound())
                .RequireAuthorization(SecurityPolicies.Admin);
        }

        // A failed save answers 400 with the same body, so callers read the errors either way.
        private static IResult Saved(WorkflowSaveResult result) => result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }
}
