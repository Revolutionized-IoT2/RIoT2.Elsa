using Elsa.Common.Serialization;
using Elsa.Extensions;
using Elsa.Features.Abstractions;
using Elsa.Features.Services;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RIoT2.Core.Models;
using RIoT2.Core.Security;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;

namespace RIoT2.Elsa.Workflows
{
    /// <summary>
    /// The RIoT activities and what they need, in any host: the activity pack's shared state, the webhook client, the
    /// schedules, the selectors' settings, the serialization aliases and the fault metrics. The host registers
    /// <see cref="IRIoTDataService"/> (the remote workflow host over HTTP, the orchestrator in process).
    /// </summary>
    public class RIoTFeature : FeatureBase
    {
        /// <summary>The serialization alias of <see cref="RIoTTemplateItem"/> in workflow JSON.</summary>
        public const string TemplateItemAlias = "RIoT2.RIoTTemplateItem";

        /// <summary>The type name Elsa 3.7 wrote for <see cref="RIoTTemplateItem"/> inputs.</summary>
        public const string LegacyTemplateItemTypeName = "RIoT2.Elsa.Studio.Models.RIoTTemplateItem, RIoT2.Elsa.Studio";

        public RIoTFeature(IModule module) : base(module)
        {
        }

        /// <summary>
        /// Gives the RIoT types that workflow JSON and workflow state hold a stable alias. Elsa 3.9 writes an unregistered
        /// type as "UnregisteredClrType:…" and never loads it back; the names earlier versions wrote still resolve.
        /// </summary>
        public static void RegisterTypes(SerializationTypeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.RegisterTypeAlias(typeof(RIoTTemplateItem), TemplateItemAlias);
            options.RegisterLegacyTypeName(typeof(RIoTTemplateItem), LegacyTemplateItemTypeName);
            options.RegisterTypeAlias(typeof(ElsaReport), "RIoT2.ElsaReport");
            options.RegisterLegacyTypeName(typeof(ElsaReport), "RIoT2.Elsa.Server.RIoT.Models.ElsaReport, RIoT2.Elsa.Server");
            options.RegisterTypeAlias(typeof(CommandConfirmation), "RIoT2.CommandConfirmation");
            options.RegisterLegacyTypeName(typeof(CommandConfirmation), "RIoT2.Elsa.Server.RIoT.Models.CommandConfirmation, RIoT2.Elsa.Server");
            options.RegisterTypeAlias(typeof(Command), "RIoT2.Command");
            options.RegisterLegacySimpleAssemblyQualifiedName(typeof(Command));
            options.RegisterTypeAlias(typeof(Variable), "RIoT2.Variable");
            options.RegisterLegacySimpleAssemblyQualifiedName(typeof(Variable));
        }

        public override void Configure()
        {
            Module.AddActivitiesFrom<RIoTFeature>();
            Services.Configure<SerializationTypeOptions>(RegisterTypes);

            // The selectors' settings, which the activity catalog passes to the designer.
            Services.AddScoped<IPropertyUIHandler, RIoTTriggerOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTDataOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTOutputOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTCommandOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTReportOptionsProvider>();
            Services.AddScoped<IPropertyUIHandler, RIoTForecastOptionsProvider>();

            // The activity pack (I48): state shared between runs, the webhook client and the schedules.
            Services.TryAddSingleton(TimeProvider.System);
            Services.AddSingleton<AutomationStateStore>();
            Services.AddHttpClient(RIoTWebhook.HttpClientName)
                .AddHttpMessageHandler(services => new OutboundUrlPolicyHandler(
                    services.GetService<IOutboundUrlPolicy>() ?? AllowAllOutboundUrlPolicy.Instance, RIoTWebhook.OutboundPurpose));
            Services.AddHostedService<ScheduleService>();

            // One per process: it remembers the trigger message ids of the last ten minutes.
            Services.TryAddSingleton<TriggerDeduplicator>();
            Services.TryAddSingleton<RIoTTriggers>();
            // Counts faulted workflow executions (riot2.workflow.faults).
            Services.AddNotificationHandler<WorkflowFaultMetrics>();
        }
    }
}
