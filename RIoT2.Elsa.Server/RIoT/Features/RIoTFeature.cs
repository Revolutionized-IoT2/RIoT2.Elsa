using Elsa.Extensions;
using Elsa.Features.Abstractions;
using Elsa.Features.Services;
using Elsa.Http.UIHints;
using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
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
            Services.AddScoped<IPropertyUIHandler, RIoTTriggerOptionsProvider>();
  
            //Services.AddSingleton<IReportGenerator, ReportGenerator>();
            //Services.AddScoped<IReportRepository, ReportRepository>();

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
            // Optional: Perform actions that depend on other features
            // being fully configured
        }
    }
}
