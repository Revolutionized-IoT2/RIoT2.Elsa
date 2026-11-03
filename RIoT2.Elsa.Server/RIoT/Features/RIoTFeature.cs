using Elsa.Extensions;
using Elsa.Features.Abstractions;
using Elsa.Features.Services;
using Elsa.Workflows;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RIoT2.Core.Security;
using RIoT2.Elsa.Server.RIoT.Activities;
using RIoT2.Elsa.Server.RIoT.Automation;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;

namespace RIoT2.Elsa.Server.RIoT.Features
{
    /// <summary>
    /// Provides integration to RIoT-specific workflows and activities.
    /// </summary>
    public class RIoTFeature : FeatureBase
    {
        public RIoTFeature(IModule module) : base(module)
        {
        }

        /// <summary>
        /// Configure services, activities, and options.
        /// </summary>
        public override void Configure()
        {
            // Register all activities from this assembly
            Module.AddActivitiesFrom<RIoTFeature>();

            // Register custom services
            Services.AddSingleton<IRIoTConfigurationService, RIoTConfigurationService>();
            Services.AddSingleton<IWorkflowMqttService, WorkflowMqttService>();
            Services.AddScoped<IPropertyUIHandler, RIoTTriggerOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTDataOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTOutputOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTCommandOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTReportOptionsProvider>();
            //Services.AddScoped<IUIHintHandler, RIoTOutputSelectorUIHintHandler>();
            // Calls to the orchestrator send X-RIoT2-Key when RIOT2_API_KEY is set, and pass the outbound
            // URL policy (design 7.5).
            Services.AddHttpClient<IRIoTDataService, RIoTDataService>(client => client.Timeout = TimeSpan.FromSeconds(60))
                .AddHttpMessageHandler(services => new ApiKeyHandler(services.GetService<IOptions<WorkflowOptions>>()?.Value.Security.ApiKey))
                .AddHttpMessageHandler(services => new OutboundUrlPolicyHandler(
                    services.GetService<IOutboundUrlPolicy>() ?? AllowAllOutboundUrlPolicy.Instance, OutboundPurposes.Orchestrator));
            // The activity pack (I48): state shared between runs, the webhook client and the schedules.
            Services.TryAddSingleton(TimeProvider.System);
            Services.AddSingleton<AutomationStateStore>();
            Services.AddHttpClient(RIoTWebhook.HttpClientName)
                .AddHttpMessageHandler(services => new OutboundUrlPolicyHandler(
                    services.GetService<IOutboundUrlPolicy>() ?? AllowAllOutboundUrlPolicy.Instance, RIoTWebhook.OutboundPurpose));
            Services.AddHostedService<MqttBackgroundService>();
            Services.AddHostedService<ScheduleService>();
        }

        /// <summary>
        /// Post-configuration logic (optional).
        /// Called after all features have been configured.
        /// </summary>
        public override void Apply()
        {
            Services.Configure<WebApplicationOptions>(options =>
            {
                // Note: Actual endpoint mapping happens in Program.cs
                // This is just for documentation purposes
            });
        }
    }
}
