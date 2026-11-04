using System.Globalization;
using System.Text.Json.Nodes;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>
    /// Gives the orchestrator a schedule of values for one command (ADR 0019): the orchestrator sends each value at its time,
    /// also when this workflow has ended, and a new plan for the command replaces the old one. Empty steps end the command's
    /// plan. Continues on <c>Done</c>, or on <c>Failed</c> with the reason in <c>Error</c>.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Plans a command's values at future times, such as a heat pump's temperatures for the cheapest hours; replaces the command's plan",
        DisplayName = "RIoT Command Plan",
        Kind = ActivityKind.Task)]
    [FlowNode(DoneOutcome, FailedOutcome)]
    public class RIoTCommandPlan : Activity
    {
        public const string DoneOutcome = "Done";
        public const string FailedOutcome = "Failed";
        public const string CatchUpLatest = "latest";
        public const string CatchUpNone = "none";

        [Input(
            Description = "The command to plan",
            UIHint = RIoTSelectorUIHandler.UIHint,
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTCommandOptionsProvider))]
        public Input<RIoTTemplateItem> Command { get; set; } = null!;

        [Input(Description = "The steps: a list of { at, value } (at: an ISO 8601 time; start works too, so forecast points fit), " +
            "or an object with a steps list, such as Run LLM's JSON answer. Empty: end the command's plan.",
            DefaultSyntax = "JavaScript", UIHint = InputUIHints.MultiLine)]
        public Input<object?> Steps { get; set; } = default!;

        [Input(Description = "A name for the plan, shown with it")]
        public Input<string?> PlanName { get; set; } = default!;

        [Input(Description = "latest: after a pause, send the latest missed step; none: skip missed steps",
            UIHint = InputUIHints.DropDown, Options = new[] { CatchUpLatest, CatchUpNone }, DefaultValue = CatchUpLatest)]
        public Input<string> CatchUp { get; set; } = new(CatchUpLatest);

        [Output(Description = "The plan's id")]
        public Output<string?> PlanId { get; set; } = default!;

        [Output(Description = "How many steps the plan has")]
        public Output<int> StepCount { get; set; } = default!;

        [Output(Description = "Why the plan was refused")]
        public Output<string?> Error { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var command = Command.Get(context);
            if (command == null || string.IsNullOrWhiteSpace(command.Id))
                throw new InvalidOperationException("RIoT Command Plan needs a command.");

            var riot = context.GetRequiredService<IRIoTDataService>();
            CommandPlanReceipt receipt;
            if (!TryReadSteps(Steps.GetOrDefault(context), out var steps, out var problem))
            {
                receipt = CommandPlanReceipt.Failure(problem!);
            }
            else if (steps.Count == 0)
            {
                var ended = await riot.CancelCommandPlanAsync(command.Id, context.CancellationToken);
                receipt = new CommandPlanReceipt { Succeeded = true, PlanId = ended, StepCount = 0 };
            }
            else
            {
                var workflow = context.WorkflowExecutionContext.Workflow;
                receipt = await riot.SubmitCommandPlanAsync(new CommandPlanSubmission
                {
                    CommandId = command.Id,
                    Name = PlanName.GetOrDefault(context),
                    Source = $"workflow {workflow.WorkflowMetadata.Name ?? workflow.Identity.DefinitionId}",
                    CatchUp = (CatchUp.GetOrDefault(context) ?? CatchUpLatest).Trim(),
                    Steps = steps
                }, context.CancellationToken);
            }

            PlanId.Set(context, receipt.PlanId);
            StepCount.Set(context, receipt.StepCount);
            Error.Set(context, receipt.Error);
            if (!receipt.Succeeded)
                context.GetRequiredService<ILogger<RIoTCommandPlan>>().LogWarning("RIoT Command Plan {Activity} failed: {Error}", context.Activity.Id, receipt.Error);
            await context.CompleteActivityWithOutcomesAsync(receipt.Succeeded ? DoneOutcome : FailedOutcome);
        }

        /// <summary>Reads the steps: a list of { at | start, value }, or an object with a <c>steps</c> list; JSON text works too.</summary>
        public static bool TryReadSteps(object? value, out List<CommandPlanStepInput> steps, out string? problem)
        {
            steps = [];
            problem = null;
            JsonNode? node;
            try
            {
                node = ActivityJson.ToNode(value);
            }
            catch (Exception x) when (x is System.Text.Json.JsonException or NotSupportedException)
            {
                problem = $"The steps can't be read: {x.Message}";
                return false;
            }
            if (node is JsonObject wrapper && wrapper["steps"] is { } inner)
                node = inner;
            if (node == null)
                return true;
            if (node is not JsonArray array)
            {
                problem = "The steps must be a list of { at, value }.";
                return false;
            }
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not JsonObject step)
                {
                    problem = $"Step {i + 1} is not an object with at and value.";
                    return false;
                }
                var at = (step["at"] ?? step["start"]) as JsonValue;
                if (at == null || !TryTime(at, out var time))
                {
                    problem = $"Step {i + 1} has no valid time in at.";
                    return false;
                }
                if (!step.ContainsKey("value"))
                {
                    problem = $"Step {i + 1} has no value.";
                    return false;
                }
                steps.Add(new CommandPlanStepInput(time, ActivityJson.ToObject(step["value"])));
            }
            return true;
        }

        private static bool TryTime(JsonValue value, out DateTimeOffset time)
        {
            if (value.TryGetValue<string>(out var text))
                return DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);
            if (value.TryGetValue<DateTimeOffset>(out time))
                return true;
            if (value.TryGetValue<DateTime>(out var date))
            {
                time = new DateTimeOffset(date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime());
                return true;
            }
            time = default;
            return false;
        }
    }
}
