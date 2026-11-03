using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.Automation;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// Starts a workflow at a local time of day, or at sunrise or sunset (with an offset in minutes), on chosen days. Inside
    /// a workflow it waits for the next such time. <see cref="ScheduleService"/> fires it; sunrise and sunset need
    /// <c>RIOT2_LATITUDE</c> and <c>RIOT2_LONGITUDE</c>, and times are in the host's time zone (<c>TZ</c>).
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Starts a workflow at a time of day, or at sunrise or sunset, on chosen days",
        DisplayName = "RIoT Schedule",
        Kind = ActivityKind.Trigger)]
    public class RIoTSchedule : Trigger, IStartNode
    {
        /// <summary>The workflow input with the time the schedule was due.</summary>
        public const string FiredAtInput = "FiredAt";

        [Input(Description = "07:30, sunrise or sunset; sunrise and sunset take an offset in minutes: sunset-30, sunrise+15", DefaultValue = "07:00")]
        public Input<string> At { get; set; } = new("07:00");

        [Input(Description = "Empty or daily, weekdays, weekends, or days separated by commas: mon,wed,fri")]
        public Input<string> Days { get; set; } = new("");

        [Output(Description = "The time the schedule was due")]
        public Output<DateTimeOffset> FiredAt { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            if (context.IsTriggerOfWorkflow())
            {
                await OnFiredAsync(context);
                return;
            }
            var spec = Parse(At.GetOrDefault(context), Days.GetOrDefault(context));
            context.CreateBookmark(new CreateBookmarkArgs { Stimulus = spec.Key, Callback = OnFiredAsync, IncludeActivityInstanceId = false });
        }

        protected override object GetTriggerPayload(TriggerIndexingContext context)
        {
            var at = At.Get(context.ExpressionExecutionContext);
            var days = Days.Get(context.ExpressionExecutionContext);
            // An invalid schedule is still indexed (the service warns about it), so the definition can be saved and fixed.
            return ScheduleSpec.TryParse(at, days, out var spec, out _) ? spec!.Key : $"invalid {at}|{days}";
        }

        private static ScheduleSpec Parse(string? at, string? days) =>
            ScheduleSpec.TryParse(at, days, out var spec, out var error) ? spec! : throw new InvalidOperationException(error);

        private static async ValueTask OnFiredAsync(ActivityExecutionContext context)
        {
            var activity = (RIoTSchedule)context.Activity;
            if (context.WorkflowInput.TryGetValue(FiredAtInput, out var firedAt) && firedAt is DateTimeOffset time)
                context.Set(activity.FiredAt, time);
            else if (firedAt is DateTime dateTime)
                context.Set(activity.FiredAt, new DateTimeOffset(dateTime));
            await context.CompleteActivityAsync();
        }
    }
}
