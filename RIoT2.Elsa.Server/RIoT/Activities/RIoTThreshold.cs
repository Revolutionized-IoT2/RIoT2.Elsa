using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.Automation;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// A threshold with hysteresis: the state becomes high above <c>Upper</c> and low below <c>Lower</c>, and stays in
    /// between. Continues on <c>Above</c> or <c>Below</c> when the state changes, else on <c>Unchanged</c>. The state is
    /// kept between workflow runs by key (in memory; unknown again after a restart).
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Continues on Above or Below when a value crosses a threshold, with hysteresis",
        DisplayName = "RIoT Threshold",
        Kind = ActivityKind.Action)]
    [FlowNode(Hysteresis.Above, Hysteresis.Below, Hysteresis.Unchanged)]
    public class RIoTThreshold : Activity
    {
        [Input(Description = "The number; leave empty for the report that started the workflow")]
        public Input<object?> Value { get; set; } = default!;

        [Input(Description = "Above this the state becomes high")]
        public Input<double> Upper { get; set; } = new(0d);

        [Input(Description = "Below this the state becomes low; at most Upper. The same as Upper for a plain threshold.")]
        public Input<double> Lower { get; set; } = new(0d);

        [Input(Description = "Optional: share the state with other thresholds that use the same key")]
        public Input<string?> Key { get; set; } = default!;

        [Output(Description = "The state after this value: high, low or unknown")]
        public Output<string> State { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var upper = Upper.GetOrDefault(context);
            var lower = Lower.GetOrDefault(context);
            if (lower > upper)
                throw new InvalidOperationException($"The lower limit ({lower}) must not be above the upper limit ({upper}).");
            var number = AutomationValues.ToNumber(AutomationValues.Read(RIoTCompare.ValueOrEvent(context, Value)));
            if (number == null)
            {
                context.GetRequiredService<ILogger<RIoTThreshold>>().LogWarning("RIoT Threshold {Activity} got a value that isn't a number", context.Activity.Id);
                State.Set(context, "unknown");
                await context.CompleteActivityWithOutcomesAsync(Hysteresis.Unchanged);
                return;
            }
            var key = AutomationActivity.Key(context, Key?.GetOrDefault(context));
            var (state, outcome) = context.GetRequiredService<AutomationStateStore>().Threshold(key, number.Value, lower, upper);
            State.Set(context, state.ToString().ToLowerInvariant());
            await context.CompleteActivityWithOutcomesAsync(outcome);
        }
    }
}
