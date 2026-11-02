using Elsa.Common;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Elsa.Retention.Extensions;
using Elsa.Retention.Models;
using Elsa.Studio.Branding;
using Elsa.Workflows;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Enums;
using Elsa.Workflows.Management.Models;
using Microsoft.AspNetCore.Mvc;
using RIoT2.Core.Hosting;
using RIoT2.Core.Security;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Extensions;
using RIoT2.Elsa.Server.RIoT.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

// Console and Logs/RIoT2.log, text or JSON (RIOT2_LOG_FORMAT, RIOT2_LOG_LEVEL; design 7.4), the same
// way as the other RIoT2 services.
var loggerFactory = builder.AddRIoT2Logging("elsa");

// Every RIOT2_* and ELSA_* setting is read and validated here, before anything uses it; an invalid
// setting stops the start with one message that lists every problem.
var workflowOptions = WorkflowOptions.Load(builder.Configuration, builder.Environment.IsDevelopment(),
    loggerFactory.CreateLogger("RIoT2.Elsa.Server"));
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(workflowOptions));

// Metrics (riot2.workflow.*, riot2.mqtt.*) and, with RIOT2_TRACING=true, traces over OTLP, only when
// OTEL_EXPORTER_OTLP_ENDPOINT is set (design 7.4).
builder.AddRIoT2Telemetry("elsa", workflowOptions.Id, RIoT2.Core.Diagnostics.MeterNames.Elsa);

WorkflowEndpointConfiguration.Configure(builder.Configuration, workflowOptions.GrpcPort);

var services = builder.Services;
var configuration = builder.Configuration;

// Development may run without a key; a random one is generated on each start.
var identitySigningKey = string.IsNullOrWhiteSpace(workflowOptions.IdentitySigningKey)
    ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
    : workflowOptions.IdentitySigningKey;

var sqLiteConnectionString = "Data Source=Data/elsa.sqlite.db;Cache=Shared;";
services
    .AddElsa(elsa => elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions = options => options.SigningKey = identitySigningKey;
            identity.UseAdminUserProvider();
        })
        .UseDefaultAuthentication()
        .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite(sqLiteConnectionString)))
        .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite(sqLiteConnectionString)))
        .UseScheduling()
        .UseJavaScript()
        .UseLiquid()
        .UseCSharp()
        .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
        .UseWorkflowsApi()
        .UseRIoT() // <-- Register the RIoT feature
        .AddActivitiesFrom<Program>()
        .AddWorkflowsFrom<Program>()
        .UseRetention(r => {
            r.SweepInterval = TimeSpan.FromMinutes(60); //check retention policies every hour
            r.AddDeletePolicy("Delete all finished workflows", sp =>
            {
                ISystemClock clock = sp.GetRequiredService<ISystemClock>();
                DateTimeOffset threshold = clock.UtcNow.Subtract(TimeSpan.FromHours(24));

                return new RetentionWorkflowInstanceFilter()
                {
                    TimestampFilters =
                    [
                        new TimestampFilter() {
                            Column = nameof(WorkflowInstance.FinishedAt),
                            Operator = TimestampFilterOperator.LessThanOrEqual,
                            Timestamp = threshold
                        }
                    ],
                    WorkflowStatus = WorkflowStatus.Finished
                };
            });
        })
    );

// The optional security mode's seams (design 7.5, phase S0): the RIoT trigger endpoints have the Operator
// policy, which the LocalTrust scheme passes while RIOT2_SECURITY_MODE is off (the only mode this
// version accepts). Elsa's own authentication stays the default scheme, so Studio's login is unchanged.
// CORS allows any origin while the mode is off.
services.AddRIoT2Security(workflowOptions.Security, defaultScheme: false, configureCors: policy => policy.WithExposedHeaders("*"));
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));
services.AddGrpc();
// One per process: it remembers the trigger message ids of the last ten minutes.
services.AddSingleton<TriggerDeduplicator>();
// Counts faulted workflow executions (riot2.workflow.faults).
services.AddNotificationHandler<WorkflowFaultMetrics>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseRouting();
app.MapRIoTEndpoints(); // <-- Map the RIoT endpoints
app.MapGrpcService<RIoTTriggerGrpcService>() // <-- Optional gRPC transport for the same trigger endpoint
    .RequireAuthorization(SecurityPolicies.Operator);
var health = () => Results.Ok(new { status = "ok" });
app.MapGet("/health", health).AllowAnonymous();
app.MapGet("/healthz", health).AllowAnonymous();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
// The middlewares are above; this sets the outbound URL policy and logs the mode.
app.UseRIoT2Security(pipeline: false);
app.UseWorkflowsApi();
app.UseWorkflows();
app.MapFallbackToPage("/_Host");
app.Run();