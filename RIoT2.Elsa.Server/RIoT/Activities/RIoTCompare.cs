using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Elsa.Server.RIoT.Automation;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// Compares a value with a constant: continues on <c>True</c> or <c>False</c>. Without a value it compares the report
    /// that started the workflow, so a trigger can be filtered without an expression ("motion equals true").
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Compares the triggering report (or a value) with a constant",
        DisplayName = "RIoT Compare",
        Kind = ActivityKind.Action)]
    [FlowNode(TrueOutcome, FalseOutcome)]
    public class RIoTCompare : Activity
    {
        public const string TrueOutcome = "True";
        public const string FalseOutcome = "False";

        [Input(Description = "The value to compare; leave empty for the report that started the workflow. A report is compared by its value.")]
        public Input<object?> Value { get; set; } = default!;

        [Input(Description = "How to compare", UIHint = InputUIHints.DropDown, Options = new[] { "equals", "not equals", "greater than", "less than", "at least", "at most" }, DefaultValue = "equals")]
        public Input<string> Operator { get; set; } = new("equals");

        [Input(Description = "The constant: a number, true/false, or a text (compared ignoring case)")]
        public Input<string> Expected { get; set; } = new("");

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var op = AutomationValues.ParseOperator(Operator.GetOrDefault(context));
            var value = AutomationValues.Read(ValueOrEvent(context, Value));
            var matches = AutomationValues.Compare(value, op, Expected.GetOrDefault(context));
            await context.CompleteActivityWithOutcomesAsync(matches ? TrueOutcome : FalseOutcome);
        }

        /// <summary>The input's value, or the triggering report when the input is empty.</summary>
        internal static object? ValueOrEvent(ActivityExecutionContext context, Input<object?>? input)
        {
            var value = input == null ? null : context.Get(input);
            return value is null || value is string { Length: 0 } || value is string text && string.IsNullOrWhiteSpace(text)
                ? AutomationActivity.EventData(context)
                : value;
        }
    }
}
