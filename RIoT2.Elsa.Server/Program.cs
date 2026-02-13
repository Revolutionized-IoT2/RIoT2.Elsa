using Elsa.Common;
using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Management;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Elsa.Extensions;
using Elsa.Retention.Extensions;
using Elsa.Retention.Models;
using Elsa.Studio.Branding;
using Elsa.Workflows;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Enums;
using Elsa.Workflows.Management.Models;
using Microsoft.AspNetCore.Mvc;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var services = builder.Services;
var configuration = builder.Configuration;


services.AddLogging(logging => logging.AddConsole());
var sqLiteConnectionString = "Data Source=Data/elsa.sqlite.db;Cache=Shared;";
services
    .AddElsa(elsa => elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions = options => options.SigningKey = "large-signing-key-for-signing-JWT-tokens";
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

services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("*")));
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

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
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.UseWorkflows();
app.MapFallbackToPage("/_Host");
app.Run();