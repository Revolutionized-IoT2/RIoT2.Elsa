using Elsa.Extensions;
using Microsoft.Extensions.Options;
using RIoT2.Core.Hosting;
using RIoT2.Core.Security;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Workflows;
using RIoT2.Elsa.Workflows.Llm;
using RIoT2.Elsa.Workflows.Services;

// The remote workflow host (ADR 0016): the orchestrator's RIOT2_WORKFLOW_MODE=remote runs workflows here, in their
// own container, and reaches this host's trigger endpoints and workflow management API. The designer lives in RIoT2.UI.
var builder = WebApplication.CreateBuilder(args);

// Console and Logs/RIoT2.log, text or JSON (RIOT2_LOG_FORMAT, RIOT2_LOG_LEVEL; design 7.4), the same
// way as the other RIoT2 services.
var loggerFactory = builder.AddRIoT2Logging("elsa");

// Every RIOT2_* setting is read and validated here, before anything uses it; an invalid
// setting stops the start with one message that lists every problem.
var workflowOptions = WorkflowOptions.Load(builder.Configuration, loggerFactory.CreateLogger("RIoT2.Elsa.Server"));
builder.Services.AddSingleton(Options.Create(workflowOptions));

// Metrics (riot2.workflow.*, riot2.mqtt.*) and, with RIOT2_TRACING=true, traces over OTLP, only when
// OTEL_EXPORTER_OTLP_ENDPOINT is set (design 7.4).
builder.AddRIoT2Telemetry("elsa", workflowOptions.Id, RIoT2.Core.Diagnostics.MeterNames.Elsa);

WorkflowEndpointConfiguration.Configure(builder.Configuration, workflowOptions.GrpcPort);

var services = builder.Services;
var configuration = builder.Configuration;

services.AddRIoT2Workflows(new RIoT2WorkflowsOptions
{
    Location = workflowOptions.Location,
    // HTTP endpoint activities keep the paths they had (the Http section, /api/workflows by default).
    HttpBasePath = configuration["Http:BasePath"] ?? "/api/workflows",
    ConfigureHttp = options => configuration.GetSection("Http").Bind(options)
});

// The activities reach the orchestrator over HTTP, with X-RIoT2-Key when RIOT2_API_KEY is set, through
// the outbound URL policy (design 7.5). The orchestrator's address arrives over MQTT.
services.AddSingleton<IRIoTConfigurationService, RIoTConfigurationService>();
services.AddSingleton<IWorkflowMqttService, WorkflowMqttService>();
services.AddHostedService<MqttBackgroundService>();
services.AddHttpClient<IRIoTDataService, RIoTDataService>(client => client.Timeout = TimeSpan.FromSeconds(60))
    .AddHttpMessageHandler(sp => new ApiKeyHandler(sp.GetRequiredService<IOptions<WorkflowOptions>>().Value.Security.ApiKey))
    .AddHttpMessageHandler(sp => new OutboundUrlPolicyHandler(
        sp.GetService<IOutboundUrlPolicy>() ?? AllowAllOutboundUrlPolicy.Instance, OutboundPurposes.Orchestrator));
// Run LLM steps run on the orchestrator, which keeps the model connections; the activity bounds each call.
services.AddHttpClient<ILlmRunner, HttpLlmRunner>(client => client.Timeout = Timeout.InfiniteTimeSpan)
    .AddHttpMessageHandler(sp => new ApiKeyHandler(sp.GetRequiredService<IOptions<WorkflowOptions>>().Value.Security.ApiKey))
    .AddHttpMessageHandler(sp => new OutboundUrlPolicyHandler(
        sp.GetService<IOutboundUrlPolicy>() ?? AllowAllOutboundUrlPolicy.Instance, OutboundPurposes.Orchestrator));

// The optional security mode's seams (design 7.5): the trigger, management and gRPC endpoints carry the
// RIoT2 policies, which the LocalTrust scheme passes while RIOT2_SECURITY_MODE is off. CORS allows any
// origin while the mode is off.
services.AddRIoT2Security(workflowOptions.Security, configureCors: policy => policy.WithExposedHeaders("*"));
services.AddGrpc();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseRouting();
app.UseRIoT2Security();
app.MapRIoTEndpoints();
app.MapGrpcService<RIoTTriggerGrpcService>() // <-- The transport the orchestrator's outbox uses
    .RequireAuthorization(SecurityPolicies.Operator);
var health = () => Results.Ok(new { status = "ok" });
app.MapGet("/health", health).AllowAnonymous();
app.MapGet("/healthz", health).AllowAnonymous();
// HTTP endpoint activities.
app.UseWorkflows();
app.Run();

/// <summary>The remote workflow host; public for the integration tests.</summary>
public partial class Program;
