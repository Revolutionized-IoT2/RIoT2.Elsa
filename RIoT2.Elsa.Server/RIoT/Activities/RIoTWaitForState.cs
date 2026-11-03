using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Helpers;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Elsa.Server.RIoT.Automation;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Server.RIoT.UIHints;
using RIoT2.Elsa.Studio.Models;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// Waits until a report has a value: continues on <c>Done</c> at once when its latest value already matches, or when a
    /// matching report arrives, and on <c>Timed out</c> after <c>TimeoutSeconds</c>. It waits on the same stimulus as
    /// <c>RIoT Trigger</c> (the report id), so it needs no polling; the workflow is suspended meanwhile.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Waits until a report has a value, or times out",
        DisplayName = "RIoT Wait for state",
        Kind = ActivityKind.Action)]
    [FlowNode(DoneOutcome, TimedOutOutcome)]
    public class RIoTWaitForState : Activity
    {
        public const string DoneOutcome = "Done";
        public const string TimedOutOutcome = "Timed out";
        public const int MaxTimeoutSeconds = 7 * 24 * 3600;

        private const string ReportProperty = "riot2:wait-report";
        private const string OperatorProperty = "riot2:wait-operator";
        private const string ExpectedProperty = "riot2:wait-expected";

        [Input(
            Description = "The report to wait for",
            UIHint = "riot-output-selector",
            DefaultSyntax = "JavaScript",
            UIHandler = typeof(RIoTReportOptionsProvider))]
        public Input<RIoTTemplateItem> Report { get; set; } = null!;

        [Input(Description = "How to compare", UIHint = InputUIHints.DropDown, Options = new[] { "equals", "not equals", "greater than", "less than", "at least", "at most" }, DefaultValue = "equals")]
        public Input<string> Operator { get; set; } = new("equals");

        [Input(Description = "The value to wait for: a number, true/false, or a text (compared ignoring case)")]
        public Input<string> Expected { get; set; } = new("");

        [Input(Description = "Seconds to wait, 1 to 604800", DefaultValue = 300)]
        public Input<int> TimeoutSeconds { get; set; } = new(300);

        [Output(Description = "The value that matched")]
        public Output<object?> Value { get; set; } = default!;

        /// <summary>The bookmark name of a report stimulus: the one <c>RIoT Trigger</c> uses.</summary>
        public static string ReportStimulusName => ActivityTypeNameHelper.GenerateTypeName<RIoTTrigger>();

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var report = Report.Get(context);
            if (report == null || string.IsNullOrWhiteSpace(report.Id))
                throw new InvalidOperationException("RIoT Wait for state requires a report.");
            var op = AutomationValues.ParseOperator(Operator.GetOrDefault(context));
            var expected = Expected.GetOrDefault(context) ?? "";
            var timeout = TimeoutSeconds.GetOrDefault(context);
            if (timeout is < 1 or > MaxTimeoutSeconds)
                throw new InvalidOperationException($"The timeout must be 1 to {MaxTimeoutSeconds} seconds, not {timeout}.");

            // The latest value first: a report that already matches needs no wait. A report without a value yet
            // (time stamp 0) never matches.
            var current = await context.GetRequiredService<IRIoTDataService>().GetReportValueAsync(report.Id);
            if (current is ElsaReport { TimeStamp: > 0 } latest && await TryCompleteAsync(context, AutomationValues.Read(latest.Value), op, expected))
                return;

            context.SetProperty(ReportProperty, report.Id);
            context.SetProperty(OperatorProperty, op.ToString());
            context.SetProperty(ExpectedProperty, expected);
            context.CreateBookmark(new CreateBookmarkArgs
            {
                Stimulus = report.Id,
                BookmarkName = ReportStimulusName,
                Callback = OnReportAsync,
                AutoBurn = false,
                IncludeActivityInstanceId = false
            });
            context.DelayFor(TimeSpan.FromSeconds(timeout), OnTimeoutAsync);
        }

        private static async ValueTask OnReportAsync(ActivityExecutionContext context)
        {
            var op = Enum.Parse<CompareOperator>(context.GetProperty<string>(OperatorProperty) ?? nameof(CompareOperator.Equals));
            var expected = context.GetProperty<string>(ExpectedProperty) ?? "";
            await TryCompleteAsync(context, AutomationValues.Read(AutomationActivity.EventData(context)), op, expected);
        }

        private static async ValueTask OnTimeoutAsync(ActivityExecutionContext context)
        {
            context.ClearBookmarks();
            await context.CompleteActivityWithOutcomesAsync(TimedOutOutcome);
        }

        private async static ValueTask<bool> TryCompleteAsync(ActivityExecutionContext context, System.Text.Json.JsonElement value, CompareOperator op, string expected)
        {
            if (!AutomationValues.Compare(value, op, expected))
                return false;
            context.Set(((RIoTWaitForState)context.Activity).Value, AutomationValues.ToPlain(value));
            context.ClearBookmarks();
            await context.CompleteActivityWithOutcomesAsync(DoneOutcome);
            return true;
        }
    }
}
