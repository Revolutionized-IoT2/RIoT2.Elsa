using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;
using RIoT2.Elsa.Studio.Models;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// Sends a command and waits for the device's result (design 7.1 phase 4): <c>POST /api/v2/commands</c>
    /// with <c>"wait": true</c>. Continues on <c>Done</c> when the device ran it (or the node doesn't report
    /// results, <c>sent</c>), and on <c>Failed</c> when it failed, no device owns it, or it expired.
    /// </summary>
    [Activity(
       Namespace = "RIoT2",
       Category = "RIoT",
       Description = "Sends a command to RIoT and waits until the device has run it",
       DisplayName = "RIoT Command with confirmation",
       Kind = ActivityKind.Task)]
    [FlowNode(DoneOutcome, FailedOutcome)]
    public class RIoTConfirmedCommand : Activity
    {
        public const string DoneOutcome = "Done";
        public const string FailedOutcome = "Failed";

        /// <summary>The longest wait; the orchestrator client times out after 60 seconds.</summary>
        public const int MaxTimeoutSeconds = 50;

        [Input(
            Description = "Define RIoT Command to Execute",
            UIHint = "riot-output-selector",
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTOutputOptionsProvider)
            )]
        public Input<RIoTTemplateItem> Command { get; set; } = null!;

        [Input(Description = "Seconds to wait for the device's result, 1 to 50.", DefaultValue = 30)]
        public Input<int> TimeoutSeconds { get; set; } = new(30);

        [Output(Description = "The delivery status: executed, duplicate, sent, failed, unknownCommand or expired")]
        public Output<string> Status { get; set; } = default!;

        [Output(Description = "Why the command failed, if it did")]
        public Output<string?> Error { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var cmd = Command.Get(context);
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Id))
                throw new InvalidOperationException("RIoT Command with confirmation requires a command identifier.");
            var timeout = TimeoutSeconds.GetOrDefault(context);
            if (timeout is < 1 or > MaxTimeoutSeconds)
                throw new InvalidOperationException($"The timeout must be 1 to {MaxTimeoutSeconds} seconds, not {timeout}.");

            var value = await RIoTOutput.EvaluateValueAsync(context, cmd);
            var riot = context.GetRequiredService<IRIoTDataService>();
            var confirmation = await riot.ExecuteCommandWithConfirmationAsync(cmd.Id, value, timeout, context.CancellationToken);

            Status.Set(context, confirmation.Status);
            Error.Set(context, confirmation.Error);
            await context.CompleteActivityWithOutcomesAsync(confirmation.Succeeded ? DoneOutcome : FailedOutcome);
        }
    }
}
