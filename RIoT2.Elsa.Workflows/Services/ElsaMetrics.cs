using System.Diagnostics.Metrics;
using Elsa.Mediator.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Notifications;
using RIoT2.Core.Diagnostics;

namespace RIoT2.Elsa.Workflows.Services
{
    /// <summary>
    /// Elsa's instruments (meter <see cref="MeterNames.Elsa"/>, design 7.4 phase 4). Nothing is measured
    /// while no listener is attached.
    /// </summary>
    public static class ElsaMetrics
    {
        public const string TriggersName = "riot2.workflow.triggers";
        public const string FaultsName = "riot2.workflow.faults";

        /// <summary><see cref="Triggers"/> results.</summary>
        public const string Received = "received";
        public const string Duplicate = "duplicate";

        private static readonly Meter Meter = new(MeterNames.Elsa);

        /// <summary>
        /// RIoT triggers, by <c>result</c> (<c>received</c>: sent to the workflows; <c>duplicate</c>: a
        /// redelivered message id, not run again) and <c>transport</c> (<c>grpc</c>, <c>http</c>, or <c>inprocess</c> in the orchestrator).
        /// </summary>
        public static readonly Counter<long> Triggers = Meter.CreateCounter<long>(TriggersName, "{trigger}",
            "RIoT workflow triggers, by result (received or duplicate) and transport (grpc, http or inprocess).");

        /// <summary>Workflow runs that ended faulted, by <c>definition</c> (the workflow definition id).</summary>
        public static readonly Counter<long> Faults = Meter.CreateCounter<long>(FaultsName, "{workflow}",
            "Workflow executions that faulted, by workflow definition id.");

        public static void Trigger(string result, string transport) =>
            Triggers.Add(1, new KeyValuePair<string, object?>("result", result), new KeyValuePair<string, object?>("transport", transport));
    }

    /// <summary>Counts faulted workflow executions in <see cref="ElsaMetrics.Faults"/>.</summary>
    public sealed class WorkflowFaultMetrics : INotificationHandler<WorkflowExecuted>
    {
        public Task HandleAsync(WorkflowExecuted notification, CancellationToken cancellationToken)
        {
            if (notification.WorkflowState.SubStatus == WorkflowSubStatus.Faulted)
                ElsaMetrics.Faults.Add(1, new KeyValuePair<string, object?>("definition", notification.WorkflowState.DefinitionId));
            return Task.CompletedTask;
        }
    }
}
