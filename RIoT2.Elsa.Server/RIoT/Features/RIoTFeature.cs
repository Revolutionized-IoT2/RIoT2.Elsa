using Elsa.Extensions;
using Elsa.Features.Abstractions;
using Elsa.Features.Services;
using Elsa.Http.UIHints;
using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Services;
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
            //Services.AddSingleton<IWorkflowMqttService, WorkflowMqttService>();
            //Services.AddHostedService(p => p.GetRequiredService<MqttBackgroundService>());
            Services.AddScoped<IPropertyUIHandler, RIoTTriggerOptionsProvider>();
            Services.AddSingleton<IRIoTDataService, RIoTDataService>();

            // Configure workflow options
            /*
            Module.ConfigureWorkflowsOptions(options =>
            {
     
                //options.AddCustomBookmarkType<CustomBookmark>();
            });*/
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
