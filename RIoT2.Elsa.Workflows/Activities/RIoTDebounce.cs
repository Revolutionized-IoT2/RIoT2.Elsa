using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Automation;

namespace RIoT2.Elsa.Workflows.Activities
{
    /// <summary>
    /// Debounce: waits <c>WindowSeconds</c>, then continues on <c>Done</c> only if no later run reached the same activity
    /// (key) in the meantime; the earlier runs continue on <c>Superseded</c>. So a burst of reports leads to one action,
    /// after it ends; "turn the light off five minutes after the last motion".
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Continues once no other run has arrived for the window (debounce)",
        DisplayName = "RIoT Debounce",
        Kind = ActivityKind.Action)]
    [FlowNode(DoneOutcome, SupersededOutcome)]
    public class RIoTDebounce : Activity
    {
        public const string DoneOutcome = "Done";
        public const string SupersededOutcome = "Superseded";
        public const int MaxWindowSeconds = 7 * 24 * 3600;

        private const string KeyProperty = "riot2:debounce-key";
        private const string TokenProperty = "riot2:debounce-token";

        [Input(Description = "Seconds without a new run before continuing, 1 to 604800", DefaultValue = 60)]
        public Input<int> WindowSeconds { get; set; } = new(60);

        [Input(Description = "Optional: share the window with other activities that use the same key")]
        public Input<string?> Key { get; set; } = default!;

        protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var window = WindowSeconds.GetOrDefault(context);
            if (window is < 1 or > MaxWindowSeconds)
                throw new InvalidOperationException($"The window must be 1 to {MaxWindowSeconds} seconds, not {window}.");
            var key = AutomationActivity.Key(context, Key?.GetOrDefault(context));
            var token = context.GetRequiredService<AutomationStateStore>().BeginDebounce(key);
            context.SetProperty(KeyProperty, key);
            context.SetProperty(TokenProperty, token);
            context.DelayFor(TimeSpan.FromSeconds(window), OnWindowEndedAsync);
            return ValueTask.CompletedTask;
        }

        private static async ValueTask OnWindowEndedAsync(ActivityExecutionContext context)
        {
            var key = context.GetProperty<string>(KeyProperty) ?? "";
            var token = context.GetProperty<string>(TokenProperty) ?? "";
            var latest = context.GetRequiredService<AutomationStateStore>().EndDebounce(key, token);
            await context.CompleteActivityWithOutcomesAsync(latest ? DoneOutcome : SupersededOutcome);
        }
    }

    /// <summary>
    /// Throttle: continues on <c>Pass</c> for the first run and then for at most one run per <c>WindowSeconds</c>; the
    /// others continue on <c>Suppressed</c>. "Notify at most once an hour".
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Lets at most one run per window through (throttle)",
        DisplayName = "RIoT Throttle",
        Kind = ActivityKind.Action)]
    [FlowNode(PassOutcome, SuppressedOutcome)]
    public class RIoTThrottle : Activity
    {
        public const string PassOutcome = "Pass";
        public const string SuppressedOutcome = "Suppressed";

        [Input(Description = "Seconds after a run that passed during which the others are suppressed, 1 to 604800", DefaultValue = 60)]
        public Input<int> WindowSeconds { get; set; } = new(60);

        [Input(Description = "Optional: share the window with other activities that use the same key")]
        public Input<string?> Key { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var window = WindowSeconds.GetOrDefault(context);
            if (window is < 1 or > RIoTDebounce.MaxWindowSeconds)
                throw new InvalidOperationException($"The window must be 1 to {RIoTDebounce.MaxWindowSeconds} seconds, not {window}.");
            var key = AutomationActivity.Key(context, Key?.GetOrDefault(context));
            var pass = context.GetRequiredService<AutomationStateStore>().TryPass(key, TimeSpan.FromSeconds(window));
            await context.CompleteActivityWithOutcomesAsync(pass ? PassOutcome : SuppressedOutcome);
        }
    }
}
