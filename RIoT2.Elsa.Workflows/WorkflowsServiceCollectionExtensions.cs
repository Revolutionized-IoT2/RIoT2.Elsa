using Elsa.Common;
using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Elsa.Retention.Extensions;
using Elsa.Retention.Models;
using Elsa.Workflows;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Enums;
using Elsa.Workflows.Management.Models;
using Microsoft.Extensions.Options;
using RIoT2.Elsa.Workflows.Management;

namespace RIoT2.Elsa.Workflows
{
    public static class WorkflowsServiceCollectionExtensions
    {
        /// <summary>
        /// Elsa with the RIoT activities, as both workflow hosts run it: the remote workflow host (RIoT2.Elsa.Server) and the
        /// orchestrator's embedded engine. Workflow management, the runtime and their SQLite persistence, scheduling,
        /// JavaScript, Liquid and (optionally) C# expressions and HTTP activities, the retention of finished instances, the
        /// RIoT feature, and the management service the designer uses. No Elsa REST API, identity or Studio.
        /// The host registers <see cref="Services.IRIoTDataService"/>.
        /// </summary>
        public static IServiceCollection AddRIoT2Workflows(this IServiceCollection services, RIoT2WorkflowsOptions options,
            Action<IModule>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new ArgumentException("A SQLite connection string is required.", nameof(options));
            if (options.FinishedRetention <= TimeSpan.Zero)
                throw new ArgumentException("The retention must be positive.", nameof(options));

            services.AddSingleton(Options.Create(options));
            var connectionString = options.ConnectionString;
            services.AddElsa(elsa =>
            {
                elsa
                    .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite(connectionString)))
                    .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite(connectionString)))
                    .UseScheduling()
                    .UseJavaScript()
                    .UseLiquid()
                    .UseRIoT()
                    .UseRetention(retention =>
                    {
                        retention.SweepInterval = TimeSpan.FromMinutes(60);
                        retention.AddDeletePolicy("Delete all finished workflows", sp =>
                        {
                            var clock = sp.GetRequiredService<ISystemClock>();
                            return new RetentionWorkflowInstanceFilter
                            {
                                TimestampFilters =
                                [
                                    new TimestampFilter
                                    {
                                        Column = nameof(WorkflowInstance.FinishedAt),
                                        Operator = TimestampFilterOperator.LessThanOrEqual,
                                        Timestamp = clock.UtcNow.Subtract(options.FinishedRetention)
                                    }
                                ],
                                WorkflowStatus = WorkflowStatus.Finished
                            };
                        });
                    });
                if (options.CSharpExpressions)
                    elsa.UseCSharp();
                if (options.HttpActivities)
                    elsa.UseHttp(http => http.ConfigureHttpOptions = httpOptions =>
                    {
                        httpOptions.BasePath = options.HttpBasePath;
                        options.ConfigureHttp?.Invoke(httpOptions);
                    });
                configure?.Invoke(elsa);
            });
            services.AddScoped<WorkflowManagementService>();
            return services;
        }

        /// <summary>Adds the RIoT activities and services to Elsa (<see cref="RIoTFeature"/>).</summary>
        public static IModule UseRIoT(this IModule module, Action<RIoTFeature>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(module);
            module.Use(configure);
            return module;
        }
    }
}
